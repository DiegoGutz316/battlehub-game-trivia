using System.Net;
using System.Net.Http.Json;
using BattleHub.Trivia.Api.Profiles;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;

namespace BattleHub.Trivia.UnitTests;

public sealed class ProfileGatewayTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(respond(request));
        }
    }

    private static IConfiguration Config(string baseUrl = "http://localhost:5220/") => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Profile:BaseUrl"] = baseUrl })
        .Build();

    private static HttpResponseMessage Permissions(params string[] permissions) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(new { permissions }) };

    [Fact]
    public async Task SendsPlayerTokenAndAcceptsTriviaPermission()
    {
        var handler = new Handler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("http://localhost:5220/api/profiles/me/permissions", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("shell-token", request.Headers.Authorization.Parameter);
            return Permissions("matches.create", ProfileGateway.PlayPermission);
        });
        using var http = new HttpClient(handler);

        Assert.True(await new ProfileGateway(http, Config()).CanPlayAsync("shell-token", default));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task RejectsPlayerWithoutTriviaPermission()
    {
        using var http = new HttpClient(new Handler(_ => Permissions("games.typing.play", "games.memory.play")));

        Assert.False(await new ProfileGateway(http, Config()).CanPlayAsync("shell-token", default));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task RejectsTokenOrProfileThatProfileDoesNotAccept(HttpStatusCode status)
    {
        using var http = new HttpClient(new Handler(_ => new HttpResponseMessage(status)));

        Assert.False(await new ProfileGateway(http, Config()).CanPlayAsync("shell-token", default));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RejectsMissingTokenWithoutCallingProfile(string token)
    {
        var handler = new Handler(_ => Permissions(ProfileGateway.PlayPermission));
        using var http = new HttpClient(handler);

        Assert.False(await new ProfileGateway(http, Config()).CanPlayAsync(token, default));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task ReportsProfileUnavailableOnServerError()
    {
        using var http = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var error = await Assert.ThrowsAsync<HubException>(() => new ProfileGateway(http, Config()).CanPlayAsync("shell-token", default));
        Assert.Equal("PROFILE_UNAVAILABLE", error.Message);
    }

    [Fact]
    public async Task ReportsProfileUnavailableWhenUnreachableOrInvalid()
    {
        using var unreachable = new HttpClient(new Handler(_ => throw new HttpRequestException("sin conexión")));
        using var invalid = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("no es json") }));

        Assert.Equal("PROFILE_UNAVAILABLE", (await Assert.ThrowsAsync<HubException>(() => new ProfileGateway(unreachable, Config()).CanPlayAsync("shell-token", default))).Message);
        Assert.Equal("PROFILE_UNAVAILABLE", (await Assert.ThrowsAsync<HubException>(() => new ProfileGateway(invalid, Config()).CanPlayAsync("shell-token", default))).Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ftp://localhost:5220")]
    [InlineData("http://usuario@localhost:5220")]
    public async Task RequiresValidBaseUrl(string baseUrl)
    {
        using var http = new HttpClient(new Handler(_ => Permissions(ProfileGateway.PlayPermission)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ProfileGateway(http, Config(baseUrl)).CanPlayAsync("shell-token", default));
    }
}
