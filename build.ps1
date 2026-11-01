$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $compiler)) { throw '.NET Framework 4.8 is required (included in Windows 11).' }
New-Item -ItemType Directory -Force dist | Out-Null
$sources = (Get-ChildItem src\*.cs).FullName
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /warn:4 /out:dist\DeadlockHelper.exe /win32manifest:src\app.manifest /win32icon:assets\companion.ico /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:vendor\Tesseract.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Copy-Item vendor\Tesseract.dll dist -Force
foreach($folder in @('x64','tessdata')){New-Item -ItemType Directory -Force (Join-Path dist $folder)|Out-Null;Copy-Item (Join-Path (Join-Path vendor $folder) '*') (Join-Path dist $folder) -Force}
Copy-Item vendor\LICENSE* dist -Force
Copy-Item vendor\THIRD-PARTY.md dist -Force
New-Item -ItemType Directory -Force dist\assets | Out-Null
Get-ChildItem assets -Filter *.png | Copy-Item -Destination dist\assets -Force
Copy-Item assets\sources.json dist\assets -Force
Copy-Item assets\companion.ico dist\assets -Force
if(Test-Path assets\resource-register.json){Copy-Item assets\resource-register.json dist\assets -Force}
if(Test-Path assets\local-art){New-Item -ItemType Directory -Force dist\assets\local-art | Out-Null;Copy-Item assets\local-art\*.png dist\assets\local-art -Force}
Copy-Item src\DeadlockHelper.exe.config dist -Force
Copy-Item README.md dist\README.md -Force -ErrorAction SilentlyContinue
Copy-Item LICENSE dist\LICENSE -Force
New-Item -ItemType Directory -Force dist\docs | Out-Null
Copy-Item docs\HELP.md,docs\PRIVACY.md,docs\CREDITS.md dist\docs -Force
Write-Output 'Built dist\DeadlockHelper.exe'

New-Item -ItemType Directory -Force dist\assets\ui | Out-Null
Copy-Item assets\ui\patron-2.png,assets\ui\patron-3.png,assets\ui\1009965641.png,assets\ui\1047818222.png dist\assets\ui -Force
