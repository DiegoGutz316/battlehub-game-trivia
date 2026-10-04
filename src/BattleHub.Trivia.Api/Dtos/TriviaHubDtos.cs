namespace BattleHub.Trivia.Api.Dtos;

public sealed record JoinMatchRequest(
    string MatchId);

public sealed record SubmitAnswerRequest(
    string MatchId,
    string QuestionId,
    string AnswerId);

public sealed record GameErrorEvent(
    string Code,
    string Message);

public sealed record PlayerDto(
    string UserId,
    string DisplayName,
    int Score,
    bool IsConnected);

public sealed record CurrentStateEvent(
    string MatchId,
    string Phase,
    DateTimeOffset ServerTimeUtc,
    int TotalQuestions,
    IReadOnlyList<PlayerDto> Players,
    object? CurrentQuestion,
    object? LastQuestionResult);