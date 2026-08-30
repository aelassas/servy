#Requires -Version 5.0
<#
.SYNOPSIS
    Unit tests for Get-FileEncoding.ps1.

.DESCRIPTION
    Creates temporary files with known Encodings/BOMs and validates that
    Get-FileEncoding correctly detects them and enforces throwOnInvalidBytes=true.
#>

$ErrorActionPreference = 'Stop'
$ScriptDir = $PSScriptRoot

# Import the target function
. (Join-Path $ScriptDir "Get-FileEncoding.ps1")

# Create isolated temporary directory for test files
$TestDir = Join-Path ([System.IO.Path]::GetTempPath()) "ServyEncodingTests_$([Guid]::NewGuid().ToString('N'))"
[void](New-Item -ItemType Directory -Path $TestDir -Force)

$TotalCount = 0
$PassCount = 0
$FailCount = 0

function Assert-Encoding {
    param(
        [string]$TestName,
        [string]$FilePath,
        [Type]$ExpectedType,
        [bool]$ExpectedEmitBom,
        [bool]$ExpectedBigEndian = $false
    )

    try {
        $encoding = Get-FileEncoding -Path $FilePath

        # 1. Check encoding type match
        if ($encoding.GetType() -ne $ExpectedType) {
            throw "Type mismatch: Expected $($ExpectedType.Name), got $($encoding.GetType().Name)"
        }

        # 2. Check BOM emission settings
        $preamble = $encoding.GetPreamble()
        $hasBom = $preamble.Length -gt 0
        if ($hasBom -ne $ExpectedEmitBom) {
            throw "BOM emission mismatch: Expected $ExpectedEmitBom, got $hasBom"
        }

        # 3. Check endianness if applicable (UTF16 / UTF32)
        if ($encoding -is [System.Text.UnicodeEncoding] -or $encoding -is [System.Text.UTF32Encoding]) {
            $isBigEndian = [bool](Test-ReflectionField -Object $encoding -FieldName "bigEndian")
            if ($isBigEndian -ne $ExpectedBigEndian) {
                throw "Endianness mismatch: Expected BigEndian=$ExpectedBigEndian, got BigEndian=$isBigEndian"
            }
        }

        # 4. Verify throwOnInvalidBytes policy by attempting to decode guaranteed invalid bytes.
        # UTF-16 requires an unpaired surrogate (e.g. high surrogate 0xD800 without low surrogate).
        $badBytes = if ($encoding -is [System.Text.UnicodeEncoding]) {
            if ($ExpectedBigEndian) { [byte[]]@(0xD8, 0x00) } else { [byte[]]@(0x00, 0xD8) }
        } else {
            [byte[]]@(0xFF, 0xFF, 0xFF, 0xFF)
        }

        $invalidBytesPassed = $false
        try {
            [void]$encoding.GetString($badBytes)
            $invalidBytesPassed = $true
        }
        catch {
            # Expected behavior: DecoderExceptionFallback should throw on invalid bytes
        }

        if ($invalidBytesPassed) {
            throw "throwOnInvalidBytes check failed: Encoding silently substituted invalid bytes instead of throwing!"
        }

        Write-Host "  [PASS] $TestName" -ForegroundColor Green
        $script:PassCount++
        $script:TotalCount++
    }
    catch {
        Write-Host "  [FAIL] $TestName - $_" -ForegroundColor Red
        $script:FailCount++
        $script:TotalCount++
    }
}

# Helper to inspect non-public fields for strict object property assertions across .NET Framework and .NET Core/5+
function Test-ReflectionField {
    param([object]$Object, [string]$FieldName)
    $flags = [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::NonPublic

    # Try the exact field name first, followed by standard .NET Core leading-underscore backing field conventions
    $candidateNames = @($FieldName, "_$FieldName", "_is$FieldName", "is$FieldName")
    $type = $Object.GetType()

    foreach ($name in $candidateNames) {
        $field = $type.GetField($name, $flags)
        if ($null -ne $field) {
            return $field.GetValue($Object)
        }
    }

    throw "Reflection seam broken: '$($type.FullName)' has no non-public field matching '$FieldName' on this runtime."
}

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " Running Get-FileEncoding.ps1 Tests                 " -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host ""

try {
    # -----------------------------------------------------
    # Test 1: UTF-8 without BOM (Default Fallback)
    # -----------------------------------------------------
    $fileUtf8NoBom = Join-Path $TestDir "utf8_nobom.txt"
    $utf8NoBomEnc = New-Object System.Text.UTF8Encoding($false, $true)
    [System.IO.File]::WriteAllText($fileUtf8NoBom, "Hello Servy World", $utf8NoBomEnc)
    Assert-Encoding -TestName "UTF-8 without BOM" -FilePath $fileUtf8NoBom -ExpectedType ([System.Text.UTF8Encoding]) -ExpectedEmitBom $false

    # -----------------------------------------------------
    # Test 2: UTF-8 with BOM
    # -----------------------------------------------------
    $fileUtf8Bom = Join-Path $TestDir "utf8_bom.txt"
    $utf8BomEnc = New-Object System.Text.UTF8Encoding($true, $true)
    [System.IO.File]::WriteAllText($fileUtf8Bom, "Hello Servy World", $utf8BomEnc)
    Assert-Encoding -TestName "UTF-8 with BOM" -FilePath $fileUtf8Bom -ExpectedType ([System.Text.UTF8Encoding]) -ExpectedEmitBom $true

    # -----------------------------------------------------
    # Test 3: UTF-16 LE (Unicode)
    # -----------------------------------------------------
    $fileUtf16Le = Join-Path $TestDir "utf16_le.txt"
    $utf16LeEnc = New-Object System.Text.UnicodeEncoding($false, $true, $true)
    [System.IO.File]::WriteAllText($fileUtf16Le, "Hello Servy World", $utf16LeEnc)
    Assert-Encoding -TestName "UTF-16 Little Endian" -FilePath $fileUtf16Le -ExpectedType ([System.Text.UnicodeEncoding]) -ExpectedEmitBom $true -ExpectedBigEndian $false

    # -----------------------------------------------------
    # Test 4: UTF-16 BE (BigEndianUnicode)
    # -----------------------------------------------------
    $fileUtf16Be = Join-Path $TestDir "utf16_be.txt"
    $utf16BeEnc = New-Object System.Text.UnicodeEncoding($true, $true, $true)
    [System.IO.File]::WriteAllText($fileUtf16Be, "Hello Servy World", $utf16BeEnc)
    Assert-Encoding -TestName "UTF-16 Big Endian" -FilePath $fileUtf16Be -ExpectedType ([System.Text.UnicodeEncoding]) -ExpectedEmitBom $true -ExpectedBigEndian $true

    # -----------------------------------------------------
    # Test 5: UTF-32 LE
    # -----------------------------------------------------
    $fileUtf32Le = Join-Path $TestDir "utf32_le.txt"
    $utf32LeEnc = New-Object System.Text.UTF32Encoding($false, $true, $true)
    [System.IO.File]::WriteAllText($fileUtf32Le, "Hello Servy World", $utf32LeEnc)
    Assert-Encoding -TestName "UTF-32 Little Endian" -FilePath $fileUtf32Le -ExpectedType ([System.Text.UTF32Encoding]) -ExpectedEmitBom $true -ExpectedBigEndian $false

    # -----------------------------------------------------
    # Test 6: UTF-32 BE
    # -----------------------------------------------------
    $fileUtf32Be = Join-Path $TestDir "utf32_be.txt"
    $utf32BeEnc = New-Object System.Text.UTF32Encoding($true, $true, $true)
    [System.IO.File]::WriteAllText($fileUtf32Be, "Hello Servy World", $utf32BeEnc)
    Assert-Encoding -TestName "UTF-32 Big Endian" -FilePath $fileUtf32Be -ExpectedType ([System.Text.UTF32Encoding]) -ExpectedEmitBom $true -ExpectedBigEndian $true

    # -----------------------------------------------------
    # Boundary & Truncated File Tests
    # -----------------------------------------------------
    # Empty file (0 bytes) -> default UTF-8 without BOM
    $fileEmpty = Join-Path $TestDir "empty.txt"
    [System.IO.File]::WriteAllBytes($fileEmpty, [byte[]]@())
    Assert-Encoding -TestName "Empty file falls back to UTF-8 without BOM" -FilePath $fileEmpty -ExpectedType ([System.Text.UTF8Encoding]) -ExpectedEmitBom $false

    # Single 0xFF byte -> default UTF-8 without BOM
    $fileOneByte = Join-Path $TestDir "one_byte.txt"
    [System.IO.File]::WriteAllBytes($fileOneByte, [byte[]]@(0xFF))
    Assert-Encoding -TestName "1-byte file falls back to UTF-8 without BOM" -FilePath $fileOneByte -ExpectedType ([System.Text.UTF8Encoding]) -ExpectedEmitBom $false

    # Bare UTF-16 LE BOM (2 bytes) -> UnicodeEncoding LE
    $fileBareUtf16Le = Join-Path $TestDir "bare_utf16_le.txt"
    [System.IO.File]::WriteAllBytes($fileBareUtf16Le, [byte[]]@(0xFF, 0xFE))
    Assert-Encoding -TestName "2-byte file with UTF-16 LE BOM" -FilePath $fileBareUtf16Le -ExpectedType ([System.Text.UnicodeEncoding]) -ExpectedEmitBom $true -ExpectedBigEndian $false

    # Bare UTF-8 BOM (3 bytes) -> UTF8Encoding with BOM
    $fileBareUtf8Bom = Join-Path $TestDir "bare_utf8_bom.txt"
    [System.IO.File]::WriteAllBytes($fileBareUtf8Bom, [byte[]]@(0xEF, 0xBB, 0xBF))
    Assert-Encoding -TestName "3-byte file with UTF-8 BOM" -FilePath $fileBareUtf8Bom -ExpectedType ([System.Text.UTF8Encoding]) -ExpectedEmitBom $true

}
finally {
    # Cleanup workspace
    if (Test-Path $TestDir) {
        Remove-Item -Recurse -Force $TestDir
    }
}

# ----------------------------------------------------------------
# Summary Output
# ----------------------------------------------------------------
Write-Host "`n====================================================" -ForegroundColor Cyan
Write-Host " Test Summary" -ForegroundColor Cyan
Write-Host " Total   : $script:TotalCount" -ForegroundColor Gray
Write-Host " Passed  : $script:PassCount" -ForegroundColor Green
if ($script:FailCount -gt 0) {
    Write-Host " Failed  : $script:FailCount" -ForegroundColor Red
    Write-Host "====================================================" -ForegroundColor Cyan
    exit 1
} else {
    Write-Host " Failed  : 0" -ForegroundColor Green
    Write-Host "====================================================" -ForegroundColor Cyan
    exit 0
}
