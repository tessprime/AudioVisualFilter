# batch_extract.ps1
# Runs Praat formant extraction on all WAV files in a directory.
#
# Usage:
#   .\batch_extract.ps1 -WavDir .\wavs -OutDir .\formants [-PraatExe "C:\Program Files\Praat\Praat.exe"]

param(
    [Parameter(Mandatory)][string]$WavDir,
    [Parameter(Mandatory)][string]$OutDir,
    [string]$PraatExe = "C:\Program Files\Praat\Praat.exe"
)

$script = Join-Path $PSScriptRoot "extract_formants.praat"

if (-not (Test-Path $PraatExe)) {
    Write-Error "Praat not found at '$PraatExe'. Use -PraatExe to specify the path."
    exit 1
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$wavs = Get-ChildItem -Path $WavDir -Filter "*.wav"
if ($wavs.Count -eq 0) {
    Write-Error "No WAV files found in '$WavDir'."
    exit 1
}

$OutDirAbs = (Resolve-Path $OutDir).Path

foreach ($wav in $wavs) {
    $out = Join-Path $OutDirAbs "$($wav.BaseName).csv"
    Write-Host "Analyzing: $($wav.FullName) -> $out"
    & $PraatExe --run $script $wav.FullName $out
}

Write-Host "Done. CSVs written to $OutDir"
