using System;
using System.Threading;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace Nonatomic.ServiceKit
{
	public interface IServiceInjectionBuilder
	{
		IServiceInjectionBuilder WithCancellation(CancellationToken cancellationToken);
		IServiceInjectionBuilder WithTimeout(float timeoutSeconds);
		IServiceInjectionBuilder WithTimeout();
		IServiceInjectionBuilder WithErrorHandling(Action<Exception> errorHandler);
		IServiceInjectionBuilder WithErrorHandling();

		void Execute();
#if SERVICEKIT_UNITASK
		UniTask ExecuteAsync();
#else
		Task ExecuteAsync();
#endif
	}
}
