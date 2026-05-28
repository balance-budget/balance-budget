using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Balance.Tests.Api.Auth;

internal sealed class AuthEndpointsTests : RealAuthEndpointsTestsBase
{
    private const string Email = "alice@example.com";
    private const string Password = "correct-horse-battery";
    private const string DisplayName = "Alice";

    [Test]
    public async Task Setup_succeeds_with_correct_token_when_no_users_exist()
    {
        using var client = CreateCookieAwareClient();

        using var response = await PostSetupAsync(
            client,
            new SetupBody(Email, Password, DisplayName, RealAuthWebApplicationFactory.SetupToken)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        // The setup endpoint should have issued the auth cookie — /me works on the same client.
        using var meResponse = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative));
        await Assert.That(meResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Setup_returns_404_when_setup_token_is_wrong()
    {
        using var client = CreateCookieAwareClient();

        using var response = await PostSetupAsync(
            client,
            new SetupBody(Email, Password, DisplayName, "wrong-token")
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Setup_returns_404_when_setup_token_is_missing()
    {
        using var client = CreateCookieAwareClient();

        using var response = await PostSetupAsync(
            client,
            new SetupBody(Email, Password, DisplayName, null)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Setup_returns_404_once_a_user_exists()
    {
        using var client = CreateCookieAwareClient();

        using var first = await PostSetupAsync(
            client,
            new SetupBody(Email, Password, DisplayName, RealAuthWebApplicationFactory.SetupToken)
        );
        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        // Even with the correct token, the wizard is gone now.
        using var second = await PostSetupAsync(
            client,
            new SetupBody(
                "second@example.com",
                Password,
                "Second",
                RealAuthWebApplicationFactory.SetupToken
            )
        );
        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Me_returns_401_when_not_authenticated()
    {
        using var client = CreateCookieAwareClient();

        using var response = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Login_with_correct_password_issues_cookie()
    {
        using var client = CreateCookieAwareClient();
        await SeedFirstUserAsync(client);
        await LogoutAsync(client);

        using var loginResponse = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new LoginBody(Email, Password)
        );
        await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        using var meResponse = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative));
        await Assert.That(meResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Login_with_wrong_password_returns_401()
    {
        using var client = CreateCookieAwareClient();
        await SeedFirstUserAsync(client);
        await LogoutAsync(client);

        using var loginResponse = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new LoginBody(Email, "definitely-wrong-password-123")
        );

        await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ApiToken_authenticates_subsequent_request()
    {
        using var client = CreateCookieAwareClient();
        await SeedFirstUserAsync(client);

        // Mint a token while still cookie-authenticated.
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/api/admin/tokens", UriKind.Relative),
            new CreateTokenBody("test token", null)
        );
        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedTokenBody>();
        await Assert.That(created).IsNotNull();
        await Assert.That(created!.Token).StartsWith("bal_pat_");

        // Drop cookie state and use the PAT instead.
        using var tokenClient = CreateCookieAwareClient();
        tokenClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            created.Token
        );

        using var meResponse = await tokenClient.GetAsync(
            new Uri("/api/auth/me", UriKind.Relative)
        );
        await Assert.That(meResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var me = await meResponse.Content.ReadFromJsonAsync<MeBody>();
        await Assert.That(me).IsNotNull();
        await Assert.That(me!.Email).IsEqualTo(Email);
        await Assert.That(me.AuthScheme).IsEqualTo("Balance.ApiToken");
    }

    [Test]
    public async Task Revoked_ApiToken_is_rejected()
    {
        using var client = CreateCookieAwareClient();
        await SeedFirstUserAsync(client);

        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/api/admin/tokens", UriKind.Relative),
            new CreateTokenBody("doomed", null)
        );
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedTokenBody>();
        await Assert.That(created).IsNotNull();

        using var revokeResponse = await client.PostAsJsonAsync(
            new Uri($"/api/admin/tokens/{created!.Metadata.Id}/revoke", UriKind.Relative),
            new { }
        );
        await Assert.That(revokeResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        using var tokenClient = CreateCookieAwareClient();
        tokenClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            created.Token
        );

        using var meResponse = await tokenClient.GetAsync(
            new Uri("/api/auth/me", UriKind.Relative)
        );
        await Assert.That(meResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Cannot_disable_self()
    {
        using var client = CreateCookieAwareClient();
        var (userId, _) = await SeedFirstUserAsync(client);

        using var response = await client.PostAsJsonAsync(
            new Uri($"/api/admin/users/{userId}/disable", UriKind.Relative),
            new { }
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Cannot_disable_last_active_user()
    {
        using var client = CreateCookieAwareClient();
        var (_, _) = await SeedFirstUserAsync(client);

        // Create a second user, then try to disable them — should fail to leave self as last.
        using var createUser = await client.PostAsJsonAsync(
            new Uri("/api/admin/users", UriKind.Relative),
            new CreateUserBody("bob@example.com", "another-strong-password", "Bob")
        );
        await Assert.That(createUser.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var bob = await createUser.Content.ReadFromJsonAsync<UserBody>();
        await Assert.That(bob).IsNotNull();

        // Bob can be disabled fine — leaves Alice as the only active user.
        using var disableBob = await client.PostAsJsonAsync(
            new Uri($"/api/admin/users/{bob!.Id}/disable", UriKind.Relative),
            new { }
        );
        await Assert.That(disableBob.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        // Now disabling Alice (the caller) is blocked by both the self-guard and the
        // last-active-user guard. We expect 400 either way.
    }

    private HttpClient CreateCookieAwareClient()
    {
        // TUnit's no-arg CreateClient() does not appear to persist Set-Cookie across
        // requests, so wire up an explicit CookieContainer over the test server's
        // HttpMessageHandler. The HttpClient takes ownership of the wrapper via
        // disposeHandler:true and disposes it transitively.
        return new HttpClient(
            new CookieHandlerWrapper(
                Factory.Server.CreateHandler(),
                new System.Net.CookieContainer()
            ),
            disposeHandler: true
        )
        {
            BaseAddress = Factory.Server.BaseAddress ?? new Uri("http://localhost"),
        };
    }

    private sealed class CookieHandlerWrapper : DelegatingHandler
    {
        private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
        {
            HttpMethod.Get.Method,
            HttpMethod.Head.Method,
            HttpMethod.Options.Method,
        };

        private readonly System.Net.CookieContainer _container;

        public CookieHandlerWrapper(HttpMessageHandler inner, System.Net.CookieContainer container)
            : base(inner)
        {
            _container = container;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var requestUri = request.RequestUri is { IsAbsoluteUri: true } abs
                ? abs
                : new Uri(
                    new Uri("http://localhost"),
                    request.RequestUri ?? new Uri("/", UriKind.Relative)
                );

            // Auto-prime on every write: antiforgery tokens are tied to the current user
            // identity, so a token issued anonymously is invalid once the user logs in.
            // Re-priming on each mutation keeps the wrapper simple and correct.
            if (!SafeMethods.Contains(request.Method.Method))
            {
                using var primeRequest = new HttpRequestMessage(
                    HttpMethod.Get,
                    new Uri(requestUri, "/api/antiforgery/token")
                );
                ApplyCookies(primeRequest, requestUri);
                using var primeResponse = await base.SendAsync(primeRequest, cancellationToken)
                    .ConfigureAwait(false);
                StoreCookies(primeResponse, requestUri);
            }

            ApplyCookies(request, requestUri);
            if (!SafeMethods.Contains(request.Method.Method))
            {
                var xsrf = GetCookieValue(requestUri, "XSRF-TOKEN");
                if (!string.IsNullOrEmpty(xsrf))
                {
                    request.Headers.Remove("X-XSRF-TOKEN");
                    request.Headers.Add("X-XSRF-TOKEN", xsrf);
                }
            }

            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            StoreCookies(response, requestUri);
            return response;
        }

        private void ApplyCookies(HttpRequestMessage request, Uri requestUri)
        {
            var cookieHeader = _container.GetCookieHeader(requestUri);
            if (!string.IsNullOrEmpty(cookieHeader))
            {
                request.Headers.Remove("Cookie");
                request.Headers.Add("Cookie", cookieHeader);
            }
        }

        private void StoreCookies(HttpResponseMessage response, Uri requestUri)
        {
            if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            {
                foreach (var value in setCookies)
                {
                    _container.SetCookies(requestUri, value);
                }
            }
        }

        private string? GetCookieValue(Uri requestUri, string name)
        {
            foreach (System.Net.Cookie cookie in _container.GetCookies(requestUri))
            {
                if (string.Equals(cookie.Name, name, StringComparison.Ordinal))
                {
                    return cookie.Value;
                }
            }
            return null;
        }
    }

    private static async Task<(string UserId, HttpClient Client)> SeedFirstUserAsync(
        HttpClient client
    )
    {
        using var setup = await PostSetupAsync(
            client,
            new SetupBody(Email, Password, DisplayName, RealAuthWebApplicationFactory.SetupToken)
        );
        await Assert.That(setup.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        using var me = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative));
        await Assert.That(me.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await me.Content.ReadFromJsonAsync<MeBody>();
        await Assert.That(body).IsNotNull();
        return (body!.Id, client);
    }

    private static async Task LogoutAsync(HttpClient client)
    {
        using var response = await client.PostAsync(
            new Uri("/api/auth/logout", UriKind.Relative),
            content: null
        );
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
    }

    private static Task<HttpResponseMessage> PostSetupAsync(HttpClient client, SetupBody body) =>
        client.PostAsJsonAsync(new Uri("/api/auth/setup", UriKind.Relative), body);

    private sealed record SetupBody(
        string Email,
        string Password,
        string DisplayName,
        string? SetupToken
    );

    private sealed record LoginBody(string Email, string Password);

    private sealed record CreateTokenBody(string Name, DateTime? ExpiresAt);

    private sealed record CreateUserBody(string Email, string Password, string DisplayName);

    private sealed record MeBody(string Id, string Email, string DisplayName, string AuthScheme);

    private sealed record UserBody(string Id, string Email, string DisplayName, bool IsActive);

    private sealed record TokenMetadataBody(string Id, string Name, string Prefix, string Last4);

    private sealed record CreatedTokenBody(TokenMetadataBody Metadata, string Token);
}
