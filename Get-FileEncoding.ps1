<#
.SYNOPSIS
    Detects the encoding of a file based on its Byte Order Mark (BOM).

.DESCRIPTION
    Reads the initial bytes of a file to determine if it is:
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

    [byte[]]$bytes = [System.IO.File]::ReadAllBytes($Path)

    # UTF-8 with BOM (EF BB BF)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        return [System.Text.Encoding]::UTF8
    }

    # UTF-16 LE / Unicode (FF FE)
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
        return [System.Text.Encoding]::Unicode
    }

    # UTF-16 BE / BigEndianUnicode (FE FF)
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) {
        return [System.Text.Encoding]::BigEndianUnicode
    }

    # Default: UTF-8 without BOM (Standard for modern .NET and Git)
    return New-Object System.Text.UTF8Encoding($false)
}