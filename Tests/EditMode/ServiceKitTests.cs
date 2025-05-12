using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nonatomic.ServiceKit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.EditMode
{
	public interface ITestService
	{
	}

	public interface IAnotherTestService
	{
	}

	public class TestService : ITestService
	{
	}

	public class AnotherTestService : IAnotherTestService
	{
	}

	public class InjectionTarget
	{
		[field: InjectService] public ITestService TestService { get; private set; }
		[field: InjectService] public IAnotherTestService AnotherTestService { get; private set; }
	}

	public class InjectionTargetBroken
	{
		public ITestService TestService { get; private set; }
	}

	public class ServiceKitTests
	{
		private ServiceKit _serviceKit;

		[SetUp]
		public void Setup()
		{
			_serviceKit = ScriptableObject.CreateInstance<ServiceKit>();
		}

		[TearDown]
		public void Teardown()
		{
			if (_serviceKit != null)
			{
				Object.DestroyImmediate(_serviceKit);
				_serviceKit = null;
			}
		}

		[Test]
		public void RegisterAndRetrieveService_ServiceIsRetrievedSuccessfully()
		{
			var serviceInstance = new TestService();
			_serviceKit.RegisterService<ITestService>(serviceInstance);
			var retrievedService = _serviceKit.GetService<ITestService>();
			Assert.IsTrue(_serviceKit.HasService<ITestService>());
			Assert.IsNotNull(retrievedService);
			Assert.AreSame(serviceInstance, retrievedService);
		}

		[Test]
		public async Task RegisterAndInjectServicesAsync_MultipleServicesAreInjectedSuccessfully()
		{
			var serviceInstance = new TestService();
			_serviceKit.RegisterService<ITestService>(serviceInstance);
			var anotherServiceInstance = new AnotherTestService();
			_serviceKit.RegisterService<IAnotherTestService>(anotherServiceInstance);
			var injectionTarget = new InjectionTarget();

			await _serviceKit.InjectServicesAsync(injectionTarget);

			Assert.IsTrue(_serviceKit.HasService<ITestService>());
			Assert.IsNotNull(injectionTarget.TestService);
			Assert.AreSame(serviceInstance, injectionTarget.TestService);
			Assert.IsTrue(_serviceKit.HasService<IAnotherTestService>());
			Assert.IsNotNull(injectionTarget.AnotherTestService);
			Assert.AreSame(anotherServiceInstance, injectionTarget.AnotherTestService);
		}

		[Test]
		public async Task RegisterAndInjectServicesAsync_SingleServiceIsInjectedSuccessfully()
		{
			var serviceInstance = new TestService();
			_serviceKit.RegisterService<ITestService>(serviceInstance);
			var injectionTarget = new InjectionTarget();
			var caughtException = false;

			try
			{
				await _serviceKit.InjectServicesAsync(injectionTarget)
					.WithTimeout(TimeSpan.FromMilliseconds(100));
			}
			catch (OperationCanceledException)
			{
				caughtException = true;
			}
			catch (AggregateException ae) when (ae.InnerExceptions.OfType<OperationCanceledException>().Any())
			{
				caughtException = true;
			}

			Assert.IsTrue(caughtException, "Operation should be cancelled due to timeout on missing service.");
			Assert.IsTrue(_serviceKit.HasService<ITestService>());
			Assert.IsNotNull(injectionTarget.TestService,
				"The registered service should still be injected before timeout/cancellation.");
			Assert.AreSame(serviceInstance, injectionTarget.TestService);
			Assert.IsNull(injectionTarget.AnotherTestService, "The unregistered service should remain null.");
		}

		[Test]
		public async Task InjectServicesAsync_FieldWithoutAttribute_IsNotInjected()
		{
			var serviceInstance = new TestService();
			_serviceKit.RegisterService<ITestService>(serviceInstance);
			var injectionTarget = new InjectionTargetBroken();

			await _serviceKit.InjectServicesAsync(injectionTarget);

			Assert.IsTrue(_serviceKit.HasService<ITestService>());
			Assert.IsNull(injectionTarget.TestService);
			Assert.AreNotSame(serviceInstance, injectionTarget.TestService);
		}

		[Test]
		public async Task InjectServicesAsync_ServicesRegisteredLater_InjectsWhenAvailable()
		{
			var serviceInstance = new TestService();
			var anotherServiceInstance = new AnotherTestService();
			var injectionTarget = new InjectionTarget();

			var injectionTask = _serviceKit.InjectServicesAsync(injectionTarget).ExecuteAsync();

			Assert.IsNull(injectionTarget.TestService);
			Assert.IsNull(injectionTarget.AnotherTestService);

			await Task.Delay(30);
			_serviceKit.RegisterService<ITestService>(serviceInstance);

			await Task.Delay(30);
			_serviceKit.RegisterService<IAnotherTestService>(anotherServiceInstance);

			await injectionTask;

			Assert.IsNotNull(injectionTarget.TestService);
			Assert.AreSame(serviceInstance, injectionTarget.TestService);
			Assert.IsNotNull(injectionTarget.AnotherTestService);
			Assert.AreSame(anotherServiceInstance, injectionTarget.AnotherTestService);
		}

		[Test]
		public async Task InjectServicesAsync_WithCancellation_StopsInjection()
		{
			var injectionTarget = new InjectionTarget();
			using (var cts = new CancellationTokenSource())
			{
				var injectionTask = _serviceKit.InjectServicesAsync(injectionTarget)
					.WithCancellation(cts.Token)
					.ExecuteAsync();

				Assert.IsFalse(injectionTask.IsCompleted);
				await Task.Delay(5);
				cts.Cancel();

				try
				{
					await injectionTask;
				}
				catch (OperationCanceledException)
				{
					Assert.IsTrue(injectionTask.IsCanceled || injectionTask.IsFaulted);
				}
				catch (AggregateException ae)
				{
					Assert.IsTrue(ae.InnerExceptions.OfType<OperationCanceledException>().Any());
				}

				Assert.IsNull(injectionTarget.TestService,
					"TestService should not be injected if operation was cancelled early.");
				Assert.IsNull(injectionTarget.AnotherTestService,
					"AnotherTestService should not be injected if operation was cancelled early.");
			}
		}

		[Test]
		public async Task InjectServicesAsync_WithTimeout_CancelsIfNotCompletedInTime()
		{
			var injectionTarget = new InjectionTarget();
			var caughtOcOrAeWithOc = false;
			try
			{
				await _serviceKit.InjectServicesAsync(injectionTarget)
					.WithTimeout(TimeSpan.FromMilliseconds(50));
			}
			catch (OperationCanceledException)
			{
				caughtOcOrAeWithOc = true;
			}
			catch (AggregateException ae) when (ae.InnerExceptions.OfType<OperationCanceledException>().Any())
			{
				caughtOcOrAeWithOc = true;
			}

			Assert.IsTrue(caughtOcOrAeWithOc, "OperationCanceledException (due to timeout) was expected.");
			Assert.IsNull(injectionTarget.TestService, "Service should not be injected due to timeout.");
		}

		[Test]
		public async Task InjectServicesAsync_WithErrorHandling_CallsHandlerAndDoesNotThrow()
		{
			var injectionTarget = new InjectionTarget();
			Exception caughtException = null;
			var handlerCalled = false;

			LogAssert.ignoreFailingMessages = true;
			try
			{
				await _serviceKit.InjectServicesAsync(injectionTarget)
					.WithTimeout(TimeSpan.FromMilliseconds(50))
					.WithErrorHandling(ex =>
					{
						handlerCalled = true;
						caughtException = ex;
					});
			}
			finally
			{
				LogAssert.ignoreFailingMessages = false;
			}

			Assert.IsTrue(handlerCalled, "Error handler should have been called.");
			Assert.IsNotNull(caughtException, "Exception should have been passed to handler.");
			Assert.IsTrue(caughtException is OperationCanceledException ||
						  (caughtException is AggregateException ae &&
						   ae.InnerExceptions.OfType<OperationCanceledException>().Any()),
				"Exception should be OperationCanceledException or AggregateException containing it due to timeout.");
			Assert.IsNull(injectionTarget.TestService,
				"Service should not be injected when error handler is used for timeout.");
		}

		[Test]
		public async Task InjectServicesAsync_WithErrorHandling_PropagatesIfHandlerRethrows()
		{
			var injectionTarget = new InjectionTarget();
			var handlerCalled = false;
			var exceptionPropagated = false;

			LogAssert.ignoreFailingMessages = true;
			try
			{
				await _serviceKit.InjectServicesAsync(injectionTarget)
					.WithTimeout(TimeSpan.FromMilliseconds(50))
					.WithErrorHandling(ex =>
					{
						handlerCalled = true;
						throw ex;
					});
			}
			catch (OperationCanceledException)
			{
				exceptionPropagated = true;
			}
			catch (AggregateException ae) when (ae.InnerExceptions.OfType<OperationCanceledException>().Any())
			{
				exceptionPropagated = true;
			}
			finally
			{
				LogAssert.ignoreFailingMessages = false;
			}

			Assert.IsTrue(handlerCalled, "Error handler should have been called.");
			Assert.IsTrue(exceptionPropagated, "Exception should have propagated after handler re-threw.");
		}


		// --- Async GetServiceAsync Tests ---
		[Test]
		public async Task GetServiceAsync_ServiceAlreadyRegistered_ReturnsServiceImmediately()
		{
			var serviceInstance = new TestService();
			_serviceKit.RegisterService<ITestService>(serviceInstance);
			var retrievedService = await _serviceKit.GetServiceAsync<ITestService>();
			Assert.IsNotNull(retrievedService);
			Assert.AreSame(serviceInstance, retrievedService);
		}

		[Test]
		public async Task GetServiceAsync_ServiceRegisteredLater_ReturnsServiceWhenRegistered()
		{
			var serviceInstance = new TestService();
			var retrievalTask = _serviceKit.GetServiceAsync<ITestService>();
			Assert.IsFalse(retrievalTask.IsCompleted);
			await Task.Delay(10);
			_serviceKit.RegisterService<ITestService>(serviceInstance);
			var retrievedService = await retrievalTask;
			Assert.IsNotNull(retrievedService);
			Assert.AreSame(serviceInstance, retrievedService);
		}

		[Test]
		public async Task GetServiceAsync_WithCancellation_TaskIsCancelled()
		{
			using (var cts = new CancellationTokenSource())
			{
				var retrievalTask = _serviceKit.GetServiceAsync<ITestService>(cts.Token);
				Assert.IsFalse(retrievalTask.IsCompleted);
				cts.Cancel();
				try
				{
					await retrievalTask;
					Assert.Fail("Task should have been cancelled.");
				}
				catch (OperationCanceledException ex)
				{
					Assert.IsTrue(retrievalTask.IsCanceled);
					Assert.AreEqual(cts.Token, ex.CancellationToken);
				}
				catch (Exception ex)
				{
					Assert.Fail($"Expected OperationCanceledException but got {ex.GetType().Name}");
				}
			}
		}
	}
}