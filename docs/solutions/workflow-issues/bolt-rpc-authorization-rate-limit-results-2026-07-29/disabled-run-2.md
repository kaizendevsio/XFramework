```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8875) (Hyper-V)
Unknown processor
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2
  Job-HVYOXN : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2

MinIterationTime=250ms  IterationCount=15  LaunchCount=3
WarmupCount=5

```
| Method   | Concurrency | Mean     | Error     | StdDev    | P95        | Op/s  | Gen0   | Allocated |
|--------- |------------ |---------:|----------:|----------:|-----------:|------:|-------:|----------:|
| **Bolt_Hub** | **1**           | **237.8 μs** |  **28.46 μs** |  **52.75 μs** |   **339.4 μs** | **4,205** |      **-** |   **3.69 KB** |
| **Bolt_Hub** | **64**          | **757.9 μs** | **111.48 μs** | **201.03 μs** | **1,114.9 μs** | **1,319** | **3.9063** | **167.88 KB** |
