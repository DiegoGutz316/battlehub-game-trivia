[CmdletBinding()]
param([string]$ClientId)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $ClientId) { $ClientId = Read-Host 'Client ID de BattleHub Trivia Service (M2M)' }
if ([string]::IsNullOrWhiteSpace($ClientId)) { throw 'Client ID obligatorio.' }
$secureValue = Read-Host 'Client Secret privado de Trivia Service' -AsSecureString
$pointer = [IntPtr]::Zero
$privateJson = $null
try {
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureValue)
    $privateValue = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    if ([string]::IsNullOrWhiteSpace($privateValue)) { throw 'Client Secret obligatorio.' }
    $privateJson = @{
        'Matchmaking:Auth0:ClientId' = $ClientId
        'Matchmaking:Auth0:ClientSecret' = $privateValue
    } | ConvertTo-Json -Compress
    $privateJson | dotnet user-secrets set --project (Join-Path $repoRoot 'src/BattleHub.Trivia.Api')
    if ($LASTEXITCODE -ne 0) { throw 'No se pudieron guardar las credenciales locales.' }
    Write-Host 'Credenciales guardadas fuera del repo. Reinicia Trivia.'
} finally {
    if ($pointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
    $privateJson = $null
    $privateValue = $null
    $secureValue.Dispose()
}
