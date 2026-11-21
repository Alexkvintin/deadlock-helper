param([string]$OutputDirectory = 'dist', [switch]$ReleaseAssetsOnly)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $compiler)) { throw '.NET Framework 4.8 is required.' }

New-Item -ItemType Directory -Force $output | Out-Null
$exe = Join-Path $output 'DeadlockHelper.exe'
$sources = (Get-ChildItem src\*.cs).FullName
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /warn:4 "/out:$exe" /win32manifest:src\app.manifest /win32icon:assets\companion.ico /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:vendor\Tesseract.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

Copy-Item vendor\Tesseract.dll,vendor\LICENSE*,vendor\THIRD-PARTY.md,src\DeadlockHelper.exe.config,README.md,LICENSE $output -Force
foreach ($folder in @('x64','tessdata')) {
    $target = Join-Path $output $folder
    New-Item -ItemType Directory -Force $target | Out-Null
    Copy-Item "vendor/$folder/*" $target -Force
}

$assetOutput = Join-Path $output 'assets'
$docsOutput = Join-Path $output 'docs'
New-Item -ItemType Directory -Force $assetOutput,$docsOutput | Out-Null
Copy-Item assets\sources.json,assets\companion.ico,assets\resource-register.json $assetOutput -Force
Copy-Item assets\brand $assetOutput -Recurse -Force
if ($ReleaseAssetsOnly) {
    $register = Get-Content assets/resource-register.json -Raw | ConvertFrom-Json
    foreach ($entry in ($register | Where-Object redistribute -eq $true)) {
        $source = Join-Path assets $entry.file
        $target = Join-Path $assetOutput $entry.file
        if (Test-Path -LiteralPath $source -PathType Leaf) {
            New-Item -ItemType Directory -Force (Split-Path $target -Parent) | Out-Null
            Copy-Item -LiteralPath $source -Destination $target -Force
        }
    }
} else {
    foreach ($folder in @('','local-art','ui')) {
        $source = if ($folder) { Join-Path 'assets' $folder } else { 'assets' }
        $target = if ($folder) { Join-Path $assetOutput $folder } else { $assetOutput }
        if (Test-Path $source) {
            New-Item -ItemType Directory -Force $target | Out-Null
            Get-ChildItem $source -Filter *.png -File | Copy-Item -Destination $target -Force
        }
    }
}
Copy-Item docs\HELP.md,docs\PRIVACY.md,docs\CREDITS.md,docs\LICENSE-REVIEW.md $docsOutput -Force
Write-Output "Built $exe"
