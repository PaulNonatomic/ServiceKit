using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nonatomic.ServiceKit.Tests.PerformanceTests.BenchmarkFramework;
using NUnit.Framework;
using UnityEngine;

namespace Nonatomic.ServiceKit.Tests.PerformanceTests.Suites
{
	/// <summary>
	/// Comprehensive ServiceKit Benchmark Suite
	/// 
	/// Performance Results Summary (50 iterations):
	/// 
	/// Resolution Performance:
	/// - TryGetService: 0.004ms (245,700 ops/sec) - Fastest operation
	/// - GetService - Synchronous: 0.010ms (~103,000 ops/sec)
	/// 
	/// Registration Performance:
	/// - RegisterService - Simple: ~0.594ms (~1,686 ops/sec)
	/// - RegisterService - With Tags: ~0.600ms (~1,666 ops/sec)
	/// - RegisterAndReadyService: ~1.196ms (~837 ops/sec)
	/// 
	/// Lifecycle Performance:
	/// - Complete Service Lifecycle: 1.722ms (581 ops/sec)
	/// 
	/// Scalability Performance:
	/// - Register 10 Services: 17.152ms (58 ops/sec)
	/// - Register 25 Services: 43.955ms (23 ops/sec)
	/// - Register 50 Services: 91.096ms (11 ops/sec)
	/// 
	/// Memory Performance:
	/// - Memory Allocation - Service Creation: 65.429ms (15 ops/sec)
	/// </summary>
	[TestFixture]
	public class ServiceKitBenchmarkSuite
	{
		private BenchmarkRunner _benchmarkRunner;
		private List<BenchmarkResult> _allResults;
		
		[OneTimeSetUp]
		public void OneTimeSetUp()
		{
			_benchmarkRunner = new BenchmarkRunner
			{
				WarmupIterations = 5,
				BenchmarkIterations = 50  // Reduced from 1000 to prevent Unity from hanging
			};
			_allResults = new List<BenchmarkResult>();
			
			Debug.Log("=== ServiceKit Benchmark Suite Starting ===");
		}
		
		[OneTimeTearDown]
		public void OneTimeTearDown()
		{
			PrintComprehensiveReport();
			ExportResultsToCSV();
		}
		
		/// <summary>
		/// Runs comprehensive ServiceKit benchmarks across all categories
		/// Total benchmarks executed: 15
		/// Fastest operation: TryGetService (0.004ms)
		/// Slowest operation: Register 50 Services (91.096ms)
		/// </summary>
		[Test, Order(1)]
		public void RunAllBenchmarks()
		{
			Debug.Log("Running comprehensive ServiceKit benchmarks...");
			
			// Run all benchmark categories
			RunRegistrationBenchmarks();
			RunResolutionBenchmarks();
			RunInjectionBenchmarks();
			RunLifecycleBenchmarks();
			RunScalabilityBenchmarks();
			RunMemoryBenchmarks();
		}
		
		[Test, Order(2)]
		public void RunQuickBenchmarks()
		{
			Debug.Log("Running quick ServiceKit benchmarks (reduced iterations)...");
			
			// Set even lower iterations for quick test
			_benchmarkRunner.WarmupIterations = 2;
			_benchmarkRunner.BenchmarkIterations = 10;
			
			// Run only essential benchmarks
			RunRegistrationBenchmarks();
			RunResolutionBenchmarks();
			
			// Reset iterations
			_benchmarkRunner.WarmupIterations = 5;
			_benchmarkRunner.BenchmarkIterations = 50;
		}
		
		private void RunRegistrationBenchmarks()
		{
			Debug.Log("--- Registration Benchmarks ---");
			
			// Simple registration
			_benchmarkRunner.RunWithSetup(
				"RegisterService - Simple",
				() => ScriptableObject.CreateInstance<ServiceKitLocator>(),
				(locator) =>
				{
					var service = new TestService();
					locator.RegisterService<ITestService>(service);
				},
				(locator) => UnityEngine.Object.DestroyImmediate(locator)
			);
			
			// Registration with tags
			var tags = new[] { new ServiceTag("benchmark"), new ServiceTag("test") };
			_benchmarkRunner.RunWithSetup(
				"RegisterService - With Tags",
				() => ScriptableObject.CreateInstance<ServiceKitLocator>(),
				(locator) =>
				{
					var service = new TestService();
					locator.RegisterService<ITestService>(service, tags);
				},
				(locator) => UnityEngine.Object.DestroyImmediate(locator)
			);
			
			// Register and ready
			_benchmarkRunner.RunWithSetup(
				"RegisterAndReadyService",
				() => ScriptableObject.CreateInstance<ServiceKitLocator>(),
				(locator) =>
				{
					var service = new TestService();
					locator.RegisterAndReadyService<ITestService>(service);
				},
				(locator) => UnityEngine.Object.DestroyImmediate(locator)
			);
			
			_allResults.AddRange(_benchmarkRunner.GetResults());
			_benchmarkRunner.Clear();
		}
		
		private void RunResolutionBenchmarks()
		{
			Debug.Log("--- Resolution Benchmarks ---");
			
			// Synchronous get
			_benchmarkRunner.RunWithSetup(
				"GetService - Synchronous",
				() =>
				{
					var locator = ScriptableObject.CreateInstance<ServiceKitLocator>();
					locator.RegisterAndReadyService<ITestService>(new TestService());
					return locator;
				},
				(locator) =>
				{
					var result = locator.GetService<ITestService>();
					Assert.IsNotNull(result);
				},
				(locator) => UnityEngine.Object.DestroyImmediate(locator)
			);
			
			// TryGet
			_benchmarkRunner.RunWithSetup(
				"TryGetService",
				() =>
				{
					var locator = ScriptableObject.CreateInstance<ServiceKitLocator>();
					locator.RegisterAndReadyService<ITestService>(new TestService());
					return locator;
				},
				(locator) =>
				{
					bool found = locator.TryGetService<ITestService>(out var result);
					Assert.IsTrue(found);
				},
				(locator) => UnityEngine.Object.DestroyImmediate(locator)
			);
			
			_allResults.AddRange(_benchmarkRunner.GetResults());
			_benchmarkRunner.Clear();
		}
		
		private void RunInjectionBenchmarks()
		{
			Debug.Log("--- Injection Benchmarks ---");
			
			// Note: These would need to be async, but for simplicity keeping sync here
			// In real implementation, you'd use the async versions
			
			_allResults.AddRange(_benchmarkRunner.GetResults());
			_benchmarkRunner.Clear();
		}
		
		private void RunLifecycleBenchmarks()
		{
			Debug.Log("--- Lifecycle Benchmarks ---");
			
			// Complete lifecycle
			_benchmarkRunner.Run(
				"Complete Service Lifecycle",
				() =>
				{
					var locator = ScriptableObject.CreateInstance<ServiceKitLocator>();
					var service = new TestService();
					
					locator.RegisterService<ITestService>(service);
					locator.ReadyService<ITestService>();
					var result = locator.GetService<ITestService>();
					locator.UnregisterService<ITestService>();
					
					Assert.IsNotNull(result);
					UnityEngine.Object.DestroyImmediate(locator);
				}
			);
			
			_allResults.AddRange(_benchmarkRunner.GetResults());
			_benchmarkRunner.Clear();
		}
		
		private void RunScalabilityBenchmarks()
		{
			Debug.Log("--- Scalability Benchmarks ---");
			
			// Test with different service counts - reduced for performance
			int[] serviceCounts = { 10, 25, 50 };
			
			foreach (int count in serviceCounts)
			{
				_benchmarkRunner.RunWithSetup(
					$"Register {count} Services",
					() => ScriptableObject.CreateInstance<ServiceKitLocator>(),
					(locator) =>
					{
						for (int i = 0; i < count; i++)
						{
							locator.RegisterAndReadyService<ITestService>(new TestService());
							if (i < count - 1)
								locator.UnregisterService<ITestService>();
						}
						
						// Verify the last service
						Assert.IsNotNull(locator.GetService<ITestService>());
					},
					(locator) => UnityEngine.Object.DestroyImmediate(locator)
				);
			}
			
			_allResults.AddRange(_benchmarkRunner.GetResults());
			_benchmarkRunner.Clear();
		}
		
		private void RunMemoryBenchmarks()
		{
			Debug.Log("--- Memory Benchmarks ---");
			
			// Memory allocation test
			_benchmarkRunner.Run(
				"Memory Allocation - Service Creation",
				() =>
				{
					var locator = ScriptableObject.CreateInstance<ServiceKitLocator>();
					
					// Force garbage collection before measurement
					GC.Collect();
					GC.WaitForPendingFinalizers();
					GC.Collect();
					
					var service = new TestService();
					locator.RegisterAndReadyService<ITestService>(service);
					var retrieved = locator.GetService<ITestService>();
					
					Assert.IsNotNull(retrieved);
					UnityEngine.Object.DestroyImmediate(locator);
				}
			);
			
			_allResults.AddRange(_benchmarkRunner.GetResults());
			_benchmarkRunner.Clear();
		}
		
		private void PrintComprehensiveReport()
		{
			Debug.Log("=== ServiceKit Benchmark Suite Results ===");
			
			var categories = _allResults
				.GroupBy(r => GetBenchmarkCategory(r.Name))
				.OrderBy(g => g.Key);
			
			foreach (var category in categories)
			{
				Debug.Log($"\n--- {category.Key} ---");
				
				var sortedResults = category.OrderBy(r => r.AverageTimeMs);
				
				foreach (var result in sortedResults)
				{
					Debug.Log($"{result.Name}:");
					Debug.Log($"  Avg: {result.AverageTimeMs:F3}ms");
					Debug.Log($"  Ops/sec: {result.OperationsPerSecond:F0}");
					Debug.Log($"  Min: {result.MinTimeMs:F3}ms | Max: {result.MaxTimeMs:F3}ms");
				}
			}
			
			// Performance summary
			Debug.Log("\n=== Performance Summary ===");
			var fastestOverall = _allResults.OrderBy(r => r.AverageTimeMs).First();
			var slowestOverall = _allResults.OrderByDescending(r => r.AverageTimeMs).First();
			
			Debug.Log($"Fastest Operation: {fastestOverall.Name} ({fastestOverall.AverageTimeMs:F3}ms)");
			Debug.Log($"Slowest Operation: {slowestOverall.Name} ({slowestOverall.AverageTimeMs:F3}ms)");
			Debug.Log($"Total Benchmarks Run: {_allResults.Count}");
		}
		
		private string GetBenchmarkCategory(string benchmarkName)
		{
			if (benchmarkName.Contains("Register")) return "Registration";
			if (benchmarkName.Contains("GetService") || benchmarkName.Contains("TryGet")) return "Resolution";
			if (benchmarkName.Contains("Inject")) return "Injection";
			if (benchmarkName.Contains("Lifecycle")) return "Lifecycle";
			if (benchmarkName.Contains("Services")) return "Scalability";
			if (benchmarkName.Contains("Memory")) return "Memory";
			return "Other";
		}
		
		private void ExportResultsToCSV()
		{
			try
			{
				var csvPath = Path.Combine(Application.persistentDataPath, "ServiceKitBenchmarkResults.csv");
				
				using (var writer = new StreamWriter(csvPath))
				{
					// Write header
					writer.WriteLine("Category,Benchmark,Iterations,AvgTimeMs,MinTimeMs,MaxTimeMs,MedianTimeMs,StdDevMs,OpsPerSec");
					
					// Write data
					foreach (var result in _allResults)
					{
						var category = GetBenchmarkCategory(result.Name);
						writer.WriteLine($"{category},{result.Name},{result.Iterations}," +
							$"{result.AverageTimeMs:F3},{result.MinTimeMs:F3},{result.MaxTimeMs:F3}," +
							$"{result.MedianTimeMs:F3},{result.StandardDeviationMs:F3},{result.OperationsPerSecond:F0}");
					}
				}
				
				Debug.Log($"Benchmark results exported to: {csvPath}");
			}
			catch (Exception ex)
			{
				Debug.LogError($"Failed to export benchmark results: {ex.Message}");
			}
		}
		
		// Test interfaces and implementations
		private interface ITestService { }
		private class TestService : ITestService { }
	}
}