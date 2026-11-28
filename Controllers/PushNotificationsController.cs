using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stockify.Common.Constants;
using Stockify.Services.Interfaces;

namespace Stockify.Controllers;

[Authorize(Roles = Roles.Admin)]
[Route("api/push")]
[ApiController]
public class PushNotificationsController : ControllerBase
{
    private readonly IPushNotificationService _pushService;

    public PushNotificationsController(IPushNotificationService pushService)
    {
        _pushService = pushService;
    }

    [HttpGet("vapid-public-key")]
    [AllowAnonymous]
    public async Task<IActionResult> GetVapidPublicKey(CancellationToken ct)
    {
        var key = await _pushService.GetPublicVapidKeyAsync(ct);
        return Ok(new { publicKey = key });
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var count = await _pushService.GetActiveSubscriptionCountAsync(ct);
        var diag = Stockify.Services.Implementations.WebPushNotificationService.GetDiagnostics();
        return Ok(new
        {
            activeSubscriptions = count,
            lastPushError = diag.LastError,
            lastPushSuccessAt = diag.LastSuccessAt
        });
    }

    [HttpPost("subscribe")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscribeRequest request, CancellationToken ct)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Endpoint) ||
            string.IsNullOrWhiteSpace(request.P256dh) || string.IsNullOrWhiteSpace(request.Auth))
        {
            return BadRequest(new { success = false, message = "بيانات الاشتراك غير مكتملة." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userAgent = Request.Headers.UserAgent.ToString();

        await _pushService.SubscribeAsync(request.Endpoint, request.P256dh, request.Auth, userId, userAgent, ct);
        var count = await _pushService.GetActiveSubscriptionCountAsync(ct);
        return Ok(new { success = true, activeSubscriptions = count });
    }

    [HttpPost("unsubscribe")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unsubscribe([FromBody] PushUnsubscribeRequest request, CancellationToken ct)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Endpoint))
        {
            return BadRequest(new { success = false });
        }

        await _pushService.UnsubscribeAsync(request.Endpoint, ct);
        var count = await _pushService.GetActiveSubscriptionCountAsync(ct);
        return Ok(new { success = true, activeSubscriptions = count });
    }

    [HttpPost("test")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTest(CancellationToken ct)
    {
        var sentCount = await _pushService.SendToAllAsync(
            "ROVEN — اختبار الإشعارات",
            "تم تفعيل إشعارات الآيفون بنجاح! ستصلك تنبيهات الطلبات والمخزون والسلات المتروكة فوراً.",
            "/Dashboard",
            "roven-test",
            ct);

        var diag = Stockify.Services.Implementations.WebPushNotificationService.GetDiagnostics();
        return Ok(new { success = true, sentCount, lastPushError = diag.LastError });
    }

    public class PushSubscribeRequest
    {
        public string Endpoint { get; set; } = string.Empty;
        public string P256dh { get; set; } = string.Empty;
        public string Auth { get; set; } = string.Empty;
    }

    public class PushUnsubscribeRequest
    {
        public string Endpoint { get; set; } = string.Empty;
    }
}
