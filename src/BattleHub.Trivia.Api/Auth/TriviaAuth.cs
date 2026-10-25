using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

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

    // Token con el que se autenticó la petición o la conexión al hub (el navegador lo envía en access_token).
    public static string AccessToken(HttpContext? http)
    {
        if (http is null) return "";
        var header = http.Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return header["Bearer ".Length..].Trim();
        return http.Request.Path.StartsWithSegments("/hubs/trivia") ? http.Request.Query["access_token"].ToString() : "";
    }

    public static IServiceCollection AddTriviaAuth(
    this IServiceCollection services,
    IConfiguration config)
{
    var issuer = config["Auth:Issuer"];
    var audience = config["Auth:Audience"];

    if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri) ||
        issuerUri.Scheme != Uri.UriSchemeHttps ||
        string.IsNullOrWhiteSpace(audience))
    {
        throw new InvalidOperationException(
            "Configurar Auth:Issuer (URL https del emisor de los tokens del Shell) y Auth:Audience.");
    }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                // Las claves de firma se descargan del emisor (OpenID Connect discovery)
                // y se vuelven a descargar cuando el emisor las rota.
                jwt.Authority = issuer;
                jwt.MapInboundClaims = false;

                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,

                    ValidateAudience = true,
                    ValidAudience = audience,

                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,

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
