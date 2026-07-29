```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8875) (Hyper-V)
Unknown processor
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2
  Job-DOFCMD : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2

MinIterationTime=250ms  IterationCount=15  LaunchCount=3
WarmupCount=5

```
| Method   | Concurrency | Mean     | Error     | StdDev    | P95        | Op/s  | Gen0   | Allocated |
|--------- |------------ |---------:|----------:|----------:|-----------:|------:|-------:|----------:|
| **Bolt_Hub** | **1**           | **204.0 μs** |  **17.16 μs** |  **31.81 μs** |   **267.8 μs** | **4,903** |      **-** |    **3.7 KB** |
| **Bolt_Hub** | **64**          | **777.0 μs** | **107.14 μs** | **195.91 μs** | **1,174.8 μs** | **1,287** | **3.9063** |  **162.5 KB** |
