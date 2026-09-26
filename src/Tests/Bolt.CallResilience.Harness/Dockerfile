# Runtime image for the call relay network harness. Build context: the `dotnet publish` output.
#   dotnet publish src/Tests/Bolt.CallResilience.Harness -c Release -o out/harness
#   docker build -t bolt-call-harness -f src/Tests/Bolt.CallResilience.Harness/Dockerfile out/harness
FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends iproute2 && rm -rf /var/lib/apt/lists/*
COPY . /app
WORKDIR /app
