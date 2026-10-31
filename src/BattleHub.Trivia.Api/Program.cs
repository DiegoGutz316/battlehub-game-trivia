
using BattleHub.Trivia.Data;
using BattleHub.Trivia.Domain.Services;
using BattleHub.Trivia.Api.Auth;
using BattleHub.Trivia.Api.Hubs;
using BattleHub.Trivia.Api.Profiles;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

builder.Services.AddTriviaData(builder.Configuration);
builder.Services.AddTriviaAuth(builder.Configuration);

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = false;
    options.MaximumReceiveMessageSize = 32768;
});

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(
            builder.Configuration
                .GetSection("Cors:Origins")
                .Get<string[]>()
            ?? ["http://localhost:4000", "http://localhost:4002"])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<TriviaScoringService>();
builder.Services.AddSingleton<TriviaGameService>();
builder.Services.AddSingleton<TriviaSessions>();
builder.Services.AddSingleton<PracticeSessions>();
builder.Services.AddHostedService<PracticeWorker>();
builder.Services.AddHttpClient(
    "Profile",
    client => client.Timeout = TimeSpan.FromSeconds(5))
    .ConfigurePrimaryHttpMessageHandler(() =>
        new HttpClientHandler
        {
            AllowAutoRedirect = false
        });

builder.Services.AddSingleton<IProfileGateway>(services =>
    new ProfileGateway(
        services.GetRequiredService<IHttpClientFactory>()
            .CreateClient("Profile"),
        builder.Configuration));

builder.Services.AddHostedService<TriviaWorker>();

var app = builder.Build();

// Las migraciones y la generación de datos iniciales NO se ejecutan aquí.
// En Docker Compose lo hacen los servicios one-shot db-migrate y db-seed
// (proyecto BattleHub.Trivia.DbInit). En local: ver README, sección Docker Compose.

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHub<TriviaHub>(
    "/hubs/trivia",
    options => options.CloseOnAuthenticationExpiration = true);

app.MapHub<PracticeHub>("/hubs/trivia/practice")
    .AllowAnonymous();

app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

public partial class Program;
