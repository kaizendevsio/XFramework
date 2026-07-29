```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8875) (Hyper-V)
Unknown processor
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2
  Job-MCJHEL : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2

MinIterationTime=250ms  IterationCount=15  LaunchCount=3
WarmupCount=5

```
| Method   | Concurrency | Mean     | Error     | StdDev    | P95        | Op/s  | Gen0   | Allocated |
|--------- |------------ |---------:|----------:|----------:|-----------:|------:|-------:|----------:|
| **Bolt_Hub** | **1**           | **201.5 μs** |  **14.00 μs** |  **26.30 μs** |   **248.6 μs** | **4,962** |      **-** |   **3.69 KB** |
| **Bolt_Hub** | **64**          | **777.4 μs** | **116.35 μs** | **212.74 μs** | **1,128.5 μs** | **1,286** | **3.9063** |  **162.6 KB** |
