using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Nonatomic.ServiceKit;
using UnityEngine.Profiling;

namespace Nonatomic.ServiceKit.Tests.PlayMode
{
	public class ServiceKitMemoryPerformanceTests
	{
		private ServiceKitLocator _locator;
		private MemoryAllocationTracker _tracker;
		private List<TestService> _testServices;
		private const int WarmupIterations = 10;
		private const int TestIterations = 1000;
		
		private interface ITestService { void DoWork(); }
		private interface ITestServiceB { void DoWork(); }
		private interface ITestServiceC { void DoWork(); }
		
		private class TestService : ITestService, ITestServiceB, ITestServiceC
		{
			public string Name { get; set; }
			public void DoWork() { }
		}
		
		[SetUp]
		public void Setup()
		{
			_locator = ScriptableObject.CreateInstance<ServiceKitLocator>();
			_tracker = new MemoryAllocationTracker();
			_testServices = new List<TestService>();
			
			// Pre-register some services for testing
			for (int i = 0; i < 10; i++)
			{
				var service = new TestService { Name = $"Service_{i}" };
				_testServices.Add(service);
				_locator.RegisterAndReadyService<ITestService>(service);
				
				// Add tags to some services
				if (i % 2 == 0)
				{
					_locator.AddTagsToService<ITestService>(new ServiceTag { name = "even" });
				}
				if (i % 3 == 0)
				{
					_locator.AddTagsToService<ITestService>(new ServiceTag { name = "multiple-of-three" });
				}
			}
		}
		
		[TearDown]
		public void TearDown()
		{
			_locator.ClearServices();
			if (_locator != null)
			{
				UnityEngine.Object.DestroyImmediate(_locator);
			}
		}
		
		[Test]
		public void Measure_GetService_Allocations()
		{
			_tracker.MeasureOperationWithWarmup(
				"GetService<T> - Hot Path",
				() => {
					var service = _locator.GetService<ITestService>();
				},
				WarmupIterations,
				TestIterations
			);
			
			var result = _tracker.GetResults()["GetService<T> - Hot Path"];
			LogResult(result);
			
			// Assert minimal allocations
			Assert.Less(result.BytesPerOperation, 1, "GetService should allocate less than 1 byte per operation");
			Assert.AreEqual(0, result.GCCollections, "GetService should not trigger GC");
		}
		
		[Test]
		public void Measure_TryGetService_Allocations()
		{
			_tracker.MeasureOperationWithWarmup(
				"TryGetService<T>",
				() => {
					_locator.TryGetService<ITestService>(out var service);
				},
				WarmupIterations,
				TestIterations
			);
			
			var result = _tracker.GetResults()["TryGetService<T>"];
			LogResult(result);
			
			Assert.Less(result.BytesPerOperation, 1, "TryGetService should allocate less than 1 byte per operation");
		}
		
		[Test]
		public void Measure_GetAllServices_Allocations()
		{
			_tracker.MeasureOperationWithWarmup(
				"GetAllServices",
				() => {
					var services = _locator.GetAllServices();
				},
				WarmupIterations,
				TestIterations
			);
			
			var result = _tracker.GetResults()["GetAllServices"];
			LogResult(result);
			
			// With pooling, this should have minimal allocations
			Assert.Less(result.BytesPerOperation, 200, "GetAllServices should use pooling effectively");
		}
		
		[Test]
		public void Measure_GetServicesWithTag_Allocations()
		{
			_tracker.MeasureOperationWithWarmup(
				"GetServicesWithTag",
				() => {
					var services = _locator.GetServicesWithTag("even");
				},
				WarmupIterations,
				TestIterations
			);
			
			var result = _tracker.GetResults()["GetServicesWithTag"];
			LogResult(result);
			
			// With optimizations, should have reduced allocations
			Assert.Less(result.BytesPerOperation, 150, "GetServicesWithTag should have minimal allocations");
		}
		
		[Test]
		public void Measure_RegisterService_Allocations()
		{
			var tracker = new MemoryAllocationTracker();
			var tempServices = new List<TestService>();
			
			// Create services outside of measurement
			for (int i = 0; i < TestIterations; i++)
			{
				tempServices.Add(new TestService { Name = $"TempService_{i}" });
			}
			
			tracker.StartTracking();
			for (int i = 0; i < TestIterations; i++)
			{
				_locator.RegisterService<ITestServiceB>(tempServices[i]);
			}
			var result = tracker.StopTracking("RegisterService", TestIterations);
			
			LogResult(result);
			
			// Clean up
			foreach (var service in tempServices)
			{
				_locator.UnregisterService<ITestServiceB>();
			}
			
			// Registration will have some allocations for dictionary entries
			Assert.Less(result.BytesPerOperation, 500, "RegisterService should have reasonable allocations");
		}
		
		[Test]
		public void Measure_ServiceInjection_Allocations()
		{
			var target = new InjectionTarget();
			
			_tracker.MeasureOperationWithWarmup(
				"ServiceInjection",
				() => {
					var builder = _locator.InjectServicesAsync(target);
					// Note: We're just measuring builder creation, not async execution
				},
				WarmupIterations,
				TestIterations
			);
			
			var result = _tracker.GetResults()["ServiceInjection"];
			LogResult(result);
			
			// Builder creation should be lightweight
			Assert.Less(result.BytesPerOperation, 200, "ServiceInjection builder should have minimal allocations");
		}
		
		[Test]
		public void Measure_GetServiceTags_Allocations()
		{
			_tracker.MeasureOperationWithWarmup(
				"GetServiceTags",
				() => {
					var tags = _locator.GetServiceTags<ITestService>();
				},
				WarmupIterations,
				TestIterations
			);
			
			var result = _tracker.GetResults()["GetServiceTags"];
			LogResult(result);
			
			// With optimizations, should avoid LINQ allocations
			Assert.Less(result.BytesPerOperation, 100, "GetServiceTags should have minimal allocations");
		}
		
		[Test]
		public void Measure_IsServiceReady_Allocations()
		{
			_tracker.MeasureOperationWithWarmup(
				"IsServiceReady",
				() => {
					var isReady = _locator.IsServiceReady<ITestService>();
				},
				WarmupIterations,
				TestIterations * 10 // More iterations for this lightweight operation
			);
			
			var result = _tracker.GetResults()["IsServiceReady"];
			LogResult(result);
			
			// This should be allocation-free
			Assert.AreEqual(0, result.BytesAllocated, "IsServiceReady should be allocation-free");
		}
		
		[Test]
		public void Compare_BeforeAfter_Optimizations()
		{
			Debug.Log("=== Memory Performance Comparison ===");
			
			// Test critical hot paths
			var operations = new[]
			{
				("GetService", (Action)(() => _locator.GetService<ITestService>())),
				("TryGetService", (Action)(() => _locator.TryGetService<ITestService>(out _))),
				("IsServiceReady", (Action)(() => _locator.IsServiceReady<ITestService>())),
				("GetAllServices", (Action)(() => _locator.GetAllServices())),
				("GetServicesWithTag", (Action)(() => _locator.GetServicesWithTag("even")))
			};
			
			foreach (var (name, operation) in operations)
			{
				_tracker.MeasureOperationWithWarmup(name, operation, 50, 1000);
			}
			
			_tracker.PrintResults();
			
			// Save results to file
			var path = System.IO.Path.Combine(Application.dataPath, "..", "MemoryPerformanceResults.csv");
			_tracker.SaveResultsToCSV(path);
			Debug.Log($"Results saved to: {path}");
		}
		
		[UnityTest]
		public IEnumerator Measure_TimeoutManager_Update_Allocations()
		{
			var manager = ServiceKitTimeoutManager.Instance;
			var sources = new List<System.Threading.CancellationTokenSource>();
			
			// Add some timeouts
			for (int i = 0; i < 10; i++)
			{
				var cts = new System.Threading.CancellationTokenSource();
				sources.Add(cts);
				manager.RegisterTimeout(cts, 10f); // 10 second timeout
			}
			
			yield return null; // Wait a frame
			
			// Measure allocations over multiple frames
			var tracker = new MemoryAllocationTracker();
			tracker.StartTracking();
			
			for (int frame = 0; frame < 60; frame++) // 60 frames
			{
				yield return null;
			}
			
			var result = tracker.StopTracking("TimeoutManager.Update (60 frames)", 60);
			LogResult(result);
			
			// Update loop should have minimal allocations
			Assert.Less(result.BytesPerOperation, 10, "TimeoutManager Update should have minimal per-frame allocations");
			
			// Clean up
			foreach (var cts in sources)
			{
				cts.Cancel();
				cts.Dispose();
			}
			ServiceKitTimeoutManager.Cleanup();
		}
		
		private void LogResult(MemoryAllocationTracker.AllocationResult result)
		{
			Debug.Log($"[Memory Test] {result}");
			
			if (result.BytesPerOperation == 0)
			{
				Debug.Log($"  ✅ ZERO ALLOCATIONS - Excellent!");
			}
			else if (result.BytesPerOperation < 50)
			{
				Debug.Log($"  ✅ Minimal allocations - Very Good");
			}
			else if (result.BytesPerOperation < 200)
			{
				Debug.Log($"  ⚠️ Moderate allocations - Acceptable");
			}
			else
			{
				Debug.LogWarning($"  ❌ High allocations - Needs optimization");
			}
		}
		
		private class InjectionTarget
		{
			[InjectService] private ITestService _service;
			[InjectService(Required = false)] private ITestServiceB _optionalService;
		}
	}
}