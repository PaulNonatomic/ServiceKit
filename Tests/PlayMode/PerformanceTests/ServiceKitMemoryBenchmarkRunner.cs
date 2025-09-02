using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Profiling;

namespace Nonatomic.ServiceKit.Tests.PlayMode
{
	/// <summary>
	/// Comprehensive memory benchmark runner for ServiceKit
	/// </summary>
	public class ServiceKitMemoryBenchmarkRunner
	{
		private const string ResultsFileName = "ServiceKitMemoryBenchmarkResults.csv";
		private const int StandardIterations = 1000;
		private const int HeavyIterations = 100;
		
		[UnityTest]
		public IEnumerator RunComprehensiveMemoryBenchmarks()
		{
			Debug.Log("=== ServiceKit Memory Performance Benchmarks ===");
			Debug.Log($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
			Debug.Log($"Platform: {Application.platform}");
			Debug.Log($"Unity Version: {Application.unityVersion}");
			
			var results = new List<BenchmarkResult>();
			var locator = ScriptableObject.CreateInstance<ServiceKitLocator>();
			
			// Setup test services
			SetupTestServices(locator);
			yield return null;
			
			// Run benchmarks
			results.AddRange(RunServiceResolutionBenchmarks(locator));
			yield return null;
			
			results.AddRange(RunServiceRegistrationBenchmarks(locator));
			yield return null;
			
			results.AddRange(RunTagOperationBenchmarks(locator));
			yield return null;
			
			results.AddRange(RunCollectionBenchmarks(locator));
			yield return null;
			
			// Run async operation benchmarks
			yield return RunAsyncBenchmarks(locator, results);
			
			// Print and save results
			PrintResults(results);
			SaveResults(results);
			
			// Cleanup
			locator.ClearServices();
			UnityEngine.Object.DestroyImmediate(locator);
			
			Debug.Log("=== Benchmarks Complete ===");
		}
		
		private void SetupTestServices(ServiceKitLocator locator)
		{
			// Register various services for testing
			for (int i = 0; i < 50; i++)
			{
				var service = new TestService { Id = i, Name = $"Service_{i}" };
				locator.RegisterAndReadyService<ITestService>(service);
				
				// Add tags
				if (i % 2 == 0)
				{
					locator.AddTagsToService<ITestService>(
						new ServiceTag { name = "even" },
						new ServiceTag { name = $"group_{i / 10}" }
					);
				}
			}
		}
		
		private List<BenchmarkResult> RunServiceResolutionBenchmarks(ServiceKitLocator locator)
		{
			Debug.Log("Running Service Resolution Benchmarks...");
			var results = new List<BenchmarkResult>();
			
			// GetService<T>
			results.Add(MeasureOperation(
				"GetService<T>",
				() => locator.GetService<ITestService>(),
				StandardIterations * 10
			));
			
			// TryGetService<T>
			results.Add(MeasureOperation(
				"TryGetService<T>",
				() => locator.TryGetService<ITestService>(out _),
				StandardIterations * 10
			));
			
			// IsServiceReady<T>
			results.Add(MeasureOperation(
				"IsServiceReady<T>",
				() => locator.IsServiceReady<ITestService>(),
				StandardIterations * 10
			));
			
			// IsServiceRegistered<T>
			results.Add(MeasureOperation(
				"IsServiceRegistered<T>",
				() => locator.IsServiceRegistered<ITestService>(),
				StandardIterations * 10
			));
			
			// GetService for non-existent
			results.Add(MeasureOperation(
				"GetService<T> (not found)",
				() => locator.GetService<INonExistentService>(),
				StandardIterations
			));
			
			return results;
		}
		
		private List<BenchmarkResult> RunServiceRegistrationBenchmarks(ServiceKitLocator locator)
		{
			Debug.Log("Running Service Registration Benchmarks...");
			var results = new List<BenchmarkResult>();
			var tempServices = new List<TestService>();
			
			// Pre-create services
			for (int i = 0; i < HeavyIterations; i++)
			{
				tempServices.Add(new TestService { Id = 1000 + i, Name = $"Temp_{i}" });
			}
			
			// RegisterService
			results.Add(MeasureOperation(
				"RegisterService<T>",
				() => {
					for (int i = 0; i < tempServices.Count; i++)
					{
						locator.RegisterService<ITestServiceB>(tempServices[i]);
					}
					// Cleanup
					for (int i = 0; i < tempServices.Count; i++)
					{
						locator.UnregisterService<ITestServiceB>();
					}
				},
				1
			));
			
			// RegisterAndReadyService
			results.Add(MeasureOperation(
				"RegisterAndReadyService<T>",
				() => {
					for (int i = 0; i < tempServices.Count; i++)
					{
						locator.RegisterAndReadyService<ITestServiceC>(tempServices[i]);
					}
					// Cleanup
					for (int i = 0; i < tempServices.Count; i++)
					{
						locator.UnregisterService<ITestServiceC>();
					}
				},
				1
			));
			
			return results;
		}
		
		private List<BenchmarkResult> RunTagOperationBenchmarks(ServiceKitLocator locator)
		{
			Debug.Log("Running Tag Operation Benchmarks...");
			var results = new List<BenchmarkResult>();
			
			// GetServicesWithTag
			results.Add(MeasureOperation(
				"GetServicesWithTag",
				() => locator.GetServicesWithTag("even"),
				StandardIterations
			));
			
			// GetServicesWithAnyTag
			results.Add(MeasureOperation(
				"GetServicesWithAnyTag",
				() => locator.GetServicesWithAnyTag("even", "odd"),
				StandardIterations
			));
			
			// GetServicesWithAllTags
			results.Add(MeasureOperation(
				"GetServicesWithAllTags",
				() => locator.GetServicesWithAllTags("even", "group_1"),
				StandardIterations
			));
			
			// GetServiceTags
			results.Add(MeasureOperation(
				"GetServiceTags<T>",
				() => locator.GetServiceTags<ITestService>(),
				StandardIterations
			));
			
			return results;
		}
		
		private List<BenchmarkResult> RunCollectionBenchmarks(ServiceKitLocator locator)
		{
			Debug.Log("Running Collection Operation Benchmarks...");
			var results = new List<BenchmarkResult>();
			
			// GetAllServices
			results.Add(MeasureOperation(
				"GetAllServices",
				() => locator.GetAllServices(),
				StandardIterations
			));
			
			// GetServicesInScene
			results.Add(MeasureOperation(
				"GetServicesInScene",
				() => locator.GetServicesInScene("TestScene"),
				StandardIterations
			));
			
			// GetDontDestroyOnLoadServices
			results.Add(MeasureOperation(
				"GetDontDestroyOnLoadServices",
				() => locator.GetDontDestroyOnLoadServices(),
				StandardIterations
			));
			
			return results;
		}
		
		private IEnumerator RunAsyncBenchmarks(ServiceKitLocator locator, List<BenchmarkResult> results)
		{
			Debug.Log("Running Async Operation Benchmarks...");
			
			// Measure TimeoutManager Update loop
			var manager = ServiceKitTimeoutManager.Instance;
			var sources = new List<System.Threading.CancellationTokenSource>();
			
			// Register some timeouts
			for (int i = 0; i < 20; i++)
			{
				var cts = new System.Threading.CancellationTokenSource();
				sources.Add(cts);
				manager.RegisterTimeout(cts, 100f);
			}
			
			// Measure Update over multiple frames
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			
			var startMemory = GC.GetTotalMemory(false);
			var frameCount = 60;
			
			for (int i = 0; i < frameCount; i++)
			{
				yield return null;
			}
			
			var endMemory = GC.GetTotalMemory(false);
			var bytesPerFrame = (endMemory - startMemory) / (double)frameCount;
			
			results.Add(new BenchmarkResult
			{
				OperationName = "TimeoutManager.Update (per frame)",
				BytesAllocated = endMemory - startMemory,
				BytesPerOperation = bytesPerFrame,
				Iterations = frameCount
			});
			
			// Cleanup
			foreach (var cts in sources)
			{
				cts.Cancel();
				cts.Dispose();
			}
			ServiceKitTimeoutManager.Cleanup();
		}
		
		private BenchmarkResult MeasureOperation(string name, Action operation, int iterations)
		{
			// Warmup
			for (int i = 0; i < 10; i++)
			{
				operation();
			}
			
			// Force GC
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			
			var startMemory = GC.GetTotalMemory(false);
			var startTime = Time.realtimeSinceStartup;
			
			// Run operation
			for (int i = 0; i < iterations; i++)
			{
				operation();
			}
			
			var endTime = Time.realtimeSinceStartup;
			var endMemory = GC.GetTotalMemory(false);
			
			return new BenchmarkResult
			{
				OperationName = name,
				BytesAllocated = Math.Max(0, endMemory - startMemory),
				BytesPerOperation = Math.Max(0, (endMemory - startMemory) / (double)iterations),
				Iterations = iterations,
				TotalTimeMs = (endTime - startTime) * 1000
			};
		}
		
		private void PrintResults(List<BenchmarkResult> results)
		{
			Debug.Log("=== Memory Benchmark Results ===");
			
			foreach (var result in results)
			{
				var status = GetStatusEmoji(result.BytesPerOperation);
				Debug.Log($"{status} {result.OperationName}: {result.BytesPerOperation:F2} bytes/op " +
						 $"(Total: {result.BytesAllocated:N0} bytes, {result.Iterations} iterations, {result.TotalTimeMs:F2}ms)");
			}
			
			// Summary statistics
			var zeroAllocation = results.FindAll(r => r.BytesPerOperation == 0).Count;
			var minimal = results.FindAll(r => r.BytesPerOperation > 0 && r.BytesPerOperation <= 50).Count;
			var moderate = results.FindAll(r => r.BytesPerOperation > 50 && r.BytesPerOperation <= 200).Count;
			var high = results.FindAll(r => r.BytesPerOperation > 200).Count;
			
			Debug.Log($"\nSummary:");
			Debug.Log($"  Zero Allocation: {zeroAllocation} operations");
			Debug.Log($"  Minimal (<50 bytes): {minimal} operations");
			Debug.Log($"  Moderate (50-200 bytes): {moderate} operations");
			Debug.Log($"  High (>200 bytes): {high} operations");
		}
		
		private string GetStatusEmoji(double bytesPerOp)
		{
			if (bytesPerOp == 0) return "✅";
			if (bytesPerOp <= 50) return "🟢";
			if (bytesPerOp <= 200) return "🟡";
			return "🔴";
		}
		
		private void SaveResults(List<BenchmarkResult> results)
		{
			var path = Path.Combine(Application.persistentDataPath, ResultsFileName);
			
			using (var writer = new StreamWriter(path))
			{
				writer.WriteLine("Timestamp,Operation,BytesPerOp,TotalBytes,Iterations,TimeMs,Status");
				
				foreach (var result in results)
				{
					var status = result.BytesPerOperation == 0 ? "ZeroAlloc" :
								result.BytesPerOperation <= 50 ? "Minimal" :
								result.BytesPerOperation <= 200 ? "Moderate" : "High";
					
					writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}," +
									$"{result.OperationName}," +
									$"{result.BytesPerOperation:F2}," +
									$"{result.BytesAllocated}," +
									$"{result.Iterations}," +
									$"{result.TotalTimeMs:F2}," +
									$"{status}");
				}
			}
			
			Debug.Log($"Results saved to: {path}");
		}
		
		private class BenchmarkResult
		{
			public string OperationName { get; set; }
			public long BytesAllocated { get; set; }
			public double BytesPerOperation { get; set; }
			public int Iterations { get; set; }
			public double TotalTimeMs { get; set; }
		}
		
		private interface ITestService { }
		private interface ITestServiceB { }
		private interface ITestServiceC { }
		private interface INonExistentService { }
		
		private class TestService : ITestService, ITestServiceB, ITestServiceC
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}
	}
}