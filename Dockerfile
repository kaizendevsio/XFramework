# linux/amd64 manifests are pinned so deployment provenance includes immutable
# compiler and runtime roots. Update both digests together during SDK upgrades.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:493fca072aac81307027cbb7b7c9a82b6e87d222af315504d05dc6530e69b519 AS build
ARG PROJECT_PATH
WORKDIR /src

# Copy solution and Directory.* files first for layer caching
COPY Directory.Packages.props Directory.Build.props* Version.props ./
COPY XFramework.slnx ./

# Copy all project files for restore
COPY src/ src/

# Restore the specific project (and its dependencies)
RUN dotnet restore "${PROJECT_PATH}"

# Build + publish
RUN dotnet publish "${PROJECT_PATH}" \
    -c Release \
    -o /app/publish \
    --no-restore

# Write the entry-point DLL name so the runtime stage can use it
RUN basename "${PROJECT_PATH}" .csproj > /app/publish/.entrypoint

# -- Runtime ---------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:68abecb063cf367fdce0a7f0ab5678beaa99f60c7f616df862ca2a51c387d4e7 AS runtime
WORKDIR /app

# Install curl for health checks and CA tooling for the mounted Bolt Hub trust root.
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["sh", "-c", "set -eu; exec dotnet \"$(cat .entrypoint).dll\""]
