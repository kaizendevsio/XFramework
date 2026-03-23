using BenchmarkDotNet.Running;
using IdentityServer.Benchmarks;

// Run with: dotnet run -c Release -- --filter "*Sequential*" or "*Concurrent*"
// Or run all: dotnet run -c Release
BenchmarkSwitcher.FromAssembly(typeof(TransportBenchmarks).Assembly).Run(args);
