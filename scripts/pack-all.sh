#!/bin/bash
# Pack all publishable NuGet packages in dependency order.
# Usage: ./scripts/pack-all.sh [Release|Debug]

set -e

CONFIG="${1:-Release}"
OUT="./nupkgs"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

rm -rf "$OUT"
mkdir -p "$OUT"

echo "Packing all XFramework NuGet packages ($CONFIG) → $OUT/"
echo ""

pack() {
    local project="$1"
    echo "  Packing $project..."
    dotnet pack "$ROOT/$project" -c "$CONFIG" -o "$OUT" --no-build -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg -v quiet
}

# Build everything first
echo "Building solution..."
dotnet build "$ROOT/XFramework.slnx" -c "$CONFIG" -v quiet
echo ""

# Tier 0: Standalone
pack "src/Libraries/Bolt/Bolt.Protocol/Bolt.Protocol.csproj"
pack "src/Libraries/Bolt/Bolt.Server/Bolt.Server.csproj"
pack "src/Libraries/Bolt/Bolt.Client/Bolt.Client.csproj"
pack "src/SourceGenerators/XFramework.SourceGenerators/XFramework.SourceGenerators.csproj"

# Tier 1: Framework Foundation
pack "src/Shared/XFramework.Domain.Shared/XFramework.Domain.Shared.csproj"
pack "src/Modules/XFramework.Bolt/Bolt.Domain.Shared/Bolt.Domain.Shared.csproj"

# Tier 2: Framework Infrastructure
pack "src/Infrastructure/XFramework.Integration/XFramework.Integration.csproj"
pack "src/Kernel/XFramework.Domain/XFramework.Domain.csproj"
pack "src/Kernel/XFramework.Core/XFramework.Core.csproj"

# Tier 3: Module Contracts + Wrappers
pack "src/Modules/XFramework.IdentityServer/IdentityServer.Domain.Shared/IdentityServer.Domain.Shared.csproj"
pack "src/Modules/XFramework.IdentityServer/IdentityServer.Integration/IdentityServer.Integration.csproj"
pack "src/Modules/XFramework.Wallets/Wallets.Domain.Shared/Wallets.Domain.Shared.csproj"
pack "src/Modules/XFramework.Wallets/Wallets.Integration/Wallets.Integration.csproj"
pack "src/Modules/XFramework.Communications/Communications.Domain.Shared/Communications.Domain.Shared.csproj"
pack "src/Modules/XFramework.Communications/Communications.Integration/Communications.Integration.csproj"
pack "src/Modules/XFramework.Community/Community.Domain.Shared/Community.Domain.Shared.csproj"
pack "src/Modules/XFramework.SmsGateway/SmsGateway.Domain.Shared/SmsGateway.Domain.Shared.csproj"
pack "src/Modules/XFramework.SmsGateway/SmsGateway.Integration/SmsGateway.Integration.csproj"
pack "src/Modules/XFramework.Inventario/Inventario.Domain.Shared/Inventario.Domain.Shared.csproj"
pack "src/Modules/XFramework.Payments/Payments.Domain.Shared/Payments.Domain.Shared.csproj"

echo ""
echo "Done! Packages:"
ls -1 "$OUT"/*.nupkg
echo ""
echo "Total: $(ls -1 "$OUT"/*.nupkg | wc -l) packages"
