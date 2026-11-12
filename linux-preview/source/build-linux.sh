#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")"
dotnet build AscendAPP.Linux.slnx -c Release
dotnet run --project tests/AscendAPP.SelfTest -c Release --no-build
dotnet run --project src/FooMeter.Linux -c Release --no-build -- --self-test-linux
dotnet publish src/FooMeter.Linux -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=false -o artifacts/FooMeter-linux-x64
cp README-LINUX.md artifacts/FooMeter-linux-x64/
cp LINUX-CHANGES.md artifacts/FooMeter-linux-x64/
cp -R THIRD_PARTY_NOTICES artifacts/FooMeter-linux-x64/
cp tools/run-linux.sh artifacts/FooMeter-linux-x64/run-foometer.sh
chmod +x artifacts/FooMeter-linux-x64/run-foometer.sh
tar -C artifacts -czf artifacts/FooMeter-linux-x64-preview.tar.gz FooMeter-linux-x64
printf '\nPackage: artifacts/FooMeter-linux-x64-preview.tar.gz\n'
