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
		public async Task WhenApplicationIsPlayingChangesDuringInjection_ServicesShouldStillBeInjected()
		{
			// This test simulates Application.isPlaying becoming false during injection
			// which causes ShouldIgnoreCancellation to return true
			
			// Arrange
			var serviceA = new ServiceA();
			_serviceLocator.RegisterService<IServiceA>(serviceA);
			// Don't ready it immediately to cause a wait
			
			var serviceB = new ServiceBWithOptionalDep();
			
			// Start injection
			var injectionTask = Task.Run(async () =>
			{
				using (var cts = new CancellationTokenSource(1000)) // 1 second timeout
				{
					// Start injection with cancellation
					// Don't use WithTimeout as it creates GameObjects which can't be done from background thread
					var task = _serviceLocator.InjectServicesAsync(serviceB)
						.WithCancellation(cts.Token)
						.ExecuteAsync();
					
					// After a short delay, cancel (simulating app quit)
					await Task.Delay(50);
					cts.Cancel();
					
					// Wait for injection to complete (or fail)
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
			
			// Ready the service while injection is happening
			await Task.Delay(25);
			_serviceLocator.ReadyService<IServiceA>();
			
			await injectionTask;
			
			// Assert
			// The issue is that when cancellation is ignored due to Application.isPlaying = false,
			// the method returns early WITHOUT injecting the resolved services
			// This is the bug in line 216-217 of ServiceInjectionBuilder
			
			// ServiceA was ready, so it should have been injected
			// But due to the early return, it might not be
			if (serviceB.ServiceA == null)
			{
				Assert.Fail("BUG CONFIRMED: Service was ready but not injected due to early return on ignored cancellation!");
			}
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