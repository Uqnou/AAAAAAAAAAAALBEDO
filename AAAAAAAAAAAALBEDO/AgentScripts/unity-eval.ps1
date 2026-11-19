param(
    [string]$Code,
    [string]$File,
    [string]$Entry,
    [string]$ArgsJson,
    [switch]$DryRun,
    [switch]$Internals,
    [int]$TimeoutSec = 30,
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$portFile = Join-Path $ProjectPath "Library\Pipeline\.unity-pipeline-port"
if (-not (Test-Path $portFile)) {
    Write-Error "Unity Pipeline nao encontrado em '$portFile'. Abra o Unity Editor com o pacote com.unity.pipeline ativo."
    exit 1
}

$desc = Get-Content -Raw -Encoding UTF8 $portFile | ConvertFrom-Json
$uri = "http://127.0.0.1:$($desc.port)/api/exec"
$headers = @{
    "Authorization" = "Bearer $($desc.evalToken)"
    "Content-Type"  = "application/json; charset=utf-8"
}

if ($Entry) {
    if (-not $File) { Write-Error "Informe -File ao usar -Entry (script run)."; exit 2 }
    $params = @{ file = $File; entry = $Entry; "dry-run" = [bool]$DryRun }
    if ($ArgsJson) { $params.args = $ArgsJson }
    $cmdName = "script run"
} else {
    if (-not $Code -and -not $File) { Write-Error "Informe -Code (C# inline) ou -File (.cs)."; exit 2 }
    $src = if ($Code) { $Code } else { Get-Content -Raw -Encoding UTF8 $File }
    $params = @{ code = $src; "auto-import" = $true; "dry-run" = [bool]$DryRun; internals = [bool]$Internals; timeout = $TimeoutSec }
    $cmdName = "script eval"
}

$payload = @{ command = $cmdName; parameters = $params; timeout = ($TimeoutSec * 1000) } | ConvertTo-Json -Depth 6
try {
    $resp = Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -Body ([System.Text.Encoding]::UTF8.GetBytes($payload))
    $resp | ConvertTo-Json -Depth 10
    if (-not $resp.success) { exit 1 }
} catch {
    $stream = $_.Exception.Response.GetResponseStream()
    if ($stream) {
        $reader = New-Object System.IO.StreamReader($stream)
        Write-Output $reader.ReadToEnd()
    } else {
        Write-Error $_
    }
    exit 1
}
