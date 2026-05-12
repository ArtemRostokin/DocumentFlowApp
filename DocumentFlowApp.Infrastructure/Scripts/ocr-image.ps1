param(
    [Parameter(Mandatory = $true)]
    [string]$ImagePath
)

$ErrorActionPreference = 'Stop'

[void][System.Reflection.Assembly]::LoadWithPartialName('System.Runtime.WindowsRuntime')

function Await-WinRtOperation {
    param([Parameter(Mandatory = $true)] $Operation)

    $operationType = $Operation.GetType()
    $asyncOperationInterface = $operationType.GetInterfaces() |
        Where-Object {
            $_.IsGenericType -and
            $_.GetGenericTypeDefinition().FullName -eq 'Windows.Foundation.IAsyncOperation`1'
        } |
        Select-Object -First 1

    if ($null -ne $asyncOperationInterface) {
        $resultType = $asyncOperationInterface.GenericTypeArguments[0]
        $method = [System.WindowsRuntimeSystemExtensions].GetMethods() |
            Where-Object {
                $_.Name -eq 'AsTask' -and
                $_.IsGenericMethodDefinition -and
                $_.GetGenericArguments().Count -eq 1 -and
                $_.GetParameters().Count -eq 1 -and
                $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
            } |
            Select-Object -First 1

        if ($null -eq $method) {
            throw 'Failed to resolve WinRT generic AsTask overload.'
        }

        $task = $method.MakeGenericMethod(@($resultType)).Invoke($null, @($Operation))
        return $task.GetAwaiter().GetResult()
    }

    $actionTask = [System.WindowsRuntimeSystemExtensions]::AsTask([Windows.Foundation.IAsyncAction]$Operation)
    $actionTask.GetAwaiter().GetResult() | Out-Null
    return $null
}

$storageFile = Await-WinRtOperation ([Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]::GetFileFromPathAsync($ImagePath))
$stream = Await-WinRtOperation ($storageFile.OpenAsync([Windows.Storage.FileAccessMode]::Read))
$decoder = Await-WinRtOperation ([Windows.Graphics.Imaging.BitmapDecoder, Windows.Graphics.Imaging, ContentType = WindowsRuntime]::CreateAsync($stream))
$bitmap = Await-WinRtOperation ($decoder.GetSoftwareBitmapAsync())
$bitmap = [Windows.Graphics.Imaging.SoftwareBitmap, Windows.Graphics.Imaging, ContentType = WindowsRuntime]::Convert(
    $bitmap,
    [Windows.Graphics.Imaging.BitmapPixelFormat]::Bgra8,
    [Windows.Graphics.Imaging.BitmapAlphaMode]::Premultiplied)

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

if ([string]::IsNullOrWhiteSpace($text)) {
    Write-Error 'Windows OCR completed but returned no text.'
}

Write-Output $text
