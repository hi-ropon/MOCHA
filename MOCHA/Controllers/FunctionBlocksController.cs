using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MOCHA.Models.Architecture;
using MOCHA.Models.Auth;
using MOCHA.Services.Architecture;

namespace MOCHA.Controllers;

/// <summary>
/// ファンクションブロック管理用 API
/// </summary>
[ApiController]
[Route("api/plc-units/{plcUnitId:guid}/function-blocks")]
public sealed class FunctionBlocksController : ControllerBase
{
    private readonly FunctionBlockService _service;

    /// <summary>
    /// 依存関係を受け取って初期化
    /// </summary>
    public FunctionBlocksController(FunctionBlockService service)
    {
        _service = service;
    }

    /// <summary>
    /// ファンクションブロック一覧取得
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> ListAsync(
        Guid plcUnitId,
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

        var blocks = await _service.ListAsync(userId, agentNumber, plcUnitId, cancellationToken);
        var response = blocks.Select(b => new
        {
            b.Id,
            b.Name,
            b.SafeName,
            b.CreatedAt,
            b.UpdatedAt,
            Label = b.LabelFile is null ? null : new
            {
                b.LabelFile.FileName,
                b.LabelFile.RelativePath
            },
            Program = b.ProgramFile is null ? null : new
            {
                b.ProgramFile.FileName,
                b.ProgramFile.RelativePath
            }
        });

        return Ok(response);
    }

    /// <summary>
    /// ファンクションブロック登録
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> UploadAsync(
        Guid plcUnitId,
        [FromForm] FunctionBlockUploadRequest request,
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

        if (request.LabelFile is null || request.ProgramFile is null)
        {
            return BadRequest("ラベルCSVとプログラムCSVを選択してください");
        }

        var draft = new FunctionBlockDraft
        {
            Name = request.Name,
            LabelFile = await ToUploadAsync(request.LabelFile, cancellationToken),
            ProgramFile = await ToUploadAsync(request.ProgramFile, cancellationToken)
        };

        var result = await _service.AddAsync(userId, request.AgentNumber, plcUnitId, draft, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return BadRequest(result.Error ?? "登録に失敗しました");
        }

        var fb = result.Value;
        var response = new
        {
            fb.Id,
            fb.Name,
            fb.SafeName,
            fb.CreatedAt,
            fb.UpdatedAt
        };

        return CreatedAtAction(nameof(ListAsync), new { plcUnitId, agentNumber = request.AgentNumber }, response);
    }

    /// <summary>
    /// ファンクションブロック削除
    /// </summary>
    [HttpDelete("{functionBlockId:guid}")]
    public async Task<IActionResult> DeleteAsync(
        Guid plcUnitId,
        Guid functionBlockId,
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

        var deleted = await _service.DeleteAsync(userId, agentNumber, plcUnitId, functionBlockId, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    private static async Task<PlcFileUpload> ToUploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, cancellationToken);
        return new PlcFileUpload
        {
            FileName = file.FileName,
            ContentType = ResolveContentType(file),
            FileSize = file.Length,
            Content = ms.ToArray()
        };
    }

    private static string ResolveContentType(IFormFile file)
    {
        if (file is FormFile formFile && formFile.Headers is null)
        {
            formFile.Headers = new HeaderDictionary();
        }

        var contentType = file.ContentType;
        return string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
    }
}
