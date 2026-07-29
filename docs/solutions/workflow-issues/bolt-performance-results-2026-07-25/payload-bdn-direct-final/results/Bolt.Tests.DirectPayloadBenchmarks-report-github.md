```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8875) (Hyper-V)
Unknown processor
.NET SDK 10.0.100
  [Host]          : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2
  PayloadCredible : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2

Job=PayloadCredible  MinIterationTime=250ms  IterationCount=15
LaunchCount=3  WarmupCount=5

```
| Method      | PayloadBytes | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0    | Gen1    | Gen2    | Allocated | Alloc Ratio |
|------------ |------------- |-----------:|---------:|---------:|------:|--------:|--------:|--------:|--------:|----------:|------------:|
| **Direct_Bolt** | **524288**       | **1,283.8 μs** | **105.8 μs** | **198.6 μs** |  **1.02** |    **0.22** |  **7.8125** |  **7.8125** |  **7.8125** |  **528145 B** |        **1.00** |
| Direct_gRPC | 524288       | 2,508.3 μs | 203.7 μs | 377.5 μs |  2.00 |    0.43 | 23.4375 | 23.4375 | 23.4375 |         - |        0.00 |
|             |              |            |          |          |       |         |         |         |         |           |             |
| **Direct_Bolt** | **1048576**      | **2,410.2 μs** | **136.1 μs** | **259.0 μs** |  **1.01** |    **0.15** | **15.6250** | **15.6250** | **15.6250** | **1052998 B** |        **1.00** |
| Direct_gRPC | 1048576      | 5,688.0 μs | 477.9 μs | 897.6 μs |  2.39 |    0.45 | 46.8750 | 46.8750 | 46.8750 |         - |        0.00 |
