using BenchmarkDotNet.Running;
using Bolt.Tests;

BenchmarkSwitcher.FromAssembly(typeof(BoltBenchmarks).Assembly).Run(args);
