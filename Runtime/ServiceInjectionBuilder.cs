using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Nonatomic.ServiceKit
{
	public class ServiceInjectionBuilder : IServiceInjectionBuilder
	{
		private readonly IServiceKitLocator _serviceKitLocator;
		private readonly object _target;
		private CancellationToken _cancellationToken = CancellationToken.None;
		private float _timeout = -1f;
		private Action<Exception> _errorHandler;

		internal ServiceInjectionBuilder(IServiceKitLocator serviceKitLocator, object target)
		{
			_serviceKitLocator = serviceKitLocator;
			_target = target;
		}

		public IServiceInjectionBuilder WithCancellation(CancellationToken cancellationToken)
		{
			_cancellationToken = cancellationToken;
			return this;
		}

		public IServiceInjectionBuilder WithTimeout()
		{
			_timeout = ServiceKitSettings.Instance.DefaultTimeout;
			return this;
		}

		public IServiceInjectionBuilder WithTimeout(float timeoutSeconds)
		{
			_timeout = timeoutSeconds;
			return this;
		}

		public IServiceInjectionBuilder WithErrorHandling(Action<Exception> errorHandler)
		{
			_errorHandler = errorHandler;
			return this;
		}

		public IServiceInjectionBuilder WithErrorHandling()
		{
			_errorHandler = DefaultErrorHandler;
			return this;
		}

		public async void Execute()
		{
			try
			{
				await ExecuteAsync();
			}
			catch (Exception ex)
			{
				_errorHandler?.Invoke(ex);
			}
		}

		public async Task ExecuteAsync()
		{
			var targetType = _target.GetType();
			var fieldsToInject = GetFieldsToInject(targetType);

			if (fieldsToInject.Count == 0)
			{
				return;
			}

			// Create timeout cancellation if needed
			using var timeoutCts = _timeout > 0 ? new CancellationTokenSource(TimeSpan.FromSeconds(_timeout)) : null;
			using var linkedCts = timeoutCts != null
				? CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, timeoutCts.Token)
				: null;

			var finalToken = linkedCts?.Token ?? _cancellationToken;

			// Capture Unity thread context
			var unityContext = SynchronizationContext.Current;

			try
			{
				// Create tasks for all service retrievals
				var serviceTasks = fieldsToInject.Select<FieldInfo, Task<(FieldInfo fieldInfo, object service, bool Required)>>(async fieldInfo =>
				{
					var attribute = CustomAttributeExtensions.GetCustomAttribute<InjectServiceAttribute>((MemberInfo)fieldInfo);
					var serviceType = fieldInfo.FieldType;

					// For optional services, try to get them immediately without waiting
					if (!attribute.Required)
					{
						object optionalService = _serviceKitLocator.GetService(serviceType);
						return (fieldInfo, optionalService, attribute.Required);
					}

					// For required services, wait for them to become available
					object requiredService = await _serviceKitLocator.GetServiceAsync(serviceType, finalToken);
					return (fieldInfo, requiredService, attribute.Required);
				}).ToList();

				// Wait for all services
				var results = await Task.WhenAll(serviceTasks);

				// Check for missing required services
				var missingRequiredServices = results
					.Where(r => r.service == null && r.Required)
					.Select(r => r.fieldInfo.FieldType.Name)
					.ToList();

				if (missingRequiredServices.Any())
				{
					throw new ServiceInjectionException(
						$"Required services not available: {string.Join(", ", missingRequiredServices)}");
				}

				// Switch back to Unity thread for injection
				await SwitchToUnityThread(unityContext);

				// Inject all services (including null for optional services)
				foreach ((var field, object service, bool _) in results)
				{
					field.SetValue(_target, service);
				}
			}
			catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true)
			{
				throw new TimeoutException($"Service injection timed out after {_timeout} seconds");
			}
			catch (Exception ex) when (!(ex is ServiceInjectionException || ex is TimeoutException))
			{
				throw new ServiceInjectionException("Failed to inject services", ex);
			}
		}

		private List<FieldInfo> GetFieldsToInject(Type targetType)
		{
			return targetType
				.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
				.Where(f => f.GetCustomAttribute<InjectServiceAttribute>() != null)
				.ToList();
		}

		private async Task SwitchToUnityThread(SynchronizationContext unityContext)
		{
			if (SynchronizationContext.Current == unityContext) return;

			var tcs = new TaskCompletionSource<bool>();
			unityContext.Post(_ => tcs.SetResult(true), null);
			await tcs.Task;
		}

		private void DefaultErrorHandler(Exception exception)
		{
			Debug.LogError($"Failed to inject required services: {exception.Message}");
		}
	}
}
