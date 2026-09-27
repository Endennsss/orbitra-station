$ErrorActionPreference = 'Stop'
$archiveRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$repoRoot = [IO.Path]::GetFullPath((Join-Path $archiveRoot '../../..'))
$manifest = Get-Content -LiteralPath (Join-Path $archiveRoot 'manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in $manifest) {
    $archived = [IO.Path]::GetFullPath((Join-Path $archiveRoot $entry.path))
    $active = [IO.Path]::GetFullPath((Join-Path $repoRoot $entry.path))
    if (!$archived.StartsWith($archiveRoot + [IO.Path]::DirectorySeparatorChar) -or
        !$active.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar)) {
        throw "Invalid archive path: $($entry.path)"
    }
    if (Test-Path -LiteralPath $active) { throw "Archived file remains active: $($entry.path)" }
    if (!(Test-Path -LiteralPath $archived -PathType Leaf)) { throw "Missing archived file: $($entry.path)" }
    if ((Get-Item -LiteralPath $archived).Length -ne $entry.bytes -or
        (Get-FileHash -LiteralPath $archived -Algorithm SHA256).Hash -ne $entry.sha256) {
        throw "Archived file changed: $($entry.path)"
    }
}
$hooks = @(
    @{ path = 'Content.Server/Chat/Systems/ChatSystem.cs'; token = 'TryHandleOrbitraRatvarChat(' },
    @{ path = 'Content.Server/Administration/Systems/AdminVerbSystem.Antags.cs'; token = 'AddOrbitraRatvarVerb(' },
    @{ path = 'Content.Server/StationEvents/Events/AnomalySpawnRule.cs'; token = 'OrbitraRatvarEventTargetComponent' },
    @{ path = 'Content.Server/StationEvents/Events/PowerGridCheckRule.cs'; token = 'OrbitraRatvarEventTargetComponent' }
)
foreach ($hook in $hooks) {
    if ((Get-Content -LiteralPath (Join-Path $repoRoot $hook.path) -Raw).Contains($hook.token)) {
        throw "Cult hook still present: $($hook.path)"
    }
}
Write-Output "Archive verified: $($manifest.Count) files unchanged; active paths and hooks absent."
