using System;
using System.Threading;
using System.Threading.Tasks;
using Nonatomic.ServiceKit;
using NSubstitute;
using NUnit.Framework;

namespace Tests.EditMode
{
	[TestFixture]
	public class ServiceKitTests
	{
		private IServiceKitLocator _mockServiceKitLocator;
		private IServiceInjectionBuilder _mockBuilder;

		[SetUp]
		public void Setup()
		{
			_mockServiceKitLocator = Substitute.For<IServiceKitLocator>();
			_mockBuilder = Substitute.For<IServiceInjectionBuilder>();

			// Setup fluent API chain
			_mockBuilder.WithCancellation(Arg.Any<CancellationToken>()).Returns(_mockBuilder);
			_mockBuilder.WithTimeout(Arg.Any<float>()).Returns(_mockBuilder);
			_mockBuilder.WithErrorHandling(Arg.Any<Action<Exception>>()).Returns(_mockBuilder);

			_mockServiceKitLocator.InjectServicesAsync(Arg.Any<object>()).Returns(_mockBuilder);
		}

		[Test]
		public void GetService_ReturnsRegisteredService()
		{
			// Arrange
			var expectedService = new PlayerService();
			_mockServiceKitLocator.GetService<IPlayerService>().Returns(expectedService);

			// Act
			var result = _mockServiceKitLocator.GetService<IPlayerService>();

			// Assert
			Assert.AreEqual(expectedService, result);
		}

		[Test]
		public void TryGetService_ReturnsTrueWhenServiceExists()
		{
			// Arrange
			var expectedService = new PlayerService();
			IPlayerService service;
			_mockServiceKitLocator.TryGetService(out service)
				.Returns(x =>
				{
					x[0] = expectedService;
					return true;
				});

			// Act
			bool result = _mockServiceKitLocator.TryGetService<IPlayerService>(out var actualService);

			// Assert
			Assert.IsTrue(result);
			Assert.AreEqual(expectedService, actualService);
		}

		[Test]
		public async Task GetServiceAsync_WaitsForServiceRegistration()
		{
			// Arrange
			var tcs = new TaskCompletionSource<IPlayerService>();
			_mockServiceKitLocator.GetServiceAsync<IPlayerService>(Arg.Any<CancellationToken>())
				.Returns(tcs.Task);

			// Act
			var serviceTask = _mockServiceKitLocator.GetServiceAsync<IPlayerService>();
			Assert.IsFalse(serviceTask.IsCompleted);

			var expectedService = new PlayerService();
			tcs.SetResult(expectedService);
			var result = await serviceTask;

			// Assert
			Assert.AreEqual(expectedService, result);
		}

		[Test]
		public void InjectServicesAsync_ReturnsFluentBuilder()
		{
			// Arrange
			var target = new TestClass();

			// Act
			var builder = _mockServiceKitLocator.InjectServicesAsync(target);

			// Assert
			Assert.IsNotNull(builder);
			Assert.AreEqual(_mockBuilder, builder);
		}

		private class TestClass
		{
			[InjectService] private IPlayerService _playerService;
		}
	}
}
