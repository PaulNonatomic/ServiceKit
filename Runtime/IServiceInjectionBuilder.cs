using System;
using System.Threading;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#endif

namespace Nonatomic.ServiceKit
{
	public interface IServiceInjectionBuilder
	{
		IServiceInjectionBuilder WithCancellation(CancellationToken cancellationToken);
		IServiceInjectionBuilder WithTimeout();
		IServiceInjectionBuilder WithTimeout(float timeoutSeconds);
		IServiceInjectionBuilder WithErrorHandling(Action<Exception> errorHandler);
		IServiceInjectionBuilder WithErrorHandling();

		#if SERVICEKIT_UNITASK
		UniTask ExecuteAsync();
		UniTask ExecuteWithCancellationAsync(CancellationToken cancellationToken);
		#else
		System.Threading.Tasks.Task ExecuteAsync();
		System.Threading.Tasks.Task ExecuteWithCancellationAsync(CancellationToken cancellationToken);
		#endif
	}
}