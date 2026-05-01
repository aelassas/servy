<#
.SYNOPSIS
    Detects the encoding of a file based on its Byte Order Mark (BOM).

.DESCRIPTION
    Reads the initial bytes of a file to determine if it is:
    - UTF-32 Little Endian (New)
    - UTF-8 with BOM
    - UTF-16 Little Endian (Unicode)
    - UTF-16 Big Endian
    - UTF-8 without BOM (Default)

.PARAMETER Path
    The full path to the file to check.

.EXAMPLE
    $encoding = Get-FileEncoding -Path "C:\temp\config.json"
#>
function Get-FileEncoding {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    # 1. Open a small stream and read only the first 4 bytes.
    # This prevents loading entire files into memory just for a BOM check.
    $buffer = New-Object byte[] 4
    $fs = [System.IO.File]::OpenRead($Path)
    try {
        $readCount = $fs.Read($buffer, 0, 4)
    } finally {
        $fs.Dispose()
    }

    # 2. Inspect the buffer. 
    # Logic: Check longer BOMs first to avoid misclassification.

    # UTF-32 LE (FF FE 00 00)
    # Check this before UTF-16 LE to avoid false positives.
    if ($readCount -ge 4 -and $buffer[0] -eq 0xFF -and $buffer[1] -eq 0xFE -and $buffer[2] -eq 0x00 -and $buffer[3] -eq 0x00) {
        return [System.Text.Encoding]::UTF32
    }

    # UTF-8 with BOM (EF BB BF)
    if ($readCount -ge 3 -and $buffer[0] -eq 0xEF -and $buffer[1] -eq 0xBB -and $buffer[2] -eq 0xBF) {
        return [System.Text.Encoding]::UTF8
    }

    # UTF-16 LE / Unicode (FF FE)
    if ($readCount -ge 2 -and $buffer[0] -eq 0xFF -and $buffer[1] -eq 0xFE) {
        return [System.Text.Encoding]::Unicode
    }

    # UTF-16 BE / BigEndianUnicode (FE FF)
    if ($readCount -ge 2 -and $buffer[0] -eq 0xFE -and $buffer[1] -eq 0xFF) {
        return [System.Text.Encoding]::BigEndianUnicode
    }

    # Default: UTF-8 without BOM (Standard for modern .NET and Git)
    return New-Object System.Text.UTF8Encoding($false)
}