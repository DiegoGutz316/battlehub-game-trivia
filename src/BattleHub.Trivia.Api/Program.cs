using BattleHub.Trivia.Data;
using BattleHub.Trivia.Data.Context;
using BattleHub.Trivia.Domain.Services;
using BattleHub.Trivia.Api.Auth;
using BattleHub.Trivia.Api.Hubs;
using BattleHub.Trivia.Api.Matchmaking;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.AddTriviaData(builder.Configuration);
builder.Services.AddTriviaAuth(builder.Configuration);
builder.Services.AddSignalR(options => { options.EnableDetailedErrors = false; options.MaximumReceiveMessageSize = 32768; });
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4000", "http://localhost:4002"])
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<TriviaScoringService>();
builder.Services.AddSingleton<TriviaGameService>();
builder.Services.AddSingleton<TriviaSessions>();
builder.Services.AddHttpClient("Matchmaking", client => client.Timeout = TimeSpan.FromSeconds(5))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<IMatchmakingGateway>(services => new MatchmakingGateway(
    services.GetRequiredService<IHttpClientFactory>().CreateClient("Matchmaking"), builder.Configuration,
    services.GetRequiredService<TimeProvider>(), services.GetRequiredService<ILogger<MatchmakingGateway>>()));
builder.Services.AddHostedService<TriviaWorker>();
builder.Services.AddHostedService<FinishWorker>();
var app = builder.Build();
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TriviaDbContext>();
    await db.Database.MigrateAsync();
    if (app.Configuration.GetValue<bool>("Database:SeedQuestions") && !await db.Questions.AnyAsync())
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("QuestionBank.sql")!;
        using var reader = new StreamReader(stream);
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync(await reader.ReadToEndAsync());
        await transaction.CommitAsync();
    }
}
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<TriviaHub>("/hubs/trivia", options => options.CloseOnAuthenticationExpiration = true);
app.MapHealthChecks("/health").AllowAnonymous();
app.Run();
public partial class Program;
