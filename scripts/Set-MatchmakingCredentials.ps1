[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

$secureValue = Read-Host 'Token privado del servicio Matchmaking' -AsSecureString
$pointer = [IntPtr]::Zero
$privateValue = $null
$privateJson = $null

try {
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureValue)
    $privateValue = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)

    if ([string]::IsNullOrWhiteSpace($privateValue)) {
        throw 'El token de Matchmaking es obligatorio.'
    }

    $privateJson = @{
        'Matchmaking:ServiceToken' = $privateValue
    } | ConvertTo-Json -Compress

    $privateJson | dotnet user-secrets set --project (Join-Path $repoRoot 'src/BattleHub.Trivia.Api')

    if ($LASTEXITCODE -ne 0) {
        throw 'No se pudo guardar el token de Matchmaking.'
    }

    Write-Host 'Token guardado fuera del repositorio. Reinicia Trivia.'
}
finally {
    if ($pointer -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }

    $privateJson = $null
    $privateValue = $null

    if ($null -ne $secureValue) {
        $secureValue.Dispose()
    }
}