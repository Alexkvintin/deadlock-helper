$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
& .\scripts\build-local-reader.ps1
& .\build.ps1
New-Item -ItemType Directory -Force releases | Out-Null
$stage=Join-Path $PWD ('artifacts\release-v1-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $stage | Out-Null
foreach($name in @('companion-reader.exe','local-reader-licenses','DeadlockHelper.exe','DeadlockHelper.exe.config','README.md','LICENSE','Tesseract.dll','x64','tessdata','THIRD-PARTY.md')){Copy-Item (Join-Path dist $name) $stage -Recurse -Force}
Copy-Item dist\LICENSE-*.txt $stage
$publicAssets=Join-Path $stage 'assets'
New-Item -ItemType Directory -Force $publicAssets,(Join-Path $publicAssets 'ui'),(Join-Path $publicAssets 'local-art'),(Join-Path $stage 'docs') | Out-Null
Copy-Item assets\companion.ico,assets\sources.json,assets\resource-register.json $publicAssets
Get-ChildItem assets -Filter *.png | Copy-Item -Destination $publicAssets
Copy-Item assets\brand $publicAssets -Recurse
Copy-Item assets\ui\patron-2.png,assets\ui\patron-3.png,assets\ui\1009965641.png,assets\ui\1047818222.png (Join-Path $publicAssets 'ui')
Copy-Item assets\local-art\background.png,assets\local-art\midboss.png (Join-Path $publicAssets 'local-art')
Copy-Item docs\HELP.md,docs\PRIVACY.md,docs\CREDITS.md (Join-Path $stage 'docs')
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath releases\DeadlockHelper-v1.0-win-x64.zip -Force
Get-FileHash releases\DeadlockHelper-v1.0-win-x64.zip -Algorithm SHA256 | ForEach-Object { $_.Hash+'  '+(Split-Path $_.Path -Leaf) } | Set-Content releases\SHA256SUMS-v1.0.txt -Encoding ascii
Write-Output 'Packaged releases\DeadlockHelper-v1.0-win-x64.zip'
