# CloverPit SaveTool
# Decrypt / encrypt CloverPit save (SaveData\GameData\GameDataFull.json)
# Encryption: XOR with fixed 27-byte key (same as EncryptCustom in game code)
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File SaveTool.ps1 verify     # decrypt and validate JSON (read-only)
#   powershell -ExecutionPolicy Bypass -File SaveTool.ps1 decrypt    # save -> GameDataFull.decrypted.json
#   powershell -ExecutionPolicy Bypass -File SaveTool.ps1 decrypt -Save <path> -Out <path>
#   powershell -ExecutionPolicy Bypass -File SaveTool.ps1 encrypt -Json <path>   # JSON back over save (+ .bak)

param(
    [Parameter(Position = 0)][ValidateSet("decrypt", "encrypt", "verify")][string]$Mode = "verify",
    [string]$Save = "",
    [string]$Out = "",
    [string]$Json = ""
)

$ErrorActionPreference = "Stop"

# XOR key (27 bytes), extracted from PlatformDataMaster.EncryptCustom / verified by public save editor
$Key = [byte[]](0x48, 0x06, 0x5c, 0x11, 0x06, 0x43, 0x01, 0x60, 0x18, 0x55,
                0x42, 0x18, 0x19, 0x1a, 0x00, 0x4a, 0x5a, 0x1a, 0x00, 0x51,
                0x56, 0x46, 0x4e, 0x47, 0x0c, 0x1b, 0x01)

function Get-SavePath {
    if ($Save -ne "") { return $Save }
    $candidates = @(
        "C:\Program Files (x86)\Steam\steamapps\common\CloverPit\SaveData\GameData\GameDataFull.json"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { return (Resolve-Path $c).Path }
    }
    throw "Save file not found. Use -Save <path>"
}

function Xor-Bytes([byte[]]$data) {
    $out = New-Object byte[] $data.Length
    for ($i = 0; $i -lt $data.Length; $i++) { $out[$i] = $data[$i] -bxor $Key[$i % $Key.Length] }
    return $out
}

switch ($Mode) {
    "decrypt" {
        $src = Get-SavePath
        if ($Out -eq "") { $Out = [IO.Path]::ChangeExtension($src, ".decrypted.json") }
        $bytes = [IO.File]::ReadAllBytes($src)
        [IO.File]::WriteAllText($Out, [Text.Encoding]::UTF8.GetString((Xor-Bytes $bytes)), [Text.Encoding]::UTF8)
        Write-Host "OK: $src -> $Out"
    }
    "encrypt" {
        if ($Json -eq "") { throw "Specify -Json <path to decrypted JSON>" }
        $src = Get-SavePath
        $backup = "$src.bak"
        Copy-Item $src $backup -Force
        $bytes = [Text.Encoding]::UTF8.GetBytes([IO.File]::ReadAllText($Json))
        [IO.File]::WriteAllBytes($src, (Xor-Bytes $bytes))
        Write-Host "OK: $Json -> $src (backup of old save: $backup)"
    }
    "verify" {
        $src = Get-SavePath
        $bytes = [IO.File]::ReadAllBytes($src)
        $text = [Text.Encoding]::UTF8.GetString((Xor-Bytes $bytes))
        try {
            $j = $text | ConvertFrom-Json
            if ($j.gameplayData) {
                Write-Host "JSON valid. gameplayData keys:"
                $j.gameplayData.PSObject.Properties.Name | Select-Object -First 40
            } else {
                Write-Host "JSON valid. Root keys:"
                $j.PSObject.Properties.Name
            }
        } catch {
            Write-Host "JSON ERROR: $_"
            Write-Host "First 200 chars:"
            Write-Host $text.Substring(0, [Math]::Min(200, $text.Length))
        }
    }
}
