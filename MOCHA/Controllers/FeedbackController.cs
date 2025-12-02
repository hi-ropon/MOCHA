using System;
using Microsoft.AspNetCore.Mvc;
using MOCHA.Models.Auth;
using MOCHA.Models.Feedback;
using MOCHA.Services.Feedback;

namespace MOCHA.Controllers;

/// <summary>
/// 回答フィードバックを受け付ける API コントローラー
/// </summary>
[ApiController]
[Route("api/feedback")]
public sealed class FeedbackController : ControllerBase
{
    private readonly IFeedbackService _feedbackService;

    /// <summary>
    /// フィードバックサービス注入による初期化
    /// </summary>
    /// <param name="feedbackService">フィードバックサービス</param>
    public FeedbackController(IFeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }

    /// <summary>
    /// フィードバック登録
    /// </summary>
    /// <param name="request">登録リクエスト</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>登録されたレコード</returns>
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] FeedbackRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserObjectId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var entry = await _feedbackService.SubmitAsync(
                userId,
                request.ConversationId,
                request.MessageIndex,
                request.Rating,
                request.Comment,
                cancellationToken);
            return Ok(entry);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// 会話単位のフィードバック集計取得
    /// </summary>
    /// <param name="conversationId">会話ID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>集計</returns>
    [HttpGet("summary/{conversationId}")]
    public async Task<ActionResult<FeedbackSummary>> GetSummary(string conversationId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserObjectId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var summary = await _feedbackService.GetSummaryAsync(userId, conversationId, cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    /// 直近の Bad 一覧取得
    /// </summary>
    /// <param name="take">取得件数</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>Bad フィードバック一覧</returns>
    [HttpGet("recent-bad")]
    public async Task<ActionResult<IReadOnlyList<FeedbackEntry>>> GetRecentBad([FromQuery] int take = 10, CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserObjectId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var normalizedTake = Math.Clamp(take, 1, 50);
        var list = await _feedbackService.GetRecentBadAsync(userId, normalizedTake, cancellationToken);
        return Ok(list);
    }

    /// <summary>
    /// 会話内で既に付与された評価取得
    /// </summary>
    /// <param name="conversationId">会話ID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>メッセージインデックスと評価のマップ</returns>
    [HttpGet("ratings/{conversationId}")]
    public async Task<ActionResult<IReadOnlyDictionary<int, FeedbackRating>>> GetRatings(string conversationId, CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserObjectId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var ratings = await _feedbackService.GetRatingsAsync(userId, conversationId, cancellationToken);
        return Ok(ratings);
    }
}
