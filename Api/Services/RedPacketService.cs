using System.Text.Json;
using Api.Models;
using StackExchange.Redis;

namespace Api.Services;

public class RedPacketService
{
    private readonly RedisService _redisService;
    private readonly ILogger<RedPacketService> _logger;
    private const string RedPacketKeyPrefix = "redpacket:";
    private const string RedPacketRecordKeyPrefix = "redpacket:record:";
    private const string RedPacketLockKeyPrefix = "redpacket:lock:";

    public RedPacketService(RedisService redisService, ILogger<RedPacketService> logger)
    {
        _redisService = redisService;
        _logger = logger;
    }

    /// <summary>
    /// 创建红包
    /// </summary>
    public async Task<string> CreateRedPacketAsync(CreateRedPacketRequest request)
    {
        // 参数校验
        if (request.TotalAmount <= 0 || request.TotalCount <= 0)
        {
            throw new ArgumentException("红包金额和数量必须大于0");
        }

        // 生成红包ID
        string redPacketId = Guid.NewGuid().ToString("N");

        // 生成红包
        RedPacket redPacket = new RedPacket
        {
            Id = redPacketId,
            TotalAmount = request.TotalAmount,
            TotalCount = request.TotalCount,
            RemainAmount = request.TotalAmount,
            RemainCount = request.TotalCount,
            CreatorId = request.CreatorId,
            CreatedTime = DateTime.Now,
            ExpireTime = DateTime.Now.AddMinutes(request.ExpireMinutes)
        };

        // 预先计算红包金额并存储
        List<int> amounts = DivideRedPacket(request.TotalAmount, request.TotalCount);
        string amountsJson = JsonSerializer.Serialize(amounts);

        // 使用Redis事务保证原子性
        var tran = _redisService.ScriptEvaluateAsync(@"
                -- 存储红包信息
                redis.call('SET', KEYS[1], ARGV[1], 'EX', ARGV[3])
                -- 存储红包金额列表
                redis.call('SET', KEYS[2], ARGV[2], 'EX', ARGV[3])
                return 1
            ",
            new RedisKey[]
            {
                $"{RedPacketKeyPrefix}{redPacketId}",
                $"{RedPacketKeyPrefix}{redPacketId}:amounts"
            },
            new RedisValue[]
            {
                JsonSerializer.Serialize(redPacket),
                amountsJson,
                request.ExpireMinutes * 60
            });

        await tran;

        return redPacketId;
    }

    /// <summary>
    /// 抢红包
    /// </summary>
    public async Task<RedPacketRecord> GrabRedPacketAsync(GrabRedPacketRequest request)
    {
        string redPacketId = request.RedPacketId;
        string userId = request.UserId;
        string redPacketKey = $"{RedPacketKeyPrefix}{redPacketId}";
        string lockKey = $"{RedPacketLockKeyPrefix}{redPacketId}:{userId}";
        string lockValue = Guid.NewGuid().ToString("N");

        try
        {
            // 使用分布式锁确保每个用户只能抢一次
            bool lockAcquired = await _redisService.LockTakeAsync(lockKey, lockValue, TimeSpan.FromSeconds(10));
            if (!lockAcquired)
            {
                throw new InvalidOperationException("您已参与过此红包或操作过于频繁，请稍后再试");
            }

            // 使用Lua脚本保证抢红包操作的原子性
            string luaScript = @"
                    -- 获取红包信息
                    local redPacketJson = redis.call('GET', KEYS[1])
                    if not redPacketJson then
                        return {err = 'RED_PACKET_NOT_FOUND'}
                    end
                    
                    -- 检查用户是否已经抢过红包
                    local recordKey = KEYS[2] .. ARGV[1]
                    local hasGrabbed = redis.call('EXISTS', recordKey)
                    if hasGrabbed == 1 then
                        return {err = 'ALREADY_GRABBED'}
                    end
                    
                    -- 解析红包信息
                    local redPacket = cjson.decode(redPacketJson)
                    
                    -- 检查红包是否已抢完
                    if redPacket.RemainCount <= 0 then
                        return {err = 'RED_PACKET_EMPTY'}
                    end
                    
                    -- 检查红包是否过期
                    local now = tonumber(ARGV[2])
                    local expireTime = tonumber(string.sub(redPacket.ExpireTime, 7, 16))
                    if now > expireTime then
                        return {err = 'RED_PACKET_EXPIRED'}
                    end
                    
                    -- 获取红包金额列表
                    local amountsJson = redis.call('GET', KEYS[3])
                    if not amountsJson then
                        return {err = 'AMOUNTS_NOT_FOUND'}
                    end
                    
                    local amounts = cjson.decode(amountsJson)
                    
                    -- 抢红包（随机选择一个金额）
                    local index = redPacket.TotalCount - redPacket.RemainCount + 1
                    local amount = amounts[tostring(index)]
                    
                    -- 更新红包信息
                    redPacket.RemainCount = redPacket.RemainCount - 1
                    redPacket.RemainAmount = redPacket.RemainAmount - amount
                    
                    -- 保存更新后的红包信息
                    redis.call('SET', KEYS[1], cjson.encode(redPacket))
                    
                    -- 记录抢红包记录
                    local record = {
                        Id = KEYS[4],
                        RedPacketId = redPacket.Id,
                        UserId = ARGV[1],
                        Amount = amount,
                        GrabTime = ARGV[2]
                    }
                    
                    redis.call('SET', recordKey, cjson.encode(record))
                    
                    -- 返回抢到的金额
                    return {amount = amount, remainCount = redPacket.RemainCount, remainAmount = redPacket.RemainAmount}
                ";

            string recordId = Guid.NewGuid().ToString("N");
            string nowTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();

            RedisResult result = await _redisService.ScriptEvaluateAsync(luaScript,
                new RedisKey[]
                {
                    redPacketKey,
                    $"{RedPacketRecordKeyPrefix}{redPacketId}:",
                    $"{RedPacketKeyPrefix}{redPacketId}:amounts",
                    recordId
                },
                new RedisValue[]
                {
                    userId,
                    nowTimestamp
                });

            // 解析结果
            if (result.Type == ResultType.MultiBulk)
            {
                RedisResult[] resultArray = (RedisResult[])result;
                if (resultArray.Length > 0 && resultArray[0].ToString() == "err")
                {
                    string error = resultArray[1].ToString();
                    switch (error)
                    {
                        case "RED_PACKET_NOT_FOUND":
                            throw new InvalidOperationException("红包不存在");
                        case "ALREADY_GRABBED":
                            throw new InvalidOperationException("您已经抢过此红包");
                        case "RED_PACKET_EMPTY":
                            throw new InvalidOperationException("红包已被抢完");
                        case "RED_PACKET_EXPIRED":
                            throw new InvalidOperationException("红包已过期");
                        case "AMOUNTS_NOT_FOUND":
                            throw new InvalidOperationException("红包金额信息不存在");
                        default:
                            throw new InvalidOperationException($"抢红包失败: {error}");
                    }
                }

                int amount = (int)resultArray[0];
                int remainCount = (int)resultArray[1];
                int remainAmount = (int)resultArray[2];

                // 创建抢红包记录
                RedPacketRecord record = new RedPacketRecord
                {
                    Id = recordId,
                    RedPacketId = redPacketId,
                    UserId = userId,
                    Amount = amount,
                    GrabTime = DateTime.Now
                };

                _logger.LogInformation($"用户 {userId} 抢到红包 {redPacketId}，金额: {amount / 100.0:F2}元，" +
                                       $"剩余: {remainCount}个，{remainAmount / 100.0:F2}元");

                return record;
            }
            else
            {
                throw new InvalidOperationException("抢红包失败，请重试");
            }
        }
        finally
        {
            // 释放分布式锁
            await _redisService.LockReleaseAsync(lockKey, lockValue);
        }
    }

    /// <summary>
    /// 获取红包信息
    /// </summary>
    public async Task<RedPacket> GetRedPacketAsync(string redPacketId)
    {
        string key = $"{RedPacketKeyPrefix}{redPacketId}";
        RedPacket redPacket = await _redisService.GetObjectAsync<RedPacket>(key);
        return redPacket;
    }

    /// <summary>
    /// 获取用户抢红包记录
    /// </summary>
    public async Task<RedPacketRecord> GetUserRedPacketRecordAsync(string redPacketId, string userId)
    {
        string key = $"{RedPacketRecordKeyPrefix}{redPacketId}:{userId}";
        RedPacketRecord record = await _redisService.GetObjectAsync<RedPacketRecord>(key);
        return record;
    }

    /// <summary>
    /// 红包金额分配算法（二倍均值法）
    /// </summary>
    private List<int> DivideRedPacket(int totalAmount, int totalCount)
    {
        Random random = new Random();
        List<int> amounts = new List<int>();

        // 剩余金额
        int remainAmount = totalAmount;
        // 剩余数量
        int remainCount = totalCount;

        for (int i = 0; i < totalCount - 1; i++)
        {
            // 确保每个红包至少有1分钱
            int minAmount = 1;
            // 使用二倍均值法，上限为剩余平均值的2倍
            int maxAmount = remainCount > 1
                ? Math.Max(1, (remainAmount / remainCount) * 2)
                : remainAmount;

            // 随机金额
            int amount = random.Next(minAmount, maxAmount + 1);
            amounts.Add(amount);

            remainAmount -= amount;
            remainCount--;
        }

        // 最后一个红包，把剩余金额全部放进去
        amounts.Add(remainAmount);

        // 随机打乱红包顺序
        return amounts.OrderBy(x => random.Next()).ToList();
    }
}