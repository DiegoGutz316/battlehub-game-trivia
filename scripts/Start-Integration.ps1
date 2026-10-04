[CmdletBinding()]
param(
    [string]$ConnectionString,
    [switch]$NoSeed
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$previousConnection = $env:ConnectionStrings__Trivia
$previousMigration = $env:Database__MigrateOnStartup
$previousSeed = $env:Database__SeedQuestions
try {
    if ($ConnectionString) { $env:ConnectionStrings__Trivia = $ConnectionString }
    $env:Database__MigrateOnStartup = 'true'
    $env:Database__SeedQuestions = (-not $NoSeed).ToString()
    Write-Host 'Trivia API: http://localhost:5185. SQL Server requerido; no se usa MySQL.'
    dotnet run --project (Join-Path $repoRoot 'src/BattleHub.Trivia.Api') --launch-profile http
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo iniciar Trivia. Revisa la conexión SQL y los errores anteriores.' }
} finally {
    $env:ConnectionStrings__Trivia = $previousConnection
    $env:Database__MigrateOnStartup = $previousMigration
    $env:Database__SeedQuestions = $previousSeed
}
