param([switch]$UseBuiltReader, [string]$ReaderDirectory = 'dist', [string]$ReleaseDirectory = 'releases')

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
if (!$UseBuiltReader) {
    if ($ReaderDirectory -ne 'dist') { throw 'Use -UseBuiltReader with a custom -ReaderDirectory.' }
    & .\scripts\build-local-reader.ps1
}
$reader = Join-Path $ReaderDirectory 'companion-reader.exe'
$notices = Join-Path $ReaderDirectory 'local-reader-licenses'
if (!(Test-Path -LiteralPath $reader -PathType Leaf) -or !(Test-Path -LiteralPath $notices -PathType Container)) {
    throw 'The matching native reader and local-reader-licenses directory are required.'
}

$stage = Join-Path $PWD ('artifacts\portable-'+[Guid]::NewGuid().ToString('N'))
& .\build.ps1 -OutputDirectory $stage -ReleaseAssetsOnly
Copy-Item -LiteralPath $reader,$notices -Destination $stage -Recurse
Copy-Item docs\images (Join-Path $stage 'docs') -Recurse

$version = (Get-Item -LiteralPath (Join-Path $stage 'DeadlockHelper.exe')).VersionInfo.ProductVersion
if ($version -cnotmatch '^\d+\.\d+\.\d+(?:-[a-z0-9.-]+)?$') { throw "Invalid executable version '$version'." }
$tag = "v$version"
$zipName = "DeadlockHelper-$tag-win-x64.zip"
$sumsName = "SHA256SUMS-$tag.txt"
New-Item -ItemType Directory -Path $ReleaseDirectory -Force | Out-Null
$zip = Join-Path $ReleaseDirectory $zipName
$sums = Join-Path $ReleaseDirectory $sumsName
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $zipName" | Set-Content -LiteralPath $sums -Encoding ascii
Write-Output "Packaged $zip"
Write-Output "Staged in $stage"
