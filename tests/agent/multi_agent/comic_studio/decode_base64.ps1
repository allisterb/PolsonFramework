param([string]$base64File, [string]$outputFile)
$b64 = Get-Content -Raw $base64File
$b64 = $b64.Trim().Replace("`r", "").Replace("`n", "")
if ($b64.IndexOf("base64,") -ge 0) {
    $b64 = $b64.Substring($b64.IndexOf("base64,") + 7)
}
$rem = $b64.Length % 4
if ($rem -ne 0) {
    $b64 = $b64.PadRight($b64.Length + (4 - $rem), '=')
}
$bytes = [Convert]::FromBase64String($b64)
[IO.File]::WriteAllBytes($outputFile, $bytes)
Write-Host "Saved $outputFile successfully with $($bytes.Length) bytes"
