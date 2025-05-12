using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Nonatomic.ServiceKit
{
	/// <summary>
	///     Represents an asynchronous service injection operation that can be configured
	///     with cancellation, timeout, and error handling.
	/// </summary>
	/// <summary>
    /// Represents an asynchronous service injection operation that can be configured
    /// with cancellation, timeout, and error handling.
    /// </summary>
    public class AsyncInjectionOperation
    {
        // Updated field type
        private readonly IServiceKit _serviceKit;
        private readonly object _target;
        private CancellationToken _cancellationToken = CancellationToken.None;
        private TimeSpan? _timeout = null;
        private Action<Exception> _errorHandler = null;

        // Updated constructor parameter type
        internal AsyncInjectionOperation(IServiceKit serviceKit, object target)
        {
            _serviceKit = serviceKit ?? throw new ArgumentNullException(nameof(serviceKit));
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public AsyncInjectionOperation WithCancellation(CancellationToken token)
        {
            _cancellationToken = token;
            return this;
        }

        public AsyncInjectionOperation WithTimeout(TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");
            _timeout = timeout;
            return this;
        }

        public AsyncInjectionOperation WithTimeout(double seconds)
        {
            if (seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds), "Timeout must be positive.");
            return WithTimeout(TimeSpan.FromSeconds(seconds));
        }

        public AsyncInjectionOperation WithErrorHandling(Action<Exception> handler)
        {
            _errorHandler = handler;
            return this;
        }

        public TaskAwaiter GetAwaiter()
        {
            return ExecuteAsync().GetAwaiter();
        }

        public async Task ExecuteAsync()
        {
            if (_target == null)
            {
                Debug.LogError("Cannot inject services into a null target (checked in ExecuteAsync).");
                return;
            }

            CancellationTokenSource operationCts = null;
            CancellationToken finalToken = _cancellationToken;

            try
            {
                if (_timeout.HasValue)
                {
                    var timeoutCts = new CancellationTokenSource(_timeout.Value);
                    operationCts = _cancellationToken != CancellationToken.None
                        ? CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, timeoutCts.Token)
                        : timeoutCts;
                    finalToken = operationCts.Token;
                }

                Type targetType = _target.GetType();
                FieldInfo[] fields = targetType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var injectionTasks = new List<Task>();

                foreach (FieldInfo field in fields)
                {
                    if (Attribute.IsDefined(field, typeof(InjectServiceAttribute)))
                    {
                        Type serviceType = field.FieldType;
                        injectionTasks.Add(InjectSingleFieldAsync(field, serviceType, finalToken));
                    }
                }
                try
                {
                    await Task.WhenAll(injectionTasks);
                }
                catch (OperationCanceledException)
                {
                    // This catch is primarily for the case where the finalToken is cancelled *before* Task.WhenAll gets awaited,
                    // or if WhenAll directly propagates a single OCE.
                    Debug.LogWarning("Injection operation cancelled (caught directly after WhenAll).");
                    throw; // Propagate cancellation
                }
                catch (AggregateException ae)
                {
                    // Check if the aggregate contains cancellation
                    ae.Handle(ex => {
                        if (ex is OperationCanceledException)
                        {
                            Debug.LogWarning($"Injection task cancelled for one or more fields (within AggregateException).");
                            return true; // Mark OCE as handled within the aggregate for logging purposes
                        }
                        return false; // Indicate other exceptions are not handled here
                    });

                    // IMPORTANT: Even if handled for logging, we need to ensure cancellation propagates
                    // if the primary reason for failure was cancellation.
                    var firstOce = ae.InnerExceptions.OfType<OperationCanceledException>().FirstOrDefault();
                    if (firstOce != null)
                    {
                        // Throw the specific OperationCanceledException that occurred.
                        // This makes the outer catch block's behavior more predictable.
                        throw firstOce;
                    }
                    else
                    {
                         // If other exceptions were present, re-throw the original AggregateException
                         throw;
                    }
                }
            }
            catch (Exception ex) // Catch exceptions propagated from above or from the setup phase
            {
                if (_errorHandler != null)
                {
                    // If a handler exists, let it decide what to do (log, suppress, re-throw)
                    _errorHandler(ex);
                }
                else
                {
                    // FIX: If no error handler, always re-throw the exception, including OperationCanceledException.
                    // The previous logic suppressed OCE here, causing the test failures.
                    throw;
                }
            }
            finally
            {
                operationCts?.Dispose();
            }
        }

        private async Task InjectSingleFieldAsync(FieldInfo field, Type serviceType, CancellationToken token)
        {
            // Let exceptions propagate up to ExecuteAsync's Task.WhenAll catch blocks
            token.ThrowIfCancellationRequested();

            MethodInfo getServiceAsyncMethodInfo = typeof(IServiceKit)
                .GetMethod(nameof(IServiceKit.GetServiceAsync), new Type[] { typeof(CancellationToken) });

            if (getServiceAsyncMethodInfo == null)
            {
                throw new InvalidOperationException($"Critical error: Could not find GetServiceAsync method definition on IServiceKit.");
            }

            MethodInfo genericGetServiceAsyncMethod = getServiceAsyncMethodInfo.MakeGenericMethod(serviceType);
            var serviceTaskUntyped = (Task)genericGetServiceAsyncMethod.Invoke(_serviceKit, new object[] { token });

            await serviceTaskUntyped; // Let potential exceptions (like OCE) propagate

            PropertyInfo resultProperty = serviceTaskUntyped.GetType().GetProperty("Result");
            object serviceInstance = resultProperty.GetValue(serviceTaskUntyped);

            if (serviceInstance != null)
            {
                if (field.FieldType.IsAssignableFrom(serviceInstance.GetType()))
                {
                    field.SetValue(_target, serviceInstance);
                    Debug.Log($"Async Injected service of type {serviceType.FullName} into field {field.Name} of {_target.GetType().Name}");
                }
                else
                {
                    Debug.LogWarning($"Registered service instance for type {serviceType.FullName} is not assignable to field {field.Name} ({field.FieldType.FullName}) in {_target.GetType().Name}. Injection skipped.");
                }
            }
            else
            {
                 Debug.LogWarning($"Service of type {serviceType.FullName} (for field {field.Name}) was resolved to null by GetServiceAsync. Injection skipped.");
            }
            // Let TargetInvocationException propagate if Invoke fails
        }
    }
}