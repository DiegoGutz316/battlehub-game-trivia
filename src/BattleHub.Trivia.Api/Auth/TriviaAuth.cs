using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace BattleHub.Trivia.Api.Auth;

public static class TriviaAuth
{
    public const string Play = "TriviaPlay";
    public const string Read = "TriviaRead";
    public const string Write = "TriviaWrite";
    public static string UserId(ClaimsPrincipal user) => user.FindFirst("sub")?.Value ?? "";
    public static bool IsMachine(ClaimsPrincipal user) => user.FindFirst("gty")?.Value == "client-credentials" || UserId(user).EndsWith("@clients", StringComparison.Ordinal);
    public static bool HasPermission(ClaimsPrincipal user, string permission) => user.FindAll("permissions").Any(c => c.Value == permission)
        || user.FindFirst("scope")?.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(permission) == true;
    public static bool IsWriter(ClaimsPrincipal user) => IsMachine(user) && HasPermission(user, "games.trivia.results.write");

    public static IServiceCollection AddTriviaAuth(
    this IServiceCollection services,
    IConfiguration config)
{
    var issuer = config["Auth:Issuer"];
    var audience = config["Auth:Audience"];
    var publicKey = config["Auth:PublicKey"];

    if (string.IsNullOrWhiteSpace(issuer) ||
        string.IsNullOrWhiteSpace(audience) ||
        string.IsNullOrWhiteSpace(publicKey))
    {
        throw new InvalidOperationException(
            "Configurar Auth:Issuer, Auth:Audience y Auth:PublicKey.");
    }

    RSA rsa = RSA.Create();

    try
    {
        rsa.ImportSubjectPublicKeyInfo(
            Convert.FromBase64String(publicKey),
            out _);

        var signingKey = new RsaSecurityKey(rsa.ExportParameters(false));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.MapInboundClaims = false;

                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,

                    ValidateAudience = true,
                    ValidAudience = audience,

                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,

                    IssuerSigningKey = signingKey,

                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],

                    NameClaimType = "name",
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                jwt.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        if (ctx.Request.Path.StartsWithSegments("/hubs/trivia"))
                        {
                            ctx.Token = ctx.Request.Query["access_token"];
                        }

                        return Task.CompletedTask;
                    }
                };
            });
    }
    finally
    {
        rsa.Dispose();
    }

    services.AddAuthorization(options =>
    {
        options.AddPolicy(Play, p =>
            p.RequireAuthenticatedUser()
             .RequireAssertion(c =>
                 !IsMachine(c.User) &&
                 UserId(c.User).Length > 0));

        options.AddPolicy(Read, p =>
            p.RequireAuthenticatedUser()
             .RequireAssertion(c =>
                 (!IsMachine(c.User) &&
                  UserId(c.User).Length > 0) ||
                 IsWriter(c.User)));

        options.AddPolicy(Write, p =>
            p.RequireAuthenticatedUser()
             .RequireAssertion(c => IsWriter(c.User)));
    });

    return services;
}
}
