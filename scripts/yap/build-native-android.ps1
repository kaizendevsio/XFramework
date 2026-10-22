param(
    [string]$AndroidSdk = 'C:/Program Files (x86)/Android/android-sdk',
    [string]$JavaSdk = 'C:/Program Files/Android/openjdk/jdk-21.0.8'
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
dotnet publish "$repo/src/Presentation/XFramework.Yap.Native.Android" -c Release -r android-arm64 "-p:AndroidSdkDirectory=$AndroidSdk" "-p:JavaSdkDirectory=$JavaSdk"
if ($LASTEXITCODE -ne 0) { throw 'Android build failed' }
Write-Output "$repo/src/Presentation/XFramework.Yap.Native.Android/bin/Release/net10.0-android/android-arm64/publish/io.kaizendevs.yap.nativebenchmark-Signed.apk"
