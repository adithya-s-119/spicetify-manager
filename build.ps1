# Rebuilds SpicetifyGui.exe and SpicetifyApply.exe using the C# compiler
# that ships with the .NET Framework (no SDK or install required).

$ErrorActionPreference = "Stop"

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $csc)) {
    throw "Could not find csc.exe. Windows 10/11 includes it with the .NET Framework."
}

Push-Location $PSScriptRoot
try {
    Write-Host "Building SpicetifyGui.exe..."
    & $csc /nologo /target:winexe /platform:anycpu /optimize+ /win32icon:SpicetifyGui.ico `
        /out:SpicetifyGui.exe `
        /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
        SpicetifyGui.cs
    if ($LASTEXITCODE -ne 0) { throw "GUI build failed." }

    Write-Host "Building SpicetifyApply.exe..."
    & $csc /nologo /target:exe /platform:anycpu /optimize+ /win32icon:SpicetifyGui.ico `
        /out:SpicetifyApply.exe SpicetifyApply.cs
    if ($LASTEXITCODE -ne 0) { throw "Console build failed." }

    Write-Host "Done: SpicetifyGui.exe, SpicetifyApply.exe"
}
finally {
    Pop-Location
}
