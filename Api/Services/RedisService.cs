using System.Text.Json;
using StackExchange.Redis;

namespace Api.Services;

public class RedisService
{
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _db;

    public RedisService(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis");
        _redis = ConnectionMultiplexer.Connect(connectionString);
        _db = _redis.GetDatabase();
    }

    /// <summary>
    /// 设置字符串值
    /// </summary>
    public async Task<bool> StringSetAsync(string key, string value, TimeSpan? expiry = null)
    {
        return await _db.StringSetAsync(key, value, expiry);
    }

    /// <summary>
    /// 获取字符串值
    /// </summary>
    public async Task<string> StringGetAsync(string key)
    {
        return await _db.StringGetAsync(key);
    }

    /// <summary>
    /// 设置哈希表字段值
    /// </summary>
    public async Task<bool> HashSetAsync(string key, string field, string value)
    {
        return await _db.HashSetAsync(key, field, value);
    }

    /// <summary>
    /// 获取哈希表字段值
    /// </summary>
    public async Task<string> HashGetAsync(string key, string field)
    {
        return await _db.HashGetAsync(key, field);
    }

    /// <summary>
    /// 获取哈希表所有字段值
    /// </summary>
    public async Task<Dictionary<string, string>> HashGetAllAsync(string key)
    {
        var entries = await _db.HashGetAllAsync(key);
        return entries.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString());
    }

    /// <summary>
    /// 执行Lua脚本
    /// </summary>
    public async Task<RedisResult> ScriptEvaluateAsync(string script, RedisKey[] keys = null,
        RedisValue[] values = null)
    {
        return await _db.ScriptEvaluateAsync(script, keys, values);
    }

    /// <summary>
    /// 设置过期时间
    /// </summary>
    public async Task<bool> KeyExpireAsync(string key, TimeSpan expiry)
    {
        return await _db.KeyExpireAsync(key, expiry);
    }

    /// <summary>
    /// 分布式锁获取
    /// </summary>
    public async Task<bool> LockTakeAsync(string key, string value, TimeSpan expiry)
    {
        return await _db.LockTakeAsync(key, value, expiry);
    }

    /// <summary>
    /// 分布式锁释放
    /// </summary>
    public async Task<bool> LockReleaseAsync(string key, string value)
    {
        return await _db.LockReleaseAsync(key, value);
    }

    /// <summary>
    /// 将对象序列化为JSON并存储
    /// </summary>
    public async Task<bool> SetObjectAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var json = JsonSerializer.Serialize(value);
        return await StringSetAsync(key, json, expiry);
    }

    /// <summary>
    /// 获取并反序列化为对象
    /// </summary>
    public async Task<T> GetObjectAsync<T>(string key)
    {
        var json = await StringGetAsync(key);
        if (string.IsNullOrEmpty(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json);
    }
}