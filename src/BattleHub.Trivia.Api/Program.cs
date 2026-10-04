using BattleHub.Trivia.Data;
using BattleHub.Trivia.Domain.Interfaces;
using BattleHub.Trivia.Domain.Repositories;
using BattleHub.Trivia.Domain.Services;
using BattleHub.Trivia.Api.Hubs;
using BattleHub.Trivia.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IGameRepository, InMemoryGameRepository>();
builder.Services.AddSingleton<TriviaScoringService>();
builder.Services.AddSingleton<TriviaGameService>();
builder.Services.AddScoped<TriviaMatchBootstrapService>();
builder.Services.AddSingleton<TriviaMatchRunner>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddTriviaData(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHub<TriviaHub>("/hubs/trivia");
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
