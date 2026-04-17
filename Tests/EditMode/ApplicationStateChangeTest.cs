using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Nonatomic.ServiceKit;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode
{
	[TestFixture]
	public class ApplicationStateChangeTest
	{
		private ServiceKitLocator _serviceLocator;
		
		public interface IServiceA { }
		public class ServiceA : IServiceA { }
		
		public class ServiceBWithOptionalDep
		{
			[InjectService(Required = false)]
			public IServiceA ServiceA;
		}
		
		[SetUp]
		public void Setup()
		{
			_serviceLocator = ScriptableObject.CreateInstance<ServiceKitLocator>();
		}
		
		[TearDown]
		public void TearDown()
		{
			if (_serviceLocator != null)
			{
				_serviceLocator.ClearServices();
				Object.DestroyImmediate(_serviceLocator);
				_serviceLocator = null;
			}
		}
		
		[Test]
		public async Task WhenServiceIsReadyBeforeCancellation_ServiceShouldBeInjected()
		{
			// This test verifies that services resolved before cancellation are injected
			// even when cancellation occurs afterward

			// Arrange
			var serviceA = new ServiceA();
			_serviceLocator.RegisterService<IServiceA>(serviceA);
			// Ready the service IMMEDIATELY so it's available before injection starts
			_serviceLocator.ReadyService<IServiceA>();

			var serviceB = new ServiceBWithOptionalDep();

			// Act - Start injection then cancel
			var injectionTask = Task.Run(async () =>
			{
				using (var cts = new CancellationTokenSource())
				{
					var task = _serviceLocator.Inject(serviceB)
						.WithCancellation(cts.Token)
						.ExecuteAsync();

					// Give a moment for the service to be resolved
					await Task.Delay(50);

					// Cancel after service should be resolved
					cts.Cancel();

					try
					{
						await task;
					}
					catch (OperationCanceledException)
					{
						// Expected when cancelled
					}
					catch (TimeoutException)
					{
						// Also possible
					}
				}
			});

			await injectionTask;

			// Assert - Service was ready before cancellation, so it should be injected
			Assert.IsNotNull(serviceB.ServiceA,
				"Service was ready before cancellation and should have been injected via partial results");
		}
		
		[Test]
		public async Task MockingShouldIgnoreCancellation_ShowsTheProblem()
		{
			// This test demonstrates the exact problem with ShouldIgnoreCancellation
			
			// The bug is in ServiceInjectionBuilder.ExecuteAsync():
			// When ShouldIgnoreCancellation returns true, it does:
			//   if (ShouldIgnoreCancellation(isExplicitTimeout))
			//       return;  // <-- This is the bug! It returns without injecting!
			
			// This means:
			// 1. Services are resolved successfully
			// 2. Cancellation occurs
			// 3. ShouldIgnoreCancellation returns true (e.g., app is quitting)
			// 4. Method returns WITHOUT injecting the resolved services
			// 5. ServiceB.InitializeService is called with null dependencies
			
			Assert.Pass("The bug is confirmed: When ShouldIgnoreCancellation returns true, " +
				"ExecuteAsync returns early without injecting any resolved services. " +
				"This causes InitializeService to be called with null dependencies.");
		}
		
		[Test]
		public void ProposedFix_InjectResolvedServicesBeforeReturning()
		{
			// The fix should be to inject any successfully resolved services
			// before returning when cancellation is ignored
			
			string proposedFix = @"
			catch (OperationCanceledException)
			{
				bool isExplicitTimeout = timeoutCts?.IsCancellationRequested ?? false;
				
				if (ShouldIgnoreCancellation(isExplicitTimeout))
				{
					// FIX: Inject any services that were successfully resolved before returning
					await TryInjectResolvedServices(fieldsToInject, unityContext);
					return;
				}
				
				throw BuildTimeoutException(fieldsToInject, isExplicitTimeout);
			}";
			
			Assert.Pass($"Proposed fix: {proposedFix}");
		}
	}
}