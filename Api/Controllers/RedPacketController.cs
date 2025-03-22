using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RedPacketController : ControllerBase
{
    private readonly RedPacketService _redPacketService;
    private readonly ILogger<RedPacketController> _logger;

    public RedPacketController(RedPacketService redPacketService, ILogger<RedPacketController> logger)
    {
        _redPacketService = redPacketService;
        _logger = logger;
    }

    /// <summary>
    /// 创建红包
    /// </summary>
    [HttpPost("create")]
    public async Task<IActionResult> CreateRedPacket([FromBody] CreateRedPacketRequest request)
    {
        try
        {
            string redPacketId = await _redPacketService.CreateRedPacketAsync(request);
            return Ok(new { success = true, redPacketId = redPacketId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建红包失败");
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 抢红包
    /// </summary>
    [HttpPost("grab")]
    public async Task<IActionResult> GrabRedPacket([FromBody] GrabRedPacketRequest request)
    {
        try
        {
            RedPacketRecord record = await _redPacketService.GrabRedPacketAsync(request);
            return Ok(new { success = true, amount = record.Amount, grabTime = record.GrabTime });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "抢红包失败");
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 获取红包信息
    /// </summary>
    [HttpGet("{redPacketId}")]
    public async Task<IActionResult> GetRedPacket(string redPacketId)
    {
        try
        {
            RedPacket redPacket = await _redPacketService.GetRedPacketAsync(redPacketId);
            if (redPacket == null)
            {
                return NotFound(new { success = false, message = "红包不存在" });
            }

            return Ok(new { success = true, redPacket = redPacket });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取红包信息失败");
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 获取用户抢红包记录
    /// </summary>
    [HttpGet("{redPacketId}/record/{userId}")]
    public async Task<IActionResult> GetUserRedPacketRecord(string redPacketId, string userId)
    {
        try
        {
            RedPacketRecord record = await _redPacketService.GetUserRedPacketRecordAsync(redPacketId, userId);
            if (record == null)
            {
                return NotFound(new { success = false, message = "抢红包记录不存在" });
            }

            return Ok(new { success = true, record = record });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取抢红包记录失败");
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}