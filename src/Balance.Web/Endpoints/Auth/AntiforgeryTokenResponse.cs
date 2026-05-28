namespace Balance.Web.Endpoints.Auth;

/// <summary>
/// Carries the antiforgery <em>request token</em> the SPA must echo as
/// <c>X-XSRF-TOKEN</c> on every state-changing request. The matching
/// <em>cookie token</em> rides along automatically as a Set-Cookie side effect
/// of <c>/api/antiforgery/token</c>.
/// </summary>
internal sealed record AntiforgeryTokenResponse(string Token);
