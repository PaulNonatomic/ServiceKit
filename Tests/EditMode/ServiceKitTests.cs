using Nonatomic.ServiceKit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools; // For LogAssert
using UnityEngine.SceneManagement; // For SceneManager in tests
using System; // For Action<Exception>
using System.Linq; // For AggregateException check
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using Object = UnityEngine.Object; // For modifying private field in test

#if UNITY_EDITOR
using UnityEditor.SceneManagement; // For EditorSceneManager
#endif

namespace Tests.EditMode
{
    public interface ITestService {}
    public interface IAnotherTestService {}
    public class TestService : ITestService {}
    public class AnotherTestService : IAnotherTestService {}
    
    public class TestMonoBehaviourService : MonoBehaviour, ITestService 
    {
        public void SomeMethod() {}
    }


    public class InjectionTarget
    {
       [field: InjectService] public ITestService TestService { get; private set; }
       [field: InjectService] public IAnotherTestService AnotherTestService { get; private set; }
    }

    public class InjectionTargetBroken
    {
       public ITestService TestService { get; private set; }
    }

    public class ServiceKitTests
    {
        private ServiceKit _serviceKit;
        private Scene _previousActiveScene;
        private bool _testSceneCreated = false;
        private string _testScenePath = "Assets/_TestScene_ServiceKit.unity"; // Define a path for saving

        private void SetServiceKitDefaultTimeout(ServiceKit kit, float seconds)
        {
            FieldInfo field = typeof(ServiceKit).GetField("_defaultAsyncInjectionTimeoutInSeconds", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(kit, seconds);
            }
            else
            {
                Assert.Fail("_defaultAsyncInjectionTimeoutInSeconds field not found in ServiceKit. Test setup needs adjustment.");
            }
        }


        [SetUp]
        public void Setup()
        {
            _serviceKit = ScriptableObject.CreateInstance<ServiceKit>();
            
            #if UNITY_EDITOR
            // Ensure no unsaved changes dialog pops up, save current scene if needed.
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                if (!string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path))
                {
                    EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                }
                else
                {
                    // If the current scene is untitled and dirty, we might not be able to proceed safely.
                    // For tests, it's often better to start from a known state or a new scene.
                    Debug.LogWarning("Current scene is dirty and untitled. Test setup might be affected.");
                }
            }
            _previousActiveScene = EditorSceneManager.GetActiveScene();
            // Create a new scene in single mode to avoid issues with untitled scenes
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _testSceneCreated = true; 
            // The newly created scene is automatically the active one.
            #endif
        }

        [TearDown]
        public void Teardown()
        {
            if (_serviceKit != null)
            {
                Object.DestroyImmediate(_serviceKit);
                _serviceKit = null;
            }
            #if UNITY_EDITOR
            if (_testSceneCreated)
            {
                // Optionally, close the test scene if it's not needed anymore.
                // And restore the previous scene if it was valid.
                // However, for editor tests, simply ensuring a clean state for the next test
                // by creating a new scene in Setup (NewSceneMode.Single) is often sufficient.
                // Closing the current (test) scene might lead to an empty editor state or default scene.
                // If tests modify the scene and need cleanup, that should be specific.
                // For now, we'll just ensure the next test starts with a fresh scene via Setup.
                _testSceneCreated = false; // Reset flag
                
                // Restore the previously active scene if it was valid and different
                if (_previousActiveScene.IsValid() && _previousActiveScene.path != EditorSceneManager.GetActiveScene().path)
                {
                    // EditorSceneManager.OpenScene(_previousActiveScene.path); // This might be too slow/disruptive for tests
                }
                 // A simpler approach for teardown might be to just ensure the next test gets a fresh scene.
                 // The NewScene(Single) in Setup handles this.
            }
            #endif
        }

        [Test]
        public void RegisterAndRetrieveService_ServiceIsRetrievedSuccessfully()
        {
            var serviceInstance = new TestService();
            _serviceKit.RegisterService<ITestService>(serviceInstance);
            var retrievedService = _serviceKit.GetService<ITestService>();
            Assert.IsTrue(_serviceKit.HasService<ITestService>());
            Assert.IsNotNull(retrievedService);
            Assert.AreSame(serviceInstance, retrievedService);
        }
        
        [Test]
        public void RegisterMonoBehaviourService_SceneIsTracked()
        {
            #if UNITY_EDITOR 
            Scene currentTestScene = SceneManager.GetActiveScene(); // Get the scene created in Setup
            Assert.IsTrue(currentTestScene.IsValid(), "Test scene should be valid for MonoBehaviour service test.");

            var go = new GameObject("TestServiceGO");
            // GameObject is automatically created in the active scene
            TestMonoBehaviourService mbServiceInstance = go.AddComponent<TestMonoBehaviourService>();
            
            _serviceKit.RegisterService<ITestService>(mbServiceInstance);

            Scene retrievedScene = _serviceKit.GetSceneForService<ITestService>();
            Assert.IsTrue(retrievedScene.IsValid(), "Retrieved scene should be valid.");
            // Compare by handle or path as scene name might not be unique if not saved
            Assert.AreEqual(currentTestScene.handle, retrievedScene.handle, "Tracked scene handle should be the one the MonoBehaviour belongs to.");
            
            Object.DestroyImmediate(go);
            #else
            Assert.Ignore("RegisterMonoBehaviourService_SceneIsTracked test is Editor-only due to scene management.");
            #endif
        }

        [Test]
        public void GetSceneForService_NonMonoBehaviourService_ReturnsInvalidScene()
        {
            var serviceInstance = new TestService(); 
            _serviceKit.RegisterService<ITestService>(serviceInstance);

            Scene retrievedScene = _serviceKit.GetSceneForService<ITestService>();
            Assert.IsFalse(retrievedScene.IsValid(), "Scene for a non-MonoBehaviour service should be invalid.");
        }

        [Test]
        public void GetSceneForService_ServiceNotRegistered_ReturnsInvalidScene()
        {
            Scene retrievedScene = _serviceKit.GetSceneForService<ITestService>();
            Assert.IsFalse(retrievedScene.IsValid(), "Scene for a non-registered service should be invalid.");
        }


        [Test]
        public async Task RegisterAndInjectServicesAsync_MultipleServicesAreInjectedSuccessfully()
        {
           var serviceInstance = new TestService();
           _serviceKit.RegisterService<ITestService>(serviceInstance);
           var anotherServiceInstance = new AnotherTestService();
           _serviceKit.RegisterService<IAnotherTestService>(anotherServiceInstance);
           var injectionTarget = new InjectionTarget();

           await _serviceKit.InjectServicesAsync(injectionTarget);

           Assert.IsTrue(_serviceKit.HasService<ITestService>());
           Assert.IsNotNull(injectionTarget.TestService);
           Assert.AreSame(serviceInstance, injectionTarget.TestService);
           Assert.IsTrue(_serviceKit.HasService<IAnotherTestService>());
           Assert.IsNotNull(injectionTarget.AnotherTestService);
           Assert.AreSame(anotherServiceInstance, injectionTarget.AnotherTestService);
        }

        [Test]
        public async Task RegisterAndInjectServicesAsync_SingleServiceIsInjectedSuccessfully()
        {
           var serviceInstance = new TestService();
           _serviceKit.RegisterService<ITestService>(serviceInstance);
           var injectionTarget = new InjectionTarget();
           bool caughtException = false;

           try
           {
                await _serviceKit.InjectServicesAsync(injectionTarget)
                               .WithTimeout(TimeSpan.FromMilliseconds(100));
           }
           catch(OperationCanceledException) { caughtException = true; }
           catch(AggregateException ae) when (ae.InnerExceptions.OfType<OperationCanceledException>().Any()) { caughtException = true; }

           Assert.IsTrue(caughtException, "Operation should be cancelled due to timeout on missing service.");
           Assert.IsTrue(_serviceKit.HasService<ITestService>());
           Assert.IsNotNull(injectionTarget.TestService, "The registered service should still be injected before timeout/cancellation.");
           Assert.AreSame(serviceInstance, injectionTarget.TestService);
           Assert.IsNull(injectionTarget.AnotherTestService, "The unregistered service should remain null.");
        }

        [Test]
        public async Task InjectServicesAsync_FieldWithoutAttribute_IsNotInjected()
        {
           var serviceInstance = new TestService();
           _serviceKit.RegisterService<ITestService>(serviceInstance);
           var injectionTarget = new InjectionTargetBroken();

           await _serviceKit.InjectServicesAsync(injectionTarget);

           Assert.IsTrue(_serviceKit.HasService<ITestService>());
           Assert.IsNull(injectionTarget.TestService);
           Assert.AreNotSame(serviceInstance, injectionTarget.TestService);
        }

        [Test]
        public async Task InjectServicesAsync_ServicesRegisteredLater_InjectsWhenAvailable()
        {
            var serviceInstance = new TestService();
            var anotherServiceInstance = new AnotherTestService();
            var injectionTarget = new InjectionTarget();

            Task injectionTask = _serviceKit.InjectServicesAsync(injectionTarget).ExecuteAsync();

            Assert.IsNull(injectionTarget.TestService);
            Assert.IsNull(injectionTarget.AnotherTestService);

            await Task.Delay(30);
            _serviceKit.RegisterService<ITestService>(serviceInstance);

            await Task.Delay(30);
            _serviceKit.RegisterService<IAnotherTestService>(anotherServiceInstance);

            await injectionTask;

            Assert.IsNotNull(injectionTarget.TestService);
            Assert.AreSame(serviceInstance, injectionTarget.TestService);
            Assert.IsNotNull(injectionTarget.AnotherTestService);
            Assert.AreSame(anotherServiceInstance, injectionTarget.AnotherTestService);
        }

        [Test]
        public async Task InjectServicesAsync_WithCancellation_StopsInjection()
        {
            var injectionTarget = new InjectionTarget();
            using (var cts = new CancellationTokenSource())
            {
                Task injectionTask = _serviceKit.InjectServicesAsync(injectionTarget)
                                           .WithCancellation(cts.Token)
                                           .ExecuteAsync();

                Assert.IsFalse(injectionTask.IsCompleted);
                await Task.Delay(5);
                cts.Cancel();

                try { await injectionTask; }
                catch (OperationCanceledException) { Assert.IsTrue(injectionTask.IsCanceled || injectionTask.IsFaulted); }
                catch (AggregateException ae) { Assert.IsTrue(ae.InnerExceptions.OfType<OperationCanceledException>().Any()); }

                Assert.IsNull(injectionTarget.TestService, "TestService should not be injected if operation was cancelled early.");
                Assert.IsNull(injectionTarget.AnotherTestService, "AnotherTestService should not be injected if operation was cancelled early.");
            }
        }

        [Test]
        public async Task InjectServicesAsync_WithExplicitTimeout_CancelsIfNotCompletedInTime()
        {
            var injectionTarget = new InjectionTarget();
            bool caughtOcOrAeWithOc = false;
            try
            {
                await _serviceKit.InjectServicesAsync(injectionTarget)
                               .WithTimeout(TimeSpan.FromMilliseconds(50)); 
            }
            catch (OperationCanceledException)
            {
                caughtOcOrAeWithOc = true;
            }
            catch (AggregateException ae) when (ae.InnerExceptions.OfType<OperationCanceledException>().Any())
            {
                caughtOcOrAeWithOc = true;
            }

            Assert.IsTrue(caughtOcOrAeWithOc, "OperationCanceledException (due to explicit timeout) was expected.");
            Assert.IsNull(injectionTarget.TestService, "Service should not be injected due to explicit timeout.");
            Assert.IsNull(injectionTarget.AnotherTestService, "Service should not be injected due to explicit timeout.");
        }
        
        [Test]
        public async Task InjectServicesAsync_ParameterlessWithTimeout_UsesInstanceDefaultAndCancels()
        {
            var injectionTarget = new InjectionTarget();
            bool caughtOcOrAeWithOc = false;
            
            SetServiceKitDefaultTimeout(_serviceKit, 0.05f); // 50 milliseconds

            try
            {
                await _serviceKit.InjectServicesAsync(injectionTarget)
                               .WithTimeout(); 
            }
            catch (OperationCanceledException) { caughtOcOrAeWithOc = true; }
            catch (AggregateException ae) when (ae.InnerExceptions.OfType<OperationCanceledException>().Any()) { caughtOcOrAeWithOc = true; }

            Assert.IsTrue(caughtOcOrAeWithOc, "OperationCanceledException (due to instance default timeout) was expected.");
            Assert.IsNull(injectionTarget.TestService, "Service should not be injected due to instance default timeout.");
        }


        [Test]
        public async Task InjectServicesAsync_WithCustomErrorHandler_CallsHandlerAndDoesNotThrow()
        {
            var injectionTarget = new InjectionTarget();
            Exception caughtExceptionByHandler = null;
            bool handlerCalled = false;

            LogAssert.ignoreFailingMessages = true;
            try
            {
                 await _serviceKit.InjectServicesAsync(injectionTarget)
                               .WithTimeout(TimeSpan.FromMilliseconds(50))
                               .WithErrorHandling(ex => { 
                                   handlerCalled = true;
                                   caughtExceptionByHandler = ex;
                               });
            }
            finally { LogAssert.ignoreFailingMessages = false; }

            Assert.IsTrue(handlerCalled, "Custom error handler should have been called.");
            Assert.IsNotNull(caughtExceptionByHandler, "Exception should have been passed to custom handler.");
            Assert.IsTrue(caughtExceptionByHandler is OperationCanceledException ||
                          (caughtExceptionByHandler is AggregateException ae && ae.InnerExceptions.OfType<OperationCanceledException>().Any()),
                "Exception passed to custom handler should be OperationCanceledException or AggregateException containing it due to timeout.");
            Assert.IsNull(injectionTarget.TestService, "Service should not be injected when custom error handler is used for timeout.");
        }

        [Test]
        public async Task InjectServicesAsync_WithCustomErrorHandler_PropagatesIfHandlerRethrows()
        {
            var injectionTarget = new InjectionTarget();
            bool handlerCalled = false;
            bool exceptionPropagated = false;

            LogAssert.ignoreFailingMessages = true;
            try
            {
                await _serviceKit.InjectServicesAsync(injectionTarget)
                               .WithTimeout(TimeSpan.FromMilliseconds(50))
                               .WithErrorHandling(ex => { 
                                   handlerCalled = true;
                                   throw ex;
                               });
            }
            catch (OperationCanceledException) { exceptionPropagated = true; }
            catch (AggregateException ae) when (ae.InnerExceptions.OfType<OperationCanceledException>().Any()) { exceptionPropagated = true; }
            finally { LogAssert.ignoreFailingMessages = false; }

            Assert.IsTrue(handlerCalled, "Custom error handler should have been called.");
            Assert.IsTrue(exceptionPropagated, "Exception should have propagated after custom handler re-threw.");
        }

        // --- New Tests for ErrorHandlingMode ---

        [Test]
        public async Task InjectServicesAsync_WithErrorHandlingModeError_ThrowsException()
        {
            var injectionTarget = new InjectionTarget();
            bool exceptionThrown = false;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                await _serviceKit.InjectServicesAsync(injectionTarget)
                               .WithTimeout(TimeSpan.FromMilliseconds(50))
                               .WithErrorHandling(ErrorHandlingMode.Error); 
            }
            catch (OperationCanceledException) { exceptionThrown = true; }
            catch (AggregateException ae) when (ae.InnerExceptions.OfType<OperationCanceledException>().Any()) { exceptionThrown = true; }
            finally { LogAssert.ignoreFailingMessages = false; }

            Assert.IsTrue(exceptionThrown, "Exception should have been thrown for ErrorHandlingMode.Error.");
        }
        
        [Test]
        public async Task InjectServicesAsync_WithErrorHandlingDefault_ThrowsException()
        {
            var injectionTarget = new InjectionTarget();
            bool exceptionThrown = false;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                await _serviceKit.InjectServicesAsync(injectionTarget)
                               .WithTimeout(TimeSpan.FromMilliseconds(50));
            }
            catch (OperationCanceledException) { exceptionThrown = true; }
            catch (AggregateException ae) when (ae.InnerExceptions.OfType<OperationCanceledException>().Any()) { exceptionThrown = true; }
            finally { LogAssert.ignoreFailingMessages = false; }

            Assert.IsTrue(exceptionThrown, "Exception should have been thrown for default error handling (Error mode).");
        }

        [Test]
        public async Task InjectServicesAsync_WithErrorHandlingNoParams_ThrowsException()
        {
            var injectionTarget = new InjectionTarget();
            bool exceptionThrown = false;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                await _serviceKit.InjectServicesAsync(injectionTarget)
                               .WithTimeout(TimeSpan.FromMilliseconds(50))
                               .WithErrorHandling(); 
            }
            catch (OperationCanceledException) { exceptionThrown = true; }
            catch (AggregateException ae) when (ae.InnerExceptions.OfType<OperationCanceledException>().Any()) { exceptionThrown = true; }
            finally { LogAssert.ignoreFailingMessages = false; }

            Assert.IsTrue(exceptionThrown, "Exception should have been thrown for WithErrorHandling() (Error mode).");
        }


        [Test]
        public async Task InjectServicesAsync_WithErrorHandlingModeWarning_LogsWarningAndDoesNotThrow()
        {
            var injectionTarget = new InjectionTarget();
            LogAssert.ignoreFailingMessages = true; 

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("ServiceKit Injection Error"));

            await _serviceKit.InjectServicesAsync(injectionTarget)
                           .WithTimeout(TimeSpan.FromMilliseconds(50))
                           .WithErrorHandling(ErrorHandlingMode.Warning);

            LogAssert.ignoreFailingMessages = false;
            Assert.Pass("Operation completed without throwing, warning was expected.");
        }

        [Test]
        public async Task InjectServicesAsync_WithErrorHandlingModeLog_LogsMessageAndDoesNotThrow()
        {
            var injectionTarget = new InjectionTarget();
            LogAssert.ignoreFailingMessages = true;

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("ServiceKit Injection Error"));

            await _serviceKit.InjectServicesAsync(injectionTarget)
                           .WithTimeout(TimeSpan.FromMilliseconds(50))
                           .WithErrorHandling(ErrorHandlingMode.Log);

            LogAssert.ignoreFailingMessages = false;
            Assert.Pass("Operation completed without throwing, log message was expected.");
        }

        [Test]
        public async Task InjectServicesAsync_WithErrorHandlingModeSilent_DoesNotThrowOrLog()
        {
            var injectionTarget = new InjectionTarget();

            await _serviceKit.InjectServicesAsync(injectionTarget)
                           .WithTimeout(TimeSpan.FromMilliseconds(50))
                           .WithErrorHandling(ErrorHandlingMode.Silent);

            Assert.Pass("Operation completed without throwing or logging (Silent mode).");
        }


        // --- Async GetServiceAsync Tests ---
        [Test]
        public async Task GetServiceAsync_ServiceAlreadyRegistered_ReturnsServiceImmediately()
        {
            var serviceInstance = new TestService();
            _serviceKit.RegisterService<ITestService>(serviceInstance);
            var retrievedService = await _serviceKit.GetServiceAsync<ITestService>();
            Assert.IsNotNull(retrievedService);
            Assert.AreSame(serviceInstance, retrievedService);
        }

        [Test]
        public async Task GetServiceAsync_ServiceRegisteredLater_ReturnsServiceWhenRegistered()
        {
            var serviceInstance = new TestService();
            Task<ITestService> retrievalTask = _serviceKit.GetServiceAsync<ITestService>();
            Assert.IsFalse(retrievalTask.IsCompleted);
            await Task.Delay(10);
            _serviceKit.RegisterService<ITestService>(serviceInstance);
            var retrievedService = await retrievalTask;
            Assert.IsNotNull(retrievedService);
            Assert.AreSame(serviceInstance, retrievedService);
        }

        [Test]
        public async Task GetServiceAsync_WithCancellation_TaskIsCancelled()
        {
            using (var cts = new CancellationTokenSource())
            {
                Task<ITestService> retrievalTask = _serviceKit.GetServiceAsync<ITestService>(cts.Token);
                Assert.IsFalse(retrievalTask.IsCompleted);
                cts.Cancel();
                try { await retrievalTask; Assert.Fail("Task should have been cancelled."); }
                catch (OperationCanceledException ex)
                {
                    Assert.IsTrue(retrievalTask.IsCanceled);
                    Assert.AreEqual(cts.Token, ex.CancellationToken);
                }
                catch (Exception ex)
                {
                     Assert.Fail($"Expected OperationCanceledException but got {ex.GetType().Name}");
                }
            }
        }
    }
}