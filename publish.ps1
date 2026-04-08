param(
    [string]$OutputDir = ".\\publish",
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "LiteMarkWin.csproj"

$publishArgs = @(
    "publish"
    $projectPath
    "-c"
    "Release"
    "-r"
    "win-x64"
    "-p:PublishSingleFile=true"
    "-p:EnableCompressionInSingleFile=true"
    "-p:DebugType=None"
    "-p:DebugSymbols=false"
    "-o"
    $OutputDir
)

if ($FrameworkDependent) {
    $publishArgs += "--no-self-contained"
}
else {
    $publishArgs += @(
        "--self-contained"
        "true"
        "-p:IncludeNativeLibrariesForSelfExtract=true"
    )
}

Write-Host "Publishing LiteMark..."
Write-Host ("Output directory: " + $OutputDir)

& dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

Get-ChildItem -LiteralPath $OutputDir | Select-Object Name, Length, LastWriteTime
