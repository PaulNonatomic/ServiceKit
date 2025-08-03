# ServiceKit Performance Tests

This directory contains a comprehensive benchmark and performance testing suite for the ServiceKit package.

## Overview

The benchmark suite is designed to measure the performance characteristics of ServiceKit's core functionality:

- **Service Registration**: Measuring the cost of registering services with various configurations
- **Service Resolution**: Testing synchronous and asynchronous service retrieval performance
- **Dependency Injection**: Benchmarking the injection process for different dependency scenarios
- **Service Lifecycle**: Testing complete service lifecycle operations
- **Stress Testing**: High-volume and concurrent access patterns

## Structure

```
PerformanceTests/
├── BenchmarkFramework/           # Custom benchmarking framework
│   ├── BenchmarkTimer.cs         # High-precision timing utilities
│   └── BenchmarkRunner.cs        # Benchmark execution and reporting
├── BenchmarkRunner/              # Comprehensive benchmark suites
│   └── ServiceKitBenchmarkSuite.cs
├── StressTesting/                # Stress and load testing
│   └── ServiceKitStressTests.cs
├── ServiceRegistrationBenchmarks.cs
├── ServiceResolutionBenchmarks.cs
├── DependencyInjectionBenchmarks.cs
├── ServiceLifecycleBenchmarks.cs
└── README.md
```

## Benchmarking Framework

The custom benchmarking framework provides:

### BenchmarkTimer
- High-precision timing using `Stopwatch`
- Statistical analysis (mean, median, min, max, standard deviation)
- Operations per second calculation
- Comprehensive result reporting

### BenchmarkRunner
- Configurable warmup and benchmark iterations
- Support for synchronous and asynchronous operations
- Setup/teardown patterns for consistent testing
- Automatic garbage collection between tests
- Result aggregation and reporting

## Test Categories

### 1. Service Registration Benchmarks
Tests the performance of various service registration patterns:
- Simple service registration
- Registration with tags
- Registration with circular dependency exemption
- Bulk service registration
- Service unregistration

### 2. Service Resolution Benchmarks
Measures service retrieval performance:
- Synchronous `GetService<T>()`
- `TryGetService<T>()` patterns
- Asynchronous `GetServiceAsync<T>()`
- Multi-type service resolution
- Non-existent service handling
- Tag-based service queries

### 3. Dependency Injection Benchmarks
Evaluates injection performance:
- Single dependency injection
- Multiple dependency injection
- Inherited field injection
- Optional dependency handling
- Complex dependency graphs
- Injection with timeouts and cancellation

### 4. Service Lifecycle Benchmarks
Tests complete service lifecycle operations:
- Register → Ready → Get → Unregister cycles
- Service status checking
- Tag management operations
- Cleanup and reregistration
- Multi-service lifecycle management

### 5. Stress Tests
High-volume and edge-case testing:
- High-volume service registration (1000+ services)
- Concurrent service access patterns
- Rapid lifecycle cycling
- Tag system stress testing
- Memory pressure testing
- Async operation scaling

## Running the Benchmarks

### EditMode Tests (Primary)
Most benchmark tests can be run in EditMode through Unity's Test Runner:

1. Open **Window → General → Test Runner**
2. Switch to **EditMode** tab
3. Navigate to the PerformanceTests folder
4. Run individual test classes or methods

### PlayMode Tests (Unity Runtime Features)
Some tests require PlayMode for Unity runtime features:

1. Open **Window → General → Test Runner**
2. Switch to **PlayMode** tab
3. Navigate to the PlayMode folder
4. Run PlayMode-specific benchmarks

**PlayMode tests include:**
- Timeout functionality benchmarks
- MonoBehaviour service lifecycle
- DontDestroyOnLoad service performance
- Scene-based service management
- ServiceKitTimeoutManager stress testing

### Comprehensive Suite
Run the complete benchmark suite:

```csharp
// EditMode comprehensive suite:
ServiceKitBenchmarkSuite.RunAllBenchmarks()

// PlayMode comprehensive suite:
PlayModeComprehensiveBenchmarks.Comprehensive_PlayMode_Benchmark_Suite()
```

This will execute all benchmark categories and generate comprehensive reports.

## Interpreting Results

### Metrics Provided
- **Total Time**: Sum of all iterations
- **Average Time**: Mean execution time per operation
- **Min/Max Time**: Best and worst case performance
- **Median Time**: 50th percentile performance
- **Standard Deviation**: Performance consistency measure
- **Operations/Second**: Throughput measurement

### Performance Baselines
Typical performance expectations (on modern hardware):

| Operation | Expected Range | Notes |
|-----------|----------------|-------|
| Simple Registration | 0.01-0.1ms | Basic service registration |
| Service Resolution | 0.001-0.01ms | Getting ready service |
| Dependency Injection | 0.1-1.0ms | Single dependency |
| Complex Injection | 1-10ms | Multiple dependencies |
| Lifecycle Complete | 0.1-1.0ms | Full register→ready→get→unregister |

### Performance Analysis
- **Averages**: General performance characteristics
- **Medians**: More stable than averages, less affected by outliers
- **Standard Deviation**: Lower values indicate more consistent performance
- **Operations/Second**: Higher values indicate better throughput

## Output and Reporting

### Console Output
Benchmark results are logged to Unity's Console with detailed statistics for each test.

### CSV Export
The comprehensive suite exports results to CSV format:
- Location: `Application.persistentDataPath/ServiceKitBenchmarkResults.csv`
- Includes all metrics for further analysis in Excel/Google Sheets

### Performance Comparison
Use the CSV data to:
- Track performance changes over time
- Compare different Unity versions
- Identify performance regressions
- Optimize based on bottlenecks

## Best Practices

### Running Benchmarks
1. **Consistent Environment**: Run on the same hardware/Unity version
2. **Clean State**: Close other applications during testing
3. **Multiple Runs**: Run benchmarks multiple times for consistency
4. **Garbage Collection**: Framework handles GC, but be aware of its impact

### Interpreting Results
1. **Focus on Medians**: More representative than averages
2. **Consider Standard Deviation**: High values indicate inconsistent performance
3. **Context Matters**: Results depend on hardware, Unity version, and system load
4. **Relative Comparison**: Compare relative performance between operations

### Performance Optimization
1. **Identify Bottlenecks**: Focus on slowest operations first
2. **Batch Operations**: Group similar operations when possible
3. **Async Patterns**: Use async methods for non-blocking operations
4. **Memory Management**: Monitor GC impact on performance

## Contributing

When adding new benchmarks:

1. **Follow Patterns**: Use existing benchmark structure
2. **Meaningful Names**: Clear, descriptive benchmark names
3. **Proper Cleanup**: Always dispose resources in teardown
4. **Documentation**: Update this README with new test descriptions
5. **Baseline Data**: Provide expected performance ranges

## Limitations

- **Unity Environment**: Results specific to Unity runtime
- **Platform Dependent**: Performance varies across platforms
- **Synthetic Workload**: May not reflect real-world usage patterns
- **No Profiler Integration**: Use Unity Profiler for deeper analysis

## Future Enhancements

- Integration with Unity Profiler API
- Platform-specific benchmark variations
- Memory allocation tracking
- Performance regression detection
- Automated performance CI/CD integration