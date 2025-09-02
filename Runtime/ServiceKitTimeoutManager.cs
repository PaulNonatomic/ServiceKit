using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Nonatomic.ServiceKit
{
	public class ServiceKitTimeoutManager : MonoBehaviour
	{
		private static ServiceKitTimeoutManager _instance;
		private static bool _isCleaningUp;
		private static bool _applicationQuitting;
		private static readonly Stack<TimeoutRegistration> _registrationPool = new Stack<TimeoutRegistration>();

		public static ServiceKitTimeoutManager Instance
		{
			get
			{
				if (IsShuttingDown()) return null;
				if (HasExistingInstance()) return _instance;
				
				return CreateNewInstance();
			}
		}
		
		private static bool IsShuttingDown()
		{
			return _isCleaningUp || _applicationQuitting;
		}
		
		private static bool HasExistingInstance()
		{
			return _instance != null;
		}
		
		private static ServiceKitTimeoutManager CreateNewInstance()
		{
			var managerGameObject = CreateManagerGameObject();
			ConfigureForPersistence(managerGameObject);
			_instance = managerGameObject.AddComponent<ServiceKitTimeoutManager>();
			return _instance;
		}
		
		private static GameObject CreateManagerGameObject()
		{
			return new GameObject("ServiceKitTimeoutManager");
		}
		
		private static void ConfigureForPersistence(GameObject managerGameObject)
		{
#if !UNITY_EDITOR
			DontDestroyOnLoad(managerGameObject);
#else
			if (Application.isPlaying)
			{
				DontDestroyOnLoad(managerGameObject);
			}
#endif
		}

		private readonly List<(CancellationTokenSource cts, float endTime)> _activeTimeouts = new List<(CancellationTokenSource, float)>();
		private readonly object _timeoutsSyncLock = new object();
		private readonly List<int> _pendingRemovalIndices = new List<int>();

		public IDisposable RegisterTimeout(CancellationTokenSource cancellationSource, float durationInSeconds)
		{
			AddTimeoutToActiveList(cancellationSource, durationInSeconds);
			return CreateTimeoutRegistration(cancellationSource);
		}
		
		private void AddTimeoutToActiveList(CancellationTokenSource cancellationSource, float durationInSeconds)
		{
			var timeoutEndTime = Time.time + durationInSeconds;
			lock (_timeoutsSyncLock)
			{
				_activeTimeouts.Add((cancellationSource, timeoutEndTime));
			}
		}
		
		private TimeoutRegistration CreateTimeoutRegistration(CancellationTokenSource cancellationSource)
		{
			lock (_registrationPool)
			{
				return TryReusePooledRegistration(cancellationSource) ?? new TimeoutRegistration(this, cancellationSource);
			}
		}
		
		private TimeoutRegistration TryReusePooledRegistration(CancellationTokenSource cancellationSource)
		{
			if (_registrationPool.Count == 0) return null;
			
			var pooledRegistration = _registrationPool.Pop();
			pooledRegistration.Initialize(this, cancellationSource);
			return pooledRegistration;
		}

		private void RemoveTimeout(CancellationTokenSource cancellationSource)
		{
			lock (_timeoutsSyncLock)
			{
				var timeoutIndex = FindTimeoutIndexByCancellationSource(cancellationSource);
				if (IsValidTimeoutIndex(timeoutIndex))
				{
					_activeTimeouts.RemoveAt(timeoutIndex);
				}
			}
		}
		
		private bool IsValidTimeoutIndex(int index)
		{
			return index >= 0;
		}

		private int FindTimeoutIndexByCancellationSource(CancellationTokenSource targetSource)
		{
			for (var i = _activeTimeouts.Count - 1; i >= 0; i--)
			{
				if (_activeTimeouts[i].cts == targetSource) return i;
			}
			return -1;
		}

		private void Update()
		{
			if (ShouldSkipTimeoutProcessing()) return;
			
			ProcessAllActiveTimeouts();
		}
		
		private bool ShouldSkipTimeoutProcessing()
		{
			return _isCleaningUp || _applicationQuitting;
		}
		
		private void ProcessAllActiveTimeouts()
		{
			lock (_timeoutsSyncLock)
			{
				IdentifyExpiredTimeouts();
				RemoveProcessedTimeouts();
			}
		}

		private void IdentifyExpiredTimeouts()
		{
			_pendingRemovalIndices.Clear();
			var currentTime = Time.time;
			
			for (var i = _activeTimeouts.Count - 1; i >= 0; i--)
			{
				if (ShouldRemoveTimeout(i, currentTime))
				{
					_pendingRemovalIndices.Add(i);
				}
			}
		}
		
		private bool ShouldRemoveTimeout(int timeoutIndex, float currentTime)
		{
			try
			{
				var (cancellationSource, endTime) = _activeTimeouts[timeoutIndex];
				
				if (cancellationSource.IsCancellationRequested) return true;
				
				if (HasTimeoutExpired(currentTime, endTime))
				{
					cancellationSource.Cancel();
					return true;
				}
				
				return false;
			}
			catch (ObjectDisposedException)
			{
				return true;
			}
		}
		
		private bool HasTimeoutExpired(float currentTime, float endTime)
		{
			return currentTime >= endTime;
		}
		
		private void RemoveProcessedTimeouts()
		{
			for (var i = 0; i < _pendingRemovalIndices.Count; i++)
			{
				var removalIndex = _pendingRemovalIndices[i];
				if (IsIndexWithinBounds(removalIndex))
				{
					_activeTimeouts.RemoveAt(removalIndex);
				}
			}
		}
		
		private bool IsIndexWithinBounds(int index)
		{
			return index < _activeTimeouts.Count;
		}


		private class TimeoutRegistration : IDisposable
		{
			private const int MaxPoolSize = 20;
			private ServiceKitTimeoutManager _owningManager;
			private CancellationTokenSource _associatedCancellationSource;

			public TimeoutRegistration(ServiceKitTimeoutManager manager, CancellationTokenSource cancellationSource)
			{
				Initialize(manager, cancellationSource);
			}
			
			public void Initialize(ServiceKitTimeoutManager manager, CancellationTokenSource cancellationSource)
			{
				_owningManager = manager;
				_associatedCancellationSource = cancellationSource;
			}

			public void Dispose()
			{
				if (ShouldSkipDisposal()) return;
				
				RemoveTimeoutFromManager();
				ReturnToPoolIfPossible();
			}
			
			private bool ShouldSkipDisposal()
			{
				return _owningManager == null || _isCleaningUp;
			}
			
			private void RemoveTimeoutFromManager()
			{
				_owningManager.RemoveTimeout(_associatedCancellationSource);
			}
			
			private void ReturnToPoolIfPossible()
			{
				lock (_registrationPool)
				{
					if (!CanReturnToPool()) return;
					
					ResetForReuse();
					_registrationPool.Push(this);
				}
			}
			
			private bool CanReturnToPool()
			{
				return _registrationPool.Count < MaxPoolSize;
			}
			
			private void ResetForReuse()
			{
				_owningManager = null;
				_associatedCancellationSource = null;
			}
		}
		
		public static void Cleanup()
		{
			_isCleaningUp = true;
			_applicationQuitting = true;
			
			if (_instance != null)
			{
				CancelAllTimeouts();
				DestroyInstance();
			}
			
			_isCleaningUp = false;
			_applicationQuitting = false;
		}

		private static void CancelAllTimeouts()
		{
			if (_instance == null) return;
			
			lock (_instance._timeoutsSyncLock)
			{
				foreach (var (cancellationSource, _) in _instance._activeTimeouts)
				{
					SafelyCancelAndDispose(cancellationSource);
				}
				_instance._activeTimeouts.Clear();
			}
		}

		private static void SafelyCancelAndDispose(CancellationTokenSource cancellationSource)
		{
			try
			{
				if (!cancellationSource.IsCancellationRequested)
				{
					cancellationSource.Cancel();
				}
				cancellationSource.Dispose();
			}
			catch
			{
				// Silently ignore exceptions during cleanup
			}
		}

		private static void DestroyInstance()
		{
			if (_instance == null) return;
			
			var gameObjectToDestroy = _instance.gameObject;
			_instance = null;
			
			if (gameObjectToDestroy != null)
			{
				if (Application.isPlaying)
				{
					Destroy(gameObjectToDestroy);
				}
				else
				{
					DestroyImmediate(gameObjectToDestroy);
				}
			}
		}
		
		private void OnDestroy()
		{
			if (!IsThisInstanceActive()) return;
			
			MarkAsShuttingDown();
			CancelAllPendingTimeouts();
			ClearInstanceReference();
		}
		
		private bool IsThisInstanceActive()
		{
			return _instance == this;
		}
		
		private static void MarkAsShuttingDown()
		{
			_isCleaningUp = true;
			_applicationQuitting = true;
		}
		
		private static void ClearInstanceReference()
		{
			_instance = null;
		}
		
		private void OnApplicationQuit()
		{
			MarkAsShuttingDown();
			CancelAllPendingTimeouts();
		}
		
		private void OnApplicationPause(bool pauseStatus)
		{
			if (ShouldHandleEditorShutdown(pauseStatus))
			{
				MarkAsShuttingDown();
				CancelAllPendingTimeouts();
			}
		}
		
		private void OnApplicationFocus(bool hasFocus)
		{
			if (ShouldHandleEditorLostFocus(hasFocus))
			{
				MarkAsShuttingDown();
				CancelAllPendingTimeouts();
			}
		}
		
		private bool ShouldHandleEditorShutdown(bool pauseStatus)
		{
			return pauseStatus && Application.isEditor && !Application.isPlaying;
		}
		
		private bool ShouldHandleEditorLostFocus(bool hasFocus)
		{
			return !hasFocus && Application.isEditor && !Application.isPlaying;
		}

		private void CancelAllPendingTimeouts()
		{
			lock (_timeoutsSyncLock)
			{
				foreach (var (cancellationSource, _) in _activeTimeouts)
				{
					SafelyCancelAndDispose(cancellationSource);
				}
				_activeTimeouts.Clear();
			}
		}
	}
}