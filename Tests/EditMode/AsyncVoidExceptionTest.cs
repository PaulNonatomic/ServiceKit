using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
	[TestFixture]
	public class AsyncVoidExceptionTest
	{
		private bool _initializeServiceCalled;
		private bool _injectionCompleted;
		private Exception _caughtException;

		private async Task SimulateInjectDependenciesAsync()
		{
			// Simulate a timeout after a delay
			await Task.Delay(10);
			throw new TimeoutException("Simulated timeout during injection");
		}

		private async Task SimulatePerformServiceInitializationSequence()
		{
			try
			{
				// This simulates InjectDependenciesAsync
				await SimulateInjectDependenciesAsync();
				_injectionCompleted = true;
			}
			catch (Exception ex)
			{
				// If exception is caught here, InitializeService shouldn't be called
				_caughtException = ex;
				throw; // Re-throw to test propagation
			}
			
			// This simulates InitializeService - should NOT be called if injection throws
			_initializeServiceCalled = true;
		}

		[Test]
		public async Task WhenExceptionInAsync_InitializeServiceShouldNotBeCalled()
		{
			// Reset state
			_initializeServiceCalled = false;
			_injectionCompleted = false;
			_caughtException = null;
			
			// Act
			bool exceptionThrown = false;
			try
			{
				await SimulatePerformServiceInitializationSequence();
			}
			catch (TimeoutException)
			{
				exceptionThrown = true;
			}
			
			// Assert
			Assert.IsTrue(exceptionThrown, "Exception should have been thrown");
			Assert.IsFalse(_injectionCompleted, "Injection should not have completed");
			Assert.IsFalse(_initializeServiceCalled, "InitializeService should NOT have been called after exception");
			Assert.IsNotNull(_caughtException, "Exception should have been caught");
		}

		[Test]
		public void AsyncVoidExceptionBehavior()
		{
			// This tests what happens with async void
			bool continuationRan = false;
			Exception unhandledException = null;
			
			// Setup handler for unobserved exceptions
			var previousHandler = SynchronizationContext.Current;
			var testContext = new TestSynchronizationContext();
			testContext.UnhandledException += (sender, args) =>
			{
				unhandledException = args.Exception;
			};
			SynchronizationContext.SetSynchronizationContext(testContext);
			
			try
			{
				// Simulate async void method
				async void AsyncVoidMethod()
				{
					await Task.Delay(1);
					throw new TimeoutException("Test exception");
				}
				
				// Call async void method
				AsyncVoidMethod();
				
				// This will run even though exception was thrown
				continuationRan = true;
				
				// Give time for async operation to complete
				System.Threading.Thread.Sleep(100);
			}
			finally
			{
				SynchronizationContext.SetSynchronizationContext(previousHandler);
			}
			
			// In async void, the exception is posted to SynchronizationContext
			// but doesn't stop execution of the calling method
			Assert.IsTrue(continuationRan, "Code after async void call should run");
		}
		
		private class TestSynchronizationContext : SynchronizationContext
		{
			public event EventHandler<TestUnhandledExceptionEventArgs> UnhandledException;
			
			public override void Post(SendOrPostCallback d, object state)
			{
				try
				{
					d(state);
				}
				catch (Exception ex)
				{
					UnhandledException?.Invoke(this, new TestUnhandledExceptionEventArgs(ex));
				}
			}
		}
		
		private class TestUnhandledExceptionEventArgs : EventArgs
		{
			public Exception Exception { get; }
			
			public TestUnhandledExceptionEventArgs(Exception exception)
			{
				Exception = exception;
			}
		}
	}
}