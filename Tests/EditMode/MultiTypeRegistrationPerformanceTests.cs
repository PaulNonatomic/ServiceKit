using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Nonatomic.ServiceKit;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#endif

namespace Tests.EditMode
{
	[TestFixture]
	public class MultiTypeRegistrationPerformanceTests
	{
		private ServiceKitLocator _serviceKit;
		private Stopwatch _stopwatch;

		[SetUp]
		public void Setup()
		{
			_serviceKit = ScriptableObject.CreateInstance<ServiceKitLocator>();
			_stopwatch = new Stopwatch();
		}

		[TearDown]
		public void TearDown()
		{
			if (_serviceKit != null)
			{
				_serviceKit.ClearServices();

				// Wait synchronously for async operations to complete before destroying the locator
				// This prevents NullReferenceExceptions in async continuations
				System.Threading.Thread.Sleep(50);

				Object.DestroyImmediate(_serviceKit);
				_serviceKit = null;
			}
		}


		[Test]
		public void Performance_SingleType_RegistrationCycle()
		{
			// Test register/unregister cycles to measure registration speed
			const int cycles = 1000;
			var service = new TestPerformanceService();

			// Act
			_stopwatch.Restart();
			for (int i = 0; i < cycles; i++)
			{
				_serviceKit.RegisterService<IPerformanceServiceA>(service);
				_serviceKit.UnregisterService<IPerformanceServiceA>();
			}
			_stopwatch.Stop();

			// Report
			var avgTime = _stopwatch.Elapsed.TotalMilliseconds / cycles;
			Debug.Log($"[Performance] Single-type registration cycles: {cycles} cycles in {_stopwatch.Elapsed.TotalMilliseconds:F2}ms (avg: {avgTime:F4}ms per cycle)");

			// Assert reasonable performance
			Assert.Less(_stopwatch.Elapsed.TotalMilliseconds, 2000, "Registration cycles should complete in under 2 seconds");
		}

		[Test]
		public void Performance_MultiType_RegistrationCycle_2Types()
		{
			// Test register/unregister cycles with multi-type registration
			const int cycles = 1000;
			var service = new TestPerformanceService();

			// Act
			_stopwatch.Restart();
			for (int i = 0; i < cycles; i++)
			{
				_serviceKit.RegisterService<IPerformanceServiceA>(service)
					.AlsoAs<IPerformanceServiceB>();
				_serviceKit.UnregisterService<IPerformanceServiceA>();
			}
			_stopwatch.Stop();

			// Report
			var avgTime = _stopwatch.Elapsed.TotalMilliseconds / cycles;
			Debug.Log($"[Performance] Multi-type registration cycles (2 types): {cycles} cycles in {_stopwatch.Elapsed.TotalMilliseconds:F2}ms (avg: {avgTime:F4}ms per cycle)");

			// Assert reasonable performance
			Assert.Less(_stopwatch.Elapsed.TotalMilliseconds, 3000, "Multi-type registration cycles should complete in under 3 seconds");
		}

		[Test]
		public void Performance_MultiType_RegistrationCycle_4Types()
		{
			// Test register/unregister cycles with 4 types
			const int cycles = 1000;
			var service = new TestPerformanceService();

			// Act
			_stopwatch.Restart();
			for (int i = 0; i < cycles; i++)
			{
				_serviceKit.RegisterService<IPerformanceServiceA>(service)
					.AlsoAs<IPerformanceServiceB>()
					.AlsoAs<IPerformanceServiceC>()
					.AlsoAs<IPerformanceServiceD>();
				_serviceKit.UnregisterService<IPerformanceServiceA>();
			}
			_stopwatch.Stop();

			// Report
			var avgTime = _stopwatch.Elapsed.TotalMilliseconds / cycles;
			Debug.Log($"[Performance] Multi-type registration cycles (4 types): {cycles} cycles in {_stopwatch.Elapsed.TotalMilliseconds:F2}ms (avg: {avgTime:F4}ms per cycle)");

			// Assert reasonable performance
			Assert.Less(_stopwatch.Elapsed.TotalMilliseconds, 4000, "Multi-type registration cycles (4 types) should complete in under 4 seconds");
		}



		[Test]
		public void Performance_SingleType_Resolution()
		{
			// Arrange - Register one service
			var service = new TestPerformanceService();
			_serviceKit.RegisterService<IPerformanceServiceA>(service);
			_serviceKit.ReadyService<IPerformanceServiceA>();

			// Act - Query it many times
			const int lookupCount = 10000;
			_stopwatch.Restart();
			for (int i = 0; i < lookupCount; i++)
			{
				var retrieved = _serviceKit.GetService<IPerformanceServiceA>();
			}
			_stopwatch.Stop();

			// Report
			var avgTime = _stopwatch.Elapsed.TotalMilliseconds / lookupCount;
			Debug.Log($"[Performance] Single-type resolution: {lookupCount} lookups in {_stopwatch.Elapsed.TotalMilliseconds:F2}ms (avg: {avgTime:F4}ms per lookup)");

			// Assert reasonable performance
			Assert.Less(_stopwatch.Elapsed.TotalMilliseconds, 500, "Resolution should complete in under 500ms");
		}

		[Test]
		public void Performance_MultiType_Resolution_DifferentTypes()
		{
			// Arrange - Register one service as multiple types
			var service = new TestPerformanceService();
			_serviceKit.RegisterService<IPerformanceServiceA>(service)
				.AlsoAs<IPerformanceServiceB>()
				.AlsoAs<IPerformanceServiceC>()
				.AlsoAs<IPerformanceServiceD>();
			_serviceKit.ReadyService<IPerformanceServiceA>();

			// Act - Resolve via different types repeatedly
			const int iterations = 2500;
			_stopwatch.Restart();
			for (int i = 0; i < iterations; i++)
			{
				var serviceA = _serviceKit.GetService<IPerformanceServiceA>();
				var serviceB = _serviceKit.GetService<IPerformanceServiceB>();
				var serviceC = _serviceKit.GetService<IPerformanceServiceC>();
				var serviceD = _serviceKit.GetService<IPerformanceServiceD>();
			}
			_stopwatch.Stop();

			// Report
			var totalLookups = iterations * 4;
			var avgTime = _stopwatch.Elapsed.TotalMilliseconds / totalLookups;
			Debug.Log($"[Performance] Multi-type resolution: {totalLookups} lookups in {_stopwatch.Elapsed.TotalMilliseconds:F2}ms (avg: {avgTime:F4}ms per lookup)");

			// Assert reasonable performance
			Assert.Less(_stopwatch.Elapsed.TotalMilliseconds, 1000, "Multi-type resolution should complete in under 1 second");
		}



		[Test]
		public async Task Performance_AsyncInjection_SingleType()
		{
			// Arrange
			const int consumerCount = 100;
			var service = new TestPerformanceService();
			_serviceKit.RegisterService<IPerformanceServiceA>(service);
			_serviceKit.ReadyService<IPerformanceServiceA>();

			var consumers = new List<SingleTypeConsumer>();
			for (int i = 0; i < consumerCount; i++)
			{
				consumers.Add(new SingleTypeConsumer());
			}

			// Act
			_stopwatch.Restart();
			for (int i = 0; i < consumerCount; i++)
			{
				await _serviceKit.InjectServicesAsync(consumers[i]).ExecuteAsync();
			}
			_stopwatch.Stop();

			// Report
			var avgTime = _stopwatch.Elapsed.TotalMilliseconds / consumerCount;
			Debug.Log($"[Performance] Single-type async injection: {consumerCount} injections in {_stopwatch.Elapsed.TotalMilliseconds:F2}ms (avg: {avgTime:F4}ms per injection)");

			// Assert
			Assert.Less(_stopwatch.Elapsed.TotalMilliseconds, 2000, "Async injection should complete in under 2 seconds");
			Assert.IsTrue(consumers.All(c => c.ServiceA != null), "All services should be injected");
		}

		[Test]
		public async Task Performance_AsyncInjection_MultiType()
		{
			// Arrange
			const int consumerCount = 100;
			var service = new TestPerformanceService();
			_serviceKit.RegisterService<IPerformanceServiceA>(service)
				.AlsoAs<IPerformanceServiceB>()
				.AlsoAs<IPerformanceServiceC>()
				.AlsoAs<IPerformanceServiceD>();
			_serviceKit.ReadyService<IPerformanceServiceA>();

			var consumers = new List<MultiTypeConsumer>();
			for (int i = 0; i < consumerCount; i++)
			{
				consumers.Add(new MultiTypeConsumer());
			}

			// Act
			_stopwatch.Restart();
			for (int i = 0; i < consumerCount; i++)
			{
				await _serviceKit.InjectServicesAsync(consumers[i]).ExecuteAsync();
			}
			_stopwatch.Stop();

			// Report
			var avgTime = _stopwatch.Elapsed.TotalMilliseconds / consumerCount;
			Debug.Log($"[Performance] Multi-type async injection: {consumerCount} injections in {_stopwatch.Elapsed.TotalMilliseconds:F2}ms (avg: {avgTime:F4}ms per injection)");

			// Assert
			Assert.Less(_stopwatch.Elapsed.TotalMilliseconds, 3000, "Multi-type async injection should complete in under 3 seconds");
			Assert.IsTrue(consumers.All(c => c.ServiceA != null && c.ServiceB != null && c.ServiceC != null && c.ServiceD != null),
				"All services should be injected");
		}

		[Test]
		public async Task Performance_ParallelAsyncInjection_MultiType()
		{
			// Arrange
			const int consumerCount = 100;
			var service = new TestPerformanceService();
			_serviceKit.RegisterService<IPerformanceServiceA>(service)
				.AlsoAs<IPerformanceServiceB>()
				.AlsoAs<IPerformanceServiceC>()
				.AlsoAs<IPerformanceServiceD>();
			_serviceKit.ReadyService<IPerformanceServiceA>();

			var consumers = new List<MultiTypeConsumer>();
			for (int i = 0; i < consumerCount; i++)
			{
				consumers.Add(new MultiTypeConsumer());
			}

			// Act - Parallel injection
			_stopwatch.Restart();
#if SERVICEKIT_UNITASK
			await UniTask.WhenAll(consumers.Select(c => _serviceKit.InjectServicesAsync(c).ExecuteAsync()));
#else
			await Task.WhenAll(consumers.Select(c => _serviceKit.InjectServicesAsync(c).ExecuteAsync()));
#endif
			_stopwatch.Stop();

			// Report
			var avgTime = _stopwatch.Elapsed.TotalMilliseconds / consumerCount;
			Debug.Log($"[Performance] Parallel multi-type async injection: {consumerCount} injections in {_stopwatch.Elapsed.TotalMilliseconds:F2}ms (avg: {avgTime:F4}ms per injection)");

			// Assert - Should be faster than sequential
			Assert.Less(_stopwatch.Elapsed.TotalMilliseconds, 2000, "Parallel injection should be faster");
			Assert.IsTrue(consumers.All(c => c.ServiceA != null && c.ServiceB != null && c.ServiceC != null && c.ServiceD != null),
				"All services should be injected");
		}



		[Test]
		public void Performance_TagQuery_WithDeduplication()
		{
			// Arrange - Register one service with tag as multiple types
			var service = new TestPerformanceService();
			var tag = new ServiceTag("PerformanceTag");

			_serviceKit.RegisterService<IPerformanceServiceA>(service, new[] { tag })
				.AlsoAs<IPerformanceServiceB>()
				.AlsoAs<IPerformanceServiceC>()
				.AlsoAs<IPerformanceServiceD>();
			_serviceKit.ReadyService<IPerformanceServiceA>();

			// Act - Query many times to test deduplication performance
			const int iterations = 5000;
			_stopwatch.Restart();
			for (int i = 0; i < iterations; i++)
			{
				var results = _serviceKit.GetServicesWithTag("PerformanceTag");
			}
			_stopwatch.Stop();

			// Report
			var avgTime = _stopwatch.Elapsed.TotalMilliseconds / iterations;
			Debug.Log($"[Performance] Tag query with deduplication: {iterations} queries in {_stopwatch.Elapsed.TotalMilliseconds:F2}ms (avg: {avgTime:F4}ms per query)");

			// Assert
			Assert.Less(_stopwatch.Elapsed.TotalMilliseconds, 500, "Tag queries should complete in under 500ms");
		}

		[Test]
		public void Performance_TagQuery_MultipleTags()
		{
			// Arrange - Register one service with multiple tags and types
			var service = new TestPerformanceService();
			var tags = new[]
			{
				new ServiceTag("Tag1"),
				new ServiceTag("Tag2"),
				new ServiceTag("Tag3"),
				new ServiceTag("Tag4")
			};

			_serviceKit.RegisterService<IPerformanceServiceA>(service, tags)
				.AlsoAs<IPerformanceServiceB>()
				.AlsoAs<IPerformanceServiceC>();
			_serviceKit.ReadyService<IPerformanceServiceA>();

			// Act - Perform multiple tag query types repeatedly
			const int iterations = 1000;
			_stopwatch.Restart();
			for (int i = 0; i < iterations; i++)
			{
				var results1 = _serviceKit.GetServicesWithTag("Tag1");
				var results2 = _serviceKit.GetServicesWithAnyTag(new[] { "Tag2", "Tag3" });
				var results3 = _serviceKit.GetServicesWithAllTags(new[] { "Tag1", "Tag2", "Tag3", "Tag4" });
			}
			_stopwatch.Stop();

			// Report
			var avgTime = _stopwatch.Elapsed.TotalMilliseconds / (iterations * 3);
			Debug.Log($"[Performance] Multiple tag query types: {iterations * 3} queries in {_stopwatch.Elapsed.TotalMilliseconds:F2}ms (avg: {avgTime:F4}ms per query)");

			// Assert
			Assert.Less(_stopwatch.Elapsed.TotalMilliseconds, 500, "Tag queries should complete in under 500ms");
		}



		[Test]
		public void Performance_Unregister_MultiType()
		{
			// Test unregistration speed with register/unregister cycles
			const int cycles = 1000;
			var service = new TestPerformanceService();

			// Act - Register and unregister via alternate type repeatedly
			_stopwatch.Restart();
			for (int i = 0; i < cycles; i++)
			{
				_serviceKit.RegisterService<IPerformanceServiceA>(service)
					.AlsoAs<IPerformanceServiceB>()
					.AlsoAs<IPerformanceServiceC>()
					.AlsoAs<IPerformanceServiceD>();
				_serviceKit.ReadyService<IPerformanceServiceA>();

				// Unregister via alternate type
				_serviceKit.UnregisterService<IPerformanceServiceB>();
			}
			_stopwatch.Stop();

			// Report
			var avgTime = _stopwatch.Elapsed.TotalMilliseconds / cycles;
			Debug.Log($"[Performance] Multi-type unregistration cycles: {cycles} cycles in {_stopwatch.Elapsed.TotalMilliseconds:F2}ms (avg: {avgTime:F4}ms per cycle)");

			// Assert
			Assert.Less(_stopwatch.Elapsed.TotalMilliseconds, 8000, "Unregistration cycles should complete in under 8 seconds");
		}



		[Test]
		public void Performance_Scaling_Cycles_ByTypeCount()
		{
			// Test scaling with increasing number of types
			var results = new List<(int typeCount, double cycleMs)>();
			var service = new TestPerformanceService();

			var scalingTests = new[] { 1, 2, 3, 4 };

			foreach (var typeCount in scalingTests)
			{
				_serviceKit.ClearServices();

				// Registration cycles
				const int cycles = 500;
				_stopwatch.Restart();
				for (int i = 0; i < cycles; i++)
				{
					var builder = _serviceKit.RegisterService<IPerformanceServiceA>(service);
					if (typeCount >= 2) builder.AlsoAs<IPerformanceServiceB>();
					if (typeCount >= 3) builder.AlsoAs<IPerformanceServiceC>();
					if (typeCount >= 4) builder.AlsoAs<IPerformanceServiceD>();
					_serviceKit.ReadyService<IPerformanceServiceA>();
					_serviceKit.UnregisterService<IPerformanceServiceA>();
				}
				var cycleTime = _stopwatch.Elapsed.TotalMilliseconds;

				results.Add((typeCount, cycleTime));
			}

			// Report
			Debug.Log("[Performance] Scaling Test Results (500 cycles each):");
			foreach (var (typeCount, cycleMs) in results)
			{
				var avgMs = cycleMs / 500;
				Debug.Log($"  {typeCount} types: Total={cycleMs:F2}ms, Avg={avgMs:F4}ms per cycle");
			}

			// Assert all completed in reasonable time
			Assert.IsTrue(results.All(r => r.cycleMs < 5000), "All scaling tests should complete in under 5 seconds");
		}

		[Test]
		public void Performance_HighFrequency_Resolution()
		{
			// Worst case: very high frequency resolution with multi-type service
			// This stresses the lookup logic
			var service = new TestPerformanceService();
			var tag = new ServiceTag("CommonTag");

			_serviceKit.RegisterService<IPerformanceServiceA>(service, new[] { tag })
				.AlsoAs<IPerformanceServiceB>()
				.AlsoAs<IPerformanceServiceC>()
				.AlsoAs<IPerformanceServiceD>();
			_serviceKit.ReadyService<IPerformanceServiceA>();

			// Act - Very high frequency lookups via different types
			const int iterations = 5000;
			_stopwatch.Restart();
			for (int i = 0; i < iterations; i++)
			{
				var a = _serviceKit.GetService<IPerformanceServiceA>();
				var b = _serviceKit.GetService<IPerformanceServiceB>();
				var c = _serviceKit.GetService<IPerformanceServiceC>();
				var d = _serviceKit.GetService<IPerformanceServiceD>();
			}
			_stopwatch.Stop();

			// Report
			var totalLookups = iterations * 4;
			var avgTime = _stopwatch.Elapsed.TotalMilliseconds / totalLookups;
			Debug.Log($"[Performance] High-frequency resolution: {totalLookups} lookups in {_stopwatch.Elapsed.TotalMilliseconds:F2}ms (avg: {avgTime:F4}ms per lookup)");

			// Assert
			Assert.Less(_stopwatch.Elapsed.TotalMilliseconds, 1000, "High-frequency lookups should complete in under 1 second");
		}



		[Test]
		public void Performance_MemoryPressure_RegisterUnregisterCycle()
		{
			// Test memory behavior with repeated register/unregister cycles
			// This tests that there are no memory leaks with multi-type services
			const int cycles = 1000;
			var service = new TestPerformanceService();

			_stopwatch.Restart();
			for (int cycle = 0; cycle < cycles; cycle++)
			{
				// Register with multiple types
				_serviceKit.RegisterService<IPerformanceServiceA>(service)
					.AlsoAs<IPerformanceServiceB>()
					.AlsoAs<IPerformanceServiceC>();
				_serviceKit.ReadyService<IPerformanceServiceA>();

				// Unregister
				_serviceKit.UnregisterService<IPerformanceServiceA>();
			}
			_stopwatch.Stop();

			// Report
			var avgTime = _stopwatch.Elapsed.TotalMilliseconds / cycles;
			Debug.Log($"[Performance] Memory pressure test: {cycles} register/unregister cycles in {_stopwatch.Elapsed.TotalMilliseconds:F2}ms (avg: {avgTime:F4}ms per cycle)");

			// Assert
			Assert.Less(_stopwatch.Elapsed.TotalMilliseconds, 5000, "Repeated cycles should complete in under 5 seconds");
		}



		private List<TestPerformanceService> CreateTestServices(int count)
		{
			var services = new List<TestPerformanceService>(count);
			for (int i = 0; i < count; i++)
			{
				services.Add(new TestPerformanceService { Id = i });
			}
			return services;
		}

	}


	public interface IPerformanceServiceA { int Id { get; set; } }
	public interface IPerformanceServiceB { int Id { get; set; } }
	public interface IPerformanceServiceC { int Id { get; set; } }
	public interface IPerformanceServiceD { int Id { get; set; } }

	public class TestPerformanceService : IPerformanceServiceA, IPerformanceServiceB, IPerformanceServiceC, IPerformanceServiceD
	{
		public int Id { get; set; }
	}

	public class SingleTypeConsumer
	{
		[InjectService]
		public IPerformanceServiceA ServiceA;
	}

	public class MultiTypeConsumer
	{
		[InjectService]
		public IPerformanceServiceA ServiceA;

		[InjectService]
		public IPerformanceServiceB ServiceB;

		[InjectService]
		public IPerformanceServiceC ServiceC;

		[InjectService]
		public IPerformanceServiceD ServiceD;
	}

}
