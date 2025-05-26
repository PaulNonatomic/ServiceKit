using System;
using System.Threading;
using System.Threading.Tasks;

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
		Task ExecuteAsync();
	}
}
