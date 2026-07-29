```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8875) (Hyper-V)
Unknown processor
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2
  Job-SFSXBT : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2

MinIterationTime=250ms  IterationCount=15  LaunchCount=3
WarmupCount=5

```
| Method   | Concurrency | Mean     | Error     | StdDev    | P95        | Op/s  | Gen0   | Allocated |
|--------- |------------ |---------:|----------:|----------:|-----------:|------:|-------:|----------:|
| **Bolt_Hub** | **1**           | **196.3 μs** |  **10.39 μs** |  **18.99 μs** |   **232.6 μs** | **5,094** |      **-** |    **3.7 KB** |
| **Bolt_Hub** | **64**          | **777.1 μs** | **108.23 μs** | **197.91 μs** | **1,288.6 μs** | **1,287** | **3.9063** | **162.38 KB** |
