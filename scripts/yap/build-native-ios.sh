#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/../.."
project=src/Presentation/XFramework.Yap.Native.iOS/XFramework.Yap.Native.iOS.csproj
# Never choose an organization or register an app implicitly. First create a development
# profile for io.kaizendevs.yap.nativebenchmark using your own Personal Team in Xcode.
if [[ "${1:-}" == "--unsigned" ]]; then
  dotnet build "$project" -c Release -r ios-arm64 -p:EnableCodeSigning=false
else
  : "${YAP_CODESIGN_KEY:?Set the Apple Development signing identity name}"
  : "${YAP_CODESIGN_PROVISION:?Set the Yap development profile UUID or name}"
  dotnet publish "$project" -c Release -r ios-arm64 -p:BuildIpa=true \
    "-p:CodesignKey=$YAP_CODESIGN_KEY" "-p:CodesignProvision=$YAP_CODESIGN_PROVISION"
fi
