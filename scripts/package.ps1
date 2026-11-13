param([switch]$UseBuiltReader)
$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
if(-not $UseBuiltReader){& .\scripts\build-local-reader.ps1}elseif(-not (Test-Path dist/companion-reader.exe)){throw 'Build the matching native reader first'}
& .\build.ps1
New-Item -ItemType Directory -Force releases | Out-Null
$stage=Join-Path $PWD ('artifacts\release-v110beta1-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $stage | Out-Null
foreach($name in @('companion-reader.exe','local-reader-licenses','DeadlockHelper.exe','DeadlockHelper.exe.config','README.md','LICENSE','Tesseract.dll','x64','tessdata','THIRD-PARTY.md')){Copy-Item (Join-Path dist $name) $stage -Recurse -Force}
Copy-Item dist\LICENSE-*.txt $stage
$publicAssets=Join-Path $stage 'assets'
New-Item -ItemType Directory -Force $publicAssets,(Join-Path $publicAssets 'ui'),(Join-Path $publicAssets 'local-art'),(Join-Path $stage 'docs') | Out-Null
Copy-Item assets\companion.ico,assets\sources.json,assets\resource-register.json $publicAssets
# Only original assets with an explicit redistribution grant are bundled.
$register=Get-Content assets/resource-register.json -Raw | ConvertFrom-Json
foreach($entry in $register){if($entry.redistribute -eq $true){$from=Join-Path assets $entry.file;if(Test-Path $from){$to=Join-Path $publicAssets $entry.file;New-Item -ItemType Directory -Force (Split-Path $to -Parent)|Out-Null;Copy-Item $from $to -Force}}}
Copy-Item assets\brand $publicAssets -Recurse
Copy-Item docs\HELP.md,docs\PRIVACY.md,docs\CREDITS.md (Join-Path $stage 'docs')
Copy-Item docs\images (Join-Path $stage 'docs') -Recurse
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath releases\DeadlockHelper-v1.1.0-beta.1-win-x64.zip -Force
Get-FileHash releases\DeadlockHelper-v1.1.0-beta.1-win-x64.zip -Algorithm SHA256 | ForEach-Object { $_.Hash+'  '+(Split-Path $_.Path -Leaf) } | Set-Content releases\SHA256SUMS-v1.1.0-beta.1.txt -Encoding ascii
Write-Output 'Packaged releases\DeadlockHelper-v1.1.0-beta.1-win-x64.zip'
