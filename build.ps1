param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
# Stop only the executable that this build will replace.
$outputExe = [IO.Path]::GetFullPath((Join-Path $OutputDirectory 'BatteryBar.exe'))
Get-Process -Name BatteryBar -ErrorAction SilentlyContinue | ForEach-Object {
    $processPath = $_.Path
    if ($processPath -and [string]::Equals([IO.Path]::GetFullPath($processPath), $outputExe, [StringComparison]::OrdinalIgnoreCase)) {
        if (-not $_.CloseMainWindow() -or -not $_.WaitForExit(2000)) { Stop-Process -Id $_.Id -ErrorAction Stop; $_.WaitForExit() }
    }
}
$sources = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName }
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /out:"$OutputDirectory\BatteryBar.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll /reference:Microsoft.CSharp.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Output "Built $OutputDirectory\BatteryBar.exe"
