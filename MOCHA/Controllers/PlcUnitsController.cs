using Microsoft.AspNetCore.Mvc;
using MOCHA.Models.Architecture;
using MOCHA.Models.Auth;
using MOCHA.Services.Architecture;

namespace MOCHA.Controllers;

/// <summary>
/// PLCユニット一覧を提供するAPI
/// </summary>
[ApiController]
[Route("api/plc-units")]
public sealed class PlcUnitsController : ControllerBase
{
    private readonly IPlcUnitRepository _repository;

    /// <summary>
    /// 依存関係を受け取って初期化
    /// </summary>
    public PlcUnitsController(IPlcUnitRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// ユーザーとエージェントでユニット一覧を取得
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlcUnitSummary>>> ListAsync([FromQuery] string agentNumber, CancellationToken cancellationToken = default)
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

        var normalizedAgent = agentNumber.Trim();
        var list = await _repository.ListAsync(normalizedAgent, cancellationToken);
        var result = list.Select(u => new PlcUnitSummary
        {
            Id = u.Id,
            Name = u.Name,
            AgentNumber = u.AgentNumber,
            Model = u.Model,
            Role = u.Role
        }).ToList();

        return Ok(result);
    }
}
