using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nonatomic.ServiceKit;
using Nonatomic.ServiceKit.Tests.PerformanceTests.BenchmarkFramework;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#endif

namespace Nonatomic.ServiceKit.Tests.PlayMode
{
	[TestFixture]
	public class PlayModeComprehensiveBenchmarks
	{
		private BenchmarkRunner _benchmarkRunner;
		
		[SetUp]
		public void Setup()
		{
			_benchmarkRunner = new BenchmarkRunner
			{
				WarmupIterations = 3,
				BenchmarkIterations = 50 // Reduced for PlayMode stability
			};
		}
		
		/**
		 * 
		 */
		[UnityTest]
		public IEnumerator Comprehensive_PlayMode_Benchmark_Suite()
		{
			var allResults = new List<BenchmarkResult>();
			
			// 1. Timeout functionality benchmark
			yield return RunTimeoutBenchmarkCoroutine((result) => 
			{
				if (result != null) allResults.Add(result);
			});
			
			// 2. MonoBehaviour service lifecycle
			yield return RunMonoBehaviourBenchmarkCoroutine((result) => 
			{
				if (result != null) allResults.Add(result);
			});
			
			// 3. DontDestroyOnLoad services
			yield return RunDontDestroyOnLoadBenchmarkCoroutine((result) => 
			{
				if (result != null) allResults.Add(result);
			});
			
			// 4. Scene-based service management
			yield return RunSceneServiceBenchmarkCoroutine((result) => 
			{
				if (result != null) allResults.Add(result);
			});
			
			// 5. ServiceKitTimeoutManager stress test
			yield return RunTimeoutManagerStressTestCoroutine((result) => 
			{
				if (result != null) allResults.Add(result);
			});
			
			// Print comprehensive results
			PrintComprehensivePlayModeResults(allResults);
		}
		
		private IEnumerator RunTimeoutBenchmarkCoroutine(System.Action<BenchmarkResult> onResult)
		{
			var timer = new BenchmarkTimer();
			var locator = ScriptableObject.CreateInstance<ServiceKitLocator>();
			var target = new ServiceWithSingleDependency();
			
			try
			{
				locator.RegisterAndReadyService<IDependencyA>(new DependencyA());
				
				// Run benchmark iterations
				for (int i = 0; i < _benchmarkRunner.BenchmarkIterations; i++)
				{
					timer.Start();
					
#if SERVICEKIT_UNITASK
					var operation = locator.InjectServicesAsync(target)
						.WithTimeout(5.0f)
						.ExecuteAsync();
					yield return operation.ToCoroutine();
#else
					var operation = locator.InjectServicesAsync(target)
						.WithTimeout(5.0f)
						.ExecuteAsync();
					yield return new WaitUntil(() => operation.IsCompleted);
#endif
					
					timer.Stop();
					Assert.IsNotNull(target.DependencyA);
				}
				
				var result = timer.GetResult("PlayMode - Injection with Timeout", _benchmarkRunner.BenchmarkIterations);
				onResult(result);
			}
			finally
			{
				if (locator != null)
					UnityEngine.Object.DestroyImmediate(locator);
			}
		}
		
		private IEnumerator RunMonoBehaviourBenchmarkCoroutine(System.Action<BenchmarkResult> onResult)
		{
			var timer = new BenchmarkTimer();
			var go = new GameObject("BenchmarkService");
			var service = go.AddComponent<TestMonoBehaviourService>();
			var locator = ScriptableObject.CreateInstance<ServiceKitLocator>();
			
			try
			{
				// Run benchmark iterations
				for (int i = 0; i < _benchmarkRunner.BenchmarkIterations; i++)
				{
					timer.Start();
					
					locator.RegisterAndReadyService<ITestMonoBehaviourService>(service);
					var retrieved = locator.GetService<ITestMonoBehaviourService>();
					Assert.IsNotNull(retrieved);
					locator.UnregisterService<ITestMonoBehaviourService>();
					
					timer.Stop();
					
					yield return null; // Allow Unity to process frame
				}
				
				var result = timer.GetResult("PlayMode - MonoBehaviour Service Registration", _benchmarkRunner.BenchmarkIterations);
				onResult(result);
			}
			finally
			{
				if (go != null)
					UnityEngine.Object.DestroyImmediate(go);
				if (locator != null)
					UnityEngine.Object.DestroyImmediate(locator);
			}
		}
		
		private IEnumerator RunDontDestroyOnLoadBenchmarkCoroutine(System.Action<BenchmarkResult> onResult)
		{
			var timer = new BenchmarkTimer();
			var go = new GameObject("DontDestroyService");
			UnityEngine.Object.DontDestroyOnLoad(go);
			var service = go.AddComponent<TestMonoBehaviourService>();
			var locator = ScriptableObject.CreateInstance<ServiceKitLocator>();
			
			try
			{
				// Run benchmark iterations
				for (int i = 0; i < _benchmarkRunner.BenchmarkIterations; i++)
				{
					timer.Start();
					
					locator.RegisterAndReadyService<ITestMonoBehaviourService>(service);
					
					var allServices = locator.GetAllServices();
					bool foundDontDestroy = false;
					foreach (var serviceInfo in allServices)
					{
						if (serviceInfo.IsDontDestroyOnLoad)
						{
							foundDontDestroy = true;
							break;
						}
					}
					Assert.IsTrue(foundDontDestroy, "Should find DontDestroyOnLoad service");
					
					locator.UnregisterService<ITestMonoBehaviourService>();
					
					timer.Stop();
					
					yield return null;
				}
				
				var result = timer.GetResult("PlayMode - DontDestroyOnLoad Service", _benchmarkRunner.BenchmarkIterations);
				onResult(result);
			}
			finally
			{
				if (go != null)
					UnityEngine.Object.DestroyImmediate(go);
				if (locator != null)
					UnityEngine.Object.DestroyImmediate(locator);
			}
		}
		
		private IEnumerator RunSceneServiceBenchmarkCoroutine(System.Action<BenchmarkResult> onResult)
		{
			var timer = new BenchmarkTimer();
			var go = new GameObject("SceneService");
			var service = go.AddComponent<TestMonoBehaviourService>();
			var locator = ScriptableObject.CreateInstance<ServiceKitLocator>();
			
			try
			{
				// Run benchmark iterations
				for (int i = 0; i < _benchmarkRunner.BenchmarkIterations; i++)
				{
					timer.Start();
					
					locator.RegisterAndReadyService<ITestMonoBehaviourService>(service);
					
					var currentScene = SceneManager.GetActiveScene();
					var sceneServices = locator.GetServicesInScene(currentScene.name);
					
					Assert.GreaterOrEqual(sceneServices.Count, 1);
					
					locator.CleanupDestroyedServices();
					locator.UnregisterService<ITestMonoBehaviourService>();
					
					timer.Stop();
					
					yield return null;
				}
				
				var result = timer.GetResult("PlayMode - Scene Service Management", _benchmarkRunner.BenchmarkIterations);
				onResult(result);
			}
			finally
			{
				if (go != null)
					UnityEngine.Object.DestroyImmediate(go);
				if (locator != null)
					UnityEngine.Object.DestroyImmediate(locator);
			}
		}
		
		private IEnumerator RunTimeoutManagerStressTestCoroutine(System.Action<BenchmarkResult> onResult)
		{
			var timer = new BenchmarkTimer();
			var ctsList = new List<CancellationTokenSource>();
			
			try
			{
				// Run benchmark iterations
				for (int i = 0; i < _benchmarkRunner.BenchmarkIterations; i++)
				{
					timer.Start();
					
					var registrations = new List<IDisposable>();
					
					for (int j = 0; j < 5; j++)
					{
						var cts = new CancellationTokenSource();
						ctsList.Add(cts);
						
						var registration = ServiceKitTimeoutManager.Instance.RegisterTimeout(cts, 10.0f);
						registrations.Add(registration);
					}
					
					yield return null; // Small delay to test the system
					
					foreach (var registration in registrations)
					{
						registration?.Dispose();
					}
					
					timer.Stop();
				}
				
				var result = timer.GetResult("PlayMode - TimeoutManager Stress Test", _benchmarkRunner.BenchmarkIterations);
				onResult(result);
			}
			finally
			{
				foreach (var cts in ctsList)
				{
					cts?.Dispose();
				}
			}
		}
		
		private void PrintComprehensivePlayModeResults(List<BenchmarkResult> results)
		{
			UnityEngine.Debug.Log("=== PlayMode Comprehensive Benchmark Results ===");
			
			foreach (var result in results)
			{
				UnityEngine.Debug.Log($"\n{result.Name}:");
				UnityEngine.Debug.Log($"  Average: {result.AverageTimeMs:F3}ms");
				UnityEngine.Debug.Log($"  Throughput: {result.OperationsPerSecond:F0} ops/sec");
				UnityEngine.Debug.Log($"  Min: {result.MinTimeMs:F3}ms | Max: {result.MaxTimeMs:F3}ms");
				UnityEngine.Debug.Log($"  Median: {result.MedianTimeMs:F3}ms | StdDev: {result.StandardDeviationMs:F3}ms");
			}
			
			UnityEngine.Debug.Log("\n=== PlayMode Performance Summary ===");
			
			if (results.Count > 0)
			{
				var fastest = results[0];
				var slowest = results[0];
				
				foreach (var result in results)
				{
					if (result.AverageTimeMs < fastest.AverageTimeMs)
						fastest = result;
					if (result.AverageTimeMs > slowest.AverageTimeMs)
						slowest = result;
				}
				
				UnityEngine.Debug.Log($"Fastest: {fastest.Name} ({fastest.AverageTimeMs:F3}ms)");
				UnityEngine.Debug.Log($"Slowest: {slowest.Name} ({slowest.AverageTimeMs:F3}ms)");
				UnityEngine.Debug.Log($"Total Tests: {results.Count}");
			}
		}
		
		// Test interfaces and implementations
		private interface IDependencyA { }
		private class DependencyA : IDependencyA { }
		
		private interface ITestMonoBehaviourService 
		{
			string GetInfo();
		}
		
		private class TestMonoBehaviourService : MonoBehaviour, ITestMonoBehaviourService 
		{
			public string GetInfo() => $"Service on {gameObject.name}";
		}
		
		private class ServiceWithSingleDependency
		{
			[InjectService] private IDependencyA _dependencyA;
			public IDependencyA DependencyA => _dependencyA;
		}
	}
}