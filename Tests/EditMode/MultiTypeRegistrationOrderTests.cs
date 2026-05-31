using Nonatomic.ServiceKit;
using NSubstitute;
using NUnit.Framework;

namespace Tests.EditMode
{
	/// <summary>
	/// Verifies that a multi-type fluent registration registers every type before readying any
	/// of them, so a consumer woken by one type never observes the others as not-yet-registered.
	/// </summary>
	[TestFixture]
	public class MultiTypeRegistrationOrderTests
	{
		public interface IFoo { }
		public interface IBar { }

		public class MultiService : IFoo, IBar { }

		[Test]
		public void FluentReady_RegistersAllTypesBeforeReadyingAny()
		{
			var locator = Substitute.For<IServiceKitLocator>();
			var service = new MultiService();

			new ServiceRegistrationBuilder(locator, service)
				.As<IFoo>()
				.As<IBar>()
				.Ready();

			Received.InOrder(() =>
			{
				locator.RegisterService(typeof(IFoo), service, Arg.Any<string>());
				locator.RegisterService(typeof(IBar), service, Arg.Any<string>());
				locator.ReadyService(typeof(IFoo));
				locator.ReadyService(typeof(IBar));
			});
		}
	}
}
