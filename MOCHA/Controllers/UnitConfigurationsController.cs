using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MOCHA.Models.Architecture;
using MOCHA.Models.Auth;
using MOCHA.Services.Architecture;

namespace MOCHA.Controllers;

/// <summary>
/// 装置ユニット構成管理用 API
/// </summary>
[ApiController]
[Route("api/unit-configurations")]
public sealed class UnitConfigurationsController : ControllerBase
{
    private readonly UnitConfigurationService _service;

    /// <summary>
    /// 依存関係を受け取って初期化
    /// </summary>
    public UnitConfigurationsController(UnitConfigurationService service)
    {
        _service = service;
    }

    /// <summary>
    /// ユニット一覧取得
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UnitConfigurationResponse>>> ListAsync(
        [FromQuery] string agentNumber,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserObjectId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(agentNumber))
        {
            return BadRequest("エージェント番号を指定してください");
        }

        var list = await _service.ListAsync(userId, agentNumber, cancellationToken);
        var response = list.Select(ToResponse).ToList();
        return Ok(response);
    }

    /// <summary>
    /// ユニット追加
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddAsync(
        [FromBody] UnitConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserObjectId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (request is null || string.IsNullOrWhiteSpace(request.AgentNumber))
        {
            return BadRequest("エージェント番号を指定してください");
        }

        var result = await _service.AddAsync(userId, request.AgentNumber, request.ToDraft(), cancellationToken);
        if (!result.Succeeded || result.Unit is null)
        {
            return BadRequest(result.Error ?? "登録に失敗しました");
        }

        var response = ToResponse(result.Unit);
        return CreatedAtAction(nameof(ListAsync), new { agentNumber = request.AgentNumber }, response);
    }

    /// <summary>
    /// ユニット更新
    /// </summary>
    [HttpPut("{unitId:guid}")]
    public async Task<IActionResult> UpdateAsync(
        Guid unitId,
        [FromBody] UnitConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserObjectId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (request is null || string.IsNullOrWhiteSpace(request.AgentNumber))
        {
            return BadRequest("エージェント番号を指定してください");
        }

        var result = await _service.UpdateAsync(userId, request.AgentNumber, unitId, request.ToDraft(), cancellationToken);
        if (!result.Succeeded || result.Unit is null)
        {
            if (string.Equals(result.Error, "ユニット構成が見つかりません", StringComparison.Ordinal))
            {
                return NotFound(result.Error);
            }

            return BadRequest(result.Error ?? "更新に失敗しました");
        }

        var response = ToResponse(result.Unit);
        return Ok(response);
    }

    /// <summary>
    /// ユニット削除
    /// </summary>
    [HttpDelete("{unitId:guid}")]
    public async Task<IActionResult> DeleteAsync(
        Guid unitId,
        [FromQuery] string agentNumber,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserObjectId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(agentNumber))
        {
            return BadRequest("エージェント番号を指定してください");
        }

        var deleted = await _service.DeleteAsync(userId, agentNumber, unitId, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    private static UnitConfigurationResponse ToResponse(UnitConfiguration unit)
    {
        return new UnitConfigurationResponse
        {
            Id = unit.Id,
            Name = unit.Name,
            Description = unit.Description,
            AgentNumber = unit.AgentNumber,
            CreatedAt = unit.CreatedAt,
            Devices = unit.Devices.Select(ToResponse).ToList()
        };
    }

    private static UnitDeviceResponse ToResponse(UnitDevice device)
    {
        return new UnitDeviceResponse
        {
            Id = device.Id,
            Name = device.Name,
            Model = device.Model,
            Maker = device.Maker,
            Description = device.Description
        };
    }
}
