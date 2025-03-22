namespace Api.Models;

/// <summary>
/// 红包模型
/// </summary>
public class RedPacket
{
    /// <summary>
    /// 红包ID
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// 红包总金额（单位：分）
    /// </summary>
    public int TotalAmount { get; set; }

    /// <summary>
    /// 红包总数量
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 剩余金额（单位：分）
    /// </summary>
    public int RemainAmount { get; set; }

    /// <summary>
    /// 剩余数量
    /// </summary>
    public int RemainCount { get; set; }

    /// <summary>
    /// 创建者用户ID
    /// </summary>
    public string CreatorId { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>
    /// 红包过期时间
    /// </summary>
    public DateTime ExpireTime { get; set; }
}

/// <summary>
/// 红包领取记录
/// </summary>
public class RedPacketRecord
{
    /// <summary>
    /// 记录ID
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// 红包ID
    /// </summary>
    public string RedPacketId { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// 抢到的金额（单位：分）
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    /// 抢红包时间
    /// </summary>
    public DateTime GrabTime { get; set; }
}

/// <summary>
/// 创建红包请求
/// </summary>
public class CreateRedPacketRequest
{
    /// <summary>
    /// 红包总金额（单位：分）
    /// </summary>
    public int TotalAmount { get; set; }

    /// <summary>
    /// 红包总数量
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 创建者用户ID
    /// </summary>
    public string CreatorId { get; set; }

    /// <summary>
    /// 红包有效期（分钟）
    /// </summary>
    public int ExpireMinutes { get; set; } = 24 * 60; // 默认24小时
}

/// <summary>
/// 抢红包请求
/// </summary>
public class GrabRedPacketRequest
{
    /// <summary>
    /// 红包ID
    /// </summary>
    public string RedPacketId { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    public string UserId { get; set; }
}