FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
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

# ── Runtime ───────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["sh", "-c", "dotnet $(cat .entrypoint).dll"]
