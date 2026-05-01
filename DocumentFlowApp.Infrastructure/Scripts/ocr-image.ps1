param(
    [Parameter(Mandatory = $true)]
    [string]$ImagePath
)

$ErrorActionPreference = 'Stop'

[void][System.Reflection.Assembly]::LoadWithPartialName('System.Runtime.WindowsRuntime')

function Await-WinRtOperation {
    param([Parameter(Mandatory = $true)] $Operation)
    return [System.WindowsRuntimeSystemExtensions]::AsTask($Operation).GetAwaiter().GetResult()
}

$storageFile = Await-WinRtOperation ([Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]::GetFileFromPathAsync($ImagePath))
$stream = Await-WinRtOperation ($storageFile.OpenAsync([Windows.Storage.FileAccessMode]::Read))
$decoder = Await-WinRtOperation ([Windows.Graphics.Imaging.BitmapDecoder, Windows.Graphics.Imaging, ContentType = WindowsRuntime]::CreateAsync($stream))
$bitmap = Await-WinRtOperation ($decoder.GetSoftwareBitmapAsync())

$engine = [Windows.Media.Ocr.OcrEngine, Windows.Media.Ocr, ContentType = WindowsRuntime]::TryCreateFromUserProfileLanguages()
if ($null -eq $engine) {
    $languageCode = [Windows.System.UserProfile.GlobalizationPreferences, Windows.System.UserProfile, ContentType = WindowsRuntime]::Languages | Select-Object -First 1
    if ($languageCode) {
        $language = [Windows.Globalization.Language, Windows.Globalization, ContentType = WindowsRuntime]::new($languageCode)
        $engine = [Windows.Media.Ocr.OcrEngine, Windows.Media.Ocr, ContentType = WindowsRuntime]::TryCreateFromLanguage($language)
    }
}

if ($null -eq $engine) {
    throw 'Windows OCR engine is unavailable.'
}

$result = Await-WinRtOperation ($engine.RecognizeAsync($bitmap))
$rawText = if ($null -eq $result.Text) { '' } else { [string]$result.Text }
$text = ($rawText -replace '\r', ' ' -replace '\n', ' ').Trim()

Write-Output $text
