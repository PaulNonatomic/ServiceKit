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
	public class AsyncInjectionOperation
	{
		private readonly IServiceKit _serviceKit;
		private readonly object _target;
		private CancellationToken _cancellationToken = CancellationToken.None;
		private Action<Exception> _customErrorHandler;
		private ErrorHandlingMode _errorHandlingMode = ErrorHandlingMode.Error;

		// Field to store the CancellationTokenSource for the internal timeout
		private CancellationTokenSource _internalTimeoutCts;
		private TimeSpan? _timeout;

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
			if (timeout <= TimeSpan.Zero)
			{
				throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");
			}

			_timeout = timeout;
			return this;
		}

		public AsyncInjectionOperation WithTimeout(double seconds)
		{
			if (seconds <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(seconds), "Timeout must be positive.");
			}

			return WithTimeout(TimeSpan.FromSeconds(seconds));
		}

		public AsyncInjectionOperation WithTimeout()
		{
			_timeout = _serviceKit.GetDefaultAsyncInjectionTimeout();
			return this;
		}

		public AsyncInjectionOperation WithErrorHandling(Action<Exception> handler)
		{
			_customErrorHandler = handler;
			return this;
		}

		public AsyncInjectionOperation WithErrorHandling(ErrorHandlingMode mode)
		{
			_errorHandlingMode = mode;
			_customErrorHandler = null;
			return this;
		}

		public AsyncInjectionOperation WithErrorHandling()
		{
			_errorHandlingMode = ErrorHandlingMode.Error;
			_customErrorHandler = null;
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

			CancellationTokenSource linkedCts = null;
			var finalToken = _cancellationToken;
			_internalTimeoutCts = null; // Reset for this execution

			try
			{
				if (_timeout.HasValue)
				{
					_internalTimeoutCts = new(_timeout.Value); // Store our own timeout CTS

					if (_cancellationToken != CancellationToken.None)
					{
						linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, _internalTimeoutCts.Token);
						finalToken = linkedCts.Token;
					}
					else
					{
						finalToken = _internalTimeoutCts.Token;
					}
				}

				var targetType = _target.GetType();
				var fields = targetType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				var injectionTasks = new List<Task>();

				foreach (var field in fields)
				{
					if (!Attribute.IsDefined(field, typeof(InjectServiceAttribute)))
					{
						continue;
					}

					var serviceType = field.FieldType;
					injectionTasks.Add(InjectSingleFieldAsync(field, serviceType, finalToken));
				}

				try
				{
					await Task.WhenAll(injectionTasks).ConfigureAwait(false);
				}
				catch (OperationCanceledException oce)
				{
					LogCancellationDetails(oce, _target, _timeout, _cancellationToken, _internalTimeoutCts, _errorHandlingMode);
					throw;
				}
				catch (AggregateException ae)
				{
					var oce = ae.InnerExceptions.OfType<OperationCanceledException>().FirstOrDefault();
					if (oce != null)
					{
						LogCancellationDetails(oce, _target, _timeout, _cancellationToken, _internalTimeoutCts, _errorHandlingMode, "One or more injection tasks cancelled.");
						throw oce; // Propagate the specific OCE
					}

					// Log other AggregateExceptions if not silent and not handled by custom handler
					if (_customErrorHandler == null && _errorHandlingMode != ErrorHandlingMode.Silent)
					{
						(_errorHandlingMode == ErrorHandlingMode.Error ? (Action<object>)Debug.LogError : Debug.LogWarning)($"[ServiceKit Injection Error] AggregateException encountered during injection into '{_target?.GetType().Name}': {ae}");
					}

					throw;
				}
			}
			catch (Exception ex)
			{
				if (_customErrorHandler != null)
				{
					_customErrorHandler(ex);
				}
				else
				{
					var isHandledCancellation = ex is OperationCanceledException;
					// For other exceptions, or if ErrorHandlingMode dictates logging for cancellations not yet logged
					if (!isHandledCancellation && _errorHandlingMode != ErrorHandlingMode.Silent)
					{
						var logMsg = $"[ServiceKit Injection Error] Error during injection into '{_target?.GetType().Name}': {ex}";
						switch (_errorHandlingMode)
						{
							case ErrorHandlingMode.Error: Debug.LogError(logMsg); break;
							case ErrorHandlingMode.Warning: Debug.LogWarning(logMsg); break;
							case ErrorHandlingMode.Log: Debug.Log(logMsg); break;
						}
					}

					if (_errorHandlingMode == ErrorHandlingMode.Error)
					{
						throw;
					}
				}
			}
			finally
			{
				linkedCts?.Dispose();
				_internalTimeoutCts?.Dispose();
			}
		}

		private void LogCancellationDetails(
			OperationCanceledException oce,
			object target,
			TimeSpan? configuredTimeout,
			CancellationToken externalToken,
			CancellationTokenSource internalTimeoutTokenSource,
			ErrorHandlingMode errorMode,
			string contextMessage = null)
		{
			// If silent mode is active for all errors, don't log here.
			// Specific handling might be needed if only some errors should be silent.
			if (errorMode == ErrorHandlingMode.Silent)
			{
				return;
			}

			var targetName = target?.GetType().Name ?? "Unknown Target";
			var baseMessage = string.IsNullOrEmpty(contextMessage)
				? $"Injection operation for '{targetName}'"
				: $"{contextMessage} for '{targetName}'";

			var oceMatchesTimeoutToken = internalTimeoutTokenSource != null && oce.CancellationToken == internalTimeoutTokenSource.Token;
			var wasTimeoutOverall = internalTimeoutTokenSource != null && internalTimeoutTokenSource.IsCancellationRequested;
			var oceMatchesExternalToken = externalToken.CanBeCanceled && oce.CancellationToken == externalToken;
			var wasExternalCancelOverall = externalToken.CanBeCanceled && externalToken.IsCancellationRequested;

			Action<string> logger = Debug.LogWarning; // Default
			if (errorMode == ErrorHandlingMode.Log)
			{
				logger = Debug.Log;
			}
			// For ErrorHandlingMode.Error, we use LogError for timeouts and LogWarning for other cancellations by default.

			if (oceMatchesTimeoutToken || (!oceMatchesExternalToken && wasTimeoutOverall && configuredTimeout.HasValue))
			{
				var logMsg = $"[ServiceKit Injection Timeout] {baseMessage} timed out after {configuredTimeout.Value.TotalSeconds}s. Ensure all required services (check field-specific logs) are registered, active, and their Awake/initialization has completed.";
				if (errorMode == ErrorHandlingMode.Error)
				{
					Debug.LogError(logMsg);
				}
				else
				{
					logger(logMsg);
				}
			}
			else if (oceMatchesExternalToken || (!oceMatchesTimeoutToken && wasExternalCancelOverall))
			{
				var logMsg = $"[ServiceKit Injection Cancelled Externally] {baseMessage} was cancelled by the provided CancellationToken (e.g., destroyCancellationToken).";
				if (errorMode == ErrorHandlingMode.Error)
				{
					Debug.LogWarning(logMsg);
				}
				else
				{
					logger(logMsg);
				}
			}
			else
			{
				var logMsg = $"[ServiceKit Injection Cancelled] {baseMessage} was cancelled. Exception: {oce.Message}. (oce.CancellationToken: {oce.CancellationToken})";
				if (errorMode == ErrorHandlingMode.Error)
				{
					Debug.LogWarning(logMsg);
				}
				else
				{
					logger(logMsg);
				}
			}
		}

		private async Task InjectSingleFieldAsync(FieldInfo field, Type serviceType, CancellationToken token)
		{
			try
			{
				token.ThrowIfCancellationRequested();

				var getServiceAsyncMethodInfo = typeof(IServiceKit)
					.GetMethod(nameof(IServiceKit.GetServiceAsync), new[] { typeof(CancellationToken) });

				if (getServiceAsyncMethodInfo == null)
				{
					throw new InvalidOperationException("Critical error: Could not find GetServiceAsync method definition on IServiceKit.");
				}

				var genericGetServiceAsyncMethod = getServiceAsyncMethodInfo.MakeGenericMethod(serviceType);
				var serviceTaskUntyped = (Task)genericGetServiceAsyncMethod.Invoke(_serviceKit, new object[] { token });

				await serviceTaskUntyped.ConfigureAwait(false);

				var resultProperty = serviceTaskUntyped.GetType().GetProperty("Result");
				var serviceInstance = resultProperty.GetValue(serviceTaskUntyped);

				if (serviceInstance != null)
				{
					if (field.FieldType.IsAssignableFrom(serviceInstance.GetType()))
					{
						field.SetValue(_target, serviceInstance);
						// Consider making this log conditional or less verbose for successful injections
						// Debug.Log($"Async Injected service of type {serviceType.FullName} into field {field.Name} of {_target.GetType().Name}");
					}
					else
					{
						Debug.LogWarning($"Registered service instance for type {serviceType.FullName} is not assignable to field {field.Name} ({field.FieldType.FullName}) in {_target.GetType().Name}. Injection skipped.");
					}
				}
				else
				{
					// This case usually means a null was explicitly registered.
					// If GetServiceAsync times out or is cancelled, it throws OperationCanceledException,
					// which is caught below.
					Debug.LogWarning($"Service of type {serviceType.FullName} (for field {field.Name}) was resolved to null by GetServiceAsync (meaning a null instance was likely registered). Injection skipped for {_target.GetType().Name}.");
				}
			}
			catch (OperationCanceledException oce)
			{
				// This log is very useful as it tells you *which* service dependency was likely problematic during a timeout/cancellation.
				Debug.LogWarning($"[ServiceKit Injection Field Cancelled/Timed Out] Injection of service {serviceType.FullName} into field {field.Name} of {_target.GetType().Name} was cancelled or timed out. This is often due to the service not being registered or active. OCE: {oce.Message}");
				throw;
			}
			catch (Exception ex)
			{
				Debug.LogError($"[ServiceKit Injection Field Error] Failed to inject service {serviceType.FullName} into field {field.Name} of {_target.GetType().Name}. Error: {ex}");
				throw;
			}
		}
	}
}