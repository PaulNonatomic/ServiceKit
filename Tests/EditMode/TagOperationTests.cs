using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Nonatomic.ServiceKit.Tests.EditMode
{
	[TestFixture]
	public class TagOperationTests
	{
		private ServiceKitLocator _locator;

		[SetUp]
		public void Setup()
		{
			_locator = ScriptableObject.CreateInstance<ServiceKitLocator>();
		}

		[TearDown]
		public void TearDown()
		{
			if (_locator != null)
			{
				_locator.ClearServices();
				Object.DestroyImmediate(_locator);
				_locator = null;
			}
		}

		[Test]
		public void GetServicesWithTag_ReturnsCorrectServices()
		{
			// Arrange
			var serviceA = new TagServiceA();
			var serviceB = new TagServiceB();

			_locator.Register(serviceA).As<ITagServiceA>().WithTags("core").Ready();
			_locator.Register(serviceB).As<ITagServiceB>().WithTags("network").Ready();

			// Act
			var coreServices = _locator.GetServicesWithTag("core");
			var networkServices = _locator.GetServicesWithTag("network");

			// Assert
			Assert.AreEqual(1, coreServices.Count);
			Assert.AreEqual(serviceA, coreServices[0].Service);

			Assert.AreEqual(1, networkServices.Count);
			Assert.AreEqual(serviceB, networkServices[0].Service);
		}

		[Test]
		public void GetServicesWithTag_EmptyForNonexistentTag()
		{
			// Arrange
			var serviceA = new TagServiceA();
			_locator.Register(serviceA).As<ITagServiceA>().WithTags("core").Ready();

			// Act
			var result = _locator.GetServicesWithTag("nonexistent");

			// Assert
			Assert.AreEqual(0, result.Count);
		}

		[Test]
		public void GetServicesWithAnyTag_ReturnsServicesMatchingAnyTag()
		{
			// Arrange
			var serviceA = new TagServiceA();
			var serviceB = new TagServiceB();
			var serviceAB = new TagServiceAB();

			_locator.Register(serviceA).As<ITagServiceA>().WithTags("core").Ready();
			_locator.Register(serviceB).As<ITagServiceB>().WithTags("network").Ready();
			_locator.Register(serviceAB).As<TagServiceAB>().WithTags("core", "network").Ready();

			// Act
			var result = _locator.GetServicesWithAnyTag("core", "network");

			// Assert
			Assert.AreEqual(3, result.Count);
		}

		[Test]
		public void GetServicesWithAnyTag_EmptyWhenNoTagsMatch()
		{
			// Arrange
			var serviceA = new TagServiceA();
			_locator.Register(serviceA).As<ITagServiceA>().WithTags("core").Ready();

			// Act
			var result = _locator.GetServicesWithAnyTag("invalid1", "invalid2");

			// Assert
			Assert.AreEqual(0, result.Count);
		}

		[Test]
		public void GetServicesWithAllTags_RequiresAllTagsPresent()
		{
			// Arrange
			var serviceAB = new TagServiceAB();
			_locator.Register(serviceAB).As<TagServiceAB>().WithTags("core", "runtime").Ready();

			// Act
			var matchBoth = _locator.GetServicesWithAllTags("core", "runtime");
			var matchWithExtra = _locator.GetServicesWithAllTags("core", "runtime", "nonexistent");

			// Assert
			Assert.AreEqual(1, matchBoth.Count);
			Assert.AreEqual(serviceAB, matchBoth[0].Service);

			Assert.AreEqual(0, matchWithExtra.Count);
		}

		[Test]
		public void AddTagsToService_AddsToReadyService()
		{
			// Arrange
			var serviceA = new TagServiceA();
			_locator.Register(serviceA).As<ITagServiceA>().Ready();

			// Act
			_locator.AddTagsToService<ITagServiceA>(
				new ServiceTag { name = "dynamic" },
				new ServiceTag { name = "runtime" });

			// Assert
			var tags = _locator.GetServiceTags<ITagServiceA>();
			Assert.IsTrue(tags.Contains("dynamic"));
			Assert.IsTrue(tags.Contains("runtime"));
			Assert.AreEqual(2, tags.Count);
		}

		[Test]
		public void AddTagsToService_HandlesDuplicateTags()
		{
			// Arrange
			var serviceA = new TagServiceA();
			_locator.Register(serviceA).As<ITagServiceA>().WithTags("core").Ready();

			// Act
			_locator.AddTagsToService<ITagServiceA>(new ServiceTag { name = "core" });

			// Assert
			var tags = _locator.GetServiceTags<ITagServiceA>();
			Assert.AreEqual(1, tags.Count(t => t == "core"));
		}

		[Test]
		public void RemoveTagsFromService_RemovesTag()
		{
			// Arrange
			var serviceA = new TagServiceA();
			_locator.Register(serviceA).As<ITagServiceA>().WithTags("core", "runtime").Ready();

			// Act
			_locator.RemoveTagsFromService<ITagServiceA>("core");

			// Assert
			var tags = _locator.GetServiceTags<ITagServiceA>();
			Assert.AreEqual(1, tags.Count);
			Assert.IsFalse(tags.Contains("core"));
			Assert.IsTrue(tags.Contains("runtime"));
		}

		[Test]
		public void RemoveTagsFromService_HandlesNonexistentTag()
		{
			// Arrange
			var serviceA = new TagServiceA();
			_locator.Register(serviceA).As<ITagServiceA>().WithTags("core").Ready();

			// Act & Assert — should not throw
			Assert.DoesNotThrow(() =>
			{
				_locator.RemoveTagsFromService<ITagServiceA>("nonexistent");
			});

			var tags = _locator.GetServiceTags<ITagServiceA>();
			Assert.AreEqual(1, tags.Count);
			Assert.IsTrue(tags.Contains("core"));
		}

		[Test]
		public void GetServiceTags_ReturnsCorrectTagNames()
		{
			// Arrange
			var serviceA = new TagServiceA();
			_locator.Register(serviceA).As<ITagServiceA>().WithTags("core", "runtime", "player").Ready();

			// Act
			var tags = _locator.GetServiceTags<ITagServiceA>();

			// Assert
			Assert.AreEqual(3, tags.Count);
			Assert.IsTrue(tags.Contains("core"));
			Assert.IsTrue(tags.Contains("runtime"));
			Assert.IsTrue(tags.Contains("player"));
		}

		[Test]
		public void GetServiceTags_EmptyForUntaggedService()
		{
			// Arrange
			var serviceA = new TagServiceA();
			_locator.Register(serviceA).As<ITagServiceA>().Ready();

			// Act
			var tags = _locator.GetServiceTags<ITagServiceA>();

			// Assert
			Assert.AreEqual(0, tags.Count);
		}

		[Test]
		public void Tags_SurviveRegisterToReadyTransition()
		{
			// Arrange
			var serviceA = new TagServiceA();
			_locator.Register(serviceA)
				.As<ITagServiceA>()
				.WithTags("core", "runtime")
				.Register();

			// Verify tags exist after register-only
			var tagsBeforeReady = _locator.GetServiceTags<ITagServiceA>();
			Assert.AreEqual(2, tagsBeforeReady.Count);

			// Act — transition to ready
			_locator.ReadyService<ITagServiceA>();

			// Assert
			var tagsAfterReady = _locator.GetServiceTags<ITagServiceA>();
			Assert.AreEqual(2, tagsAfterReady.Count);
			Assert.IsTrue(tagsAfterReady.Contains("core"));
			Assert.IsTrue(tagsAfterReady.Contains("runtime"));
		}

		// Test interfaces and classes
		private interface ITagServiceA { }

		private interface ITagServiceB { }

		private class TagServiceA : ITagServiceA { }

		private class TagServiceB : ITagServiceB { }

		private class TagServiceAB : ITagServiceA, ITagServiceB { }
	}
}
