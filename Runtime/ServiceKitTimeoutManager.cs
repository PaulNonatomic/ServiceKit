using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Nonatomic.ServiceKit
{
	public class ServiceKitTimeoutManager : MonoBehaviour
	{
		private static ServiceKitTimeoutManager _instance;
		private static bool _isCleaningUp = false;

		public static ServiceKitTimeoutManager Instance
		{
			get
			{
				if (_isCleaningUp)
				{
					return null;
				}
				
				if (_instance != null)
				{
					return _instance;
				}

				var go = new GameObject("ServiceKitTimeoutManager");
				
				// Only use DontDestroyOnLoad in Play Mode
				#if !UNITY_EDITOR
				DontDestroyOnLoad(go);
				#else
				if (Application.isPlaying)
				{
					DontDestroyOnLoad(go);
				}
				#endif
				
				_instance = go.AddComponent<ServiceKitTimeoutManager>();
				
				return _instance;
			}
		}

		private readonly List<(CancellationTokenSource cts, float endTime)> _timeouts = new List<(CancellationTokenSource, float)>();
		private readonly object _timeoutsLock = new object();

		public IDisposable RegisterTimeout(CancellationTokenSource cts, float duration)
		{
			var endTime = Time.time + duration;
			lock (_timeoutsLock)
			{
				_timeouts.Add((cts, endTime));
			}
			return new TimeoutRegistration(this, cts);
		}

		private void RemoveTimeout(CancellationTokenSource cts)
		{
			lock (_timeoutsLock)
			{
				var indexToRemove = FindTimeoutIndex(cts);
				if (indexToRemove >= 0)
				{
					_timeouts.RemoveAt(indexToRemove);
				}
			}
		}

		private int FindTimeoutIndex(CancellationTokenSource cts)
		{
			for (var i = _timeouts.Count - 1; i >= 0; i--)
			{
				if (_timeouts[i].cts == cts) return i;
			}
			return -1;
		}

		private void Update()
		{
			if (_isCleaningUp) return;
			
			lock (_timeoutsLock)
			{
				ProcessTimeouts();
			}
		}

		private void ProcessTimeouts()
		{
			for (var i = _timeouts.Count - 1; i >= 0; i--)
			{
				if (!IsValidTimeoutIndex(i)) continue;
				
				try
				{
					ProcessSingleTimeout(i);
				}
				catch (ArgumentOutOfRangeException)
				{
					break;
				}
				catch (ObjectDisposedException)
				{
					RemoveTimeoutIfValid(i);
				}
			}
		}

		private void ProcessSingleTimeout(int index)
		{
			var (cts, endTime) = _timeouts[index];
			
			if (cts.IsCancellationRequested)
			{
				RemoveTimeoutIfValid(index);
				return;
			}

			if (Time.time < endTime) return;
			
			cts.Cancel();
			RemoveTimeoutIfValid(index);
		}

		private bool IsValidTimeoutIndex(int index)
		{
			return index >= 0 && index < _timeouts.Count;
		}

		private void RemoveTimeoutIfValid(int index)
		{
			if (IsValidTimeoutIndex(index))
			{
				_timeouts.RemoveAt(index);
			}
		}

		private class TimeoutRegistration : IDisposable
		{
			private ServiceKitTimeoutManager _manager;
			private CancellationTokenSource _cts;

			public TimeoutRegistration(ServiceKitTimeoutManager manager, CancellationTokenSource cts)
			{
				_manager = manager;
				_cts = cts;
			}

			public void Dispose()
			{
				if (_manager != null && !_isCleaningUp)
				{
					_manager.RemoveTimeout(_cts);
				}
			}
		}
		
		public static void Cleanup()
		{
			_isCleaningUp = true;
			
			if (_instance != null)
			{
				CancelAllTimeouts();
				DestroyInstance();
			}
			
			_isCleaningUp = false;
		}

		private static void CancelAllTimeouts()
		{
			lock (_instance._timeoutsLock)
			{
				foreach (var (cts, _) in _instance._timeouts)
				{
					SafelyCancelAndDispose(cts);
				}
				_instance._timeouts.Clear();
			}
		}

		private static void SafelyCancelAndDispose(CancellationTokenSource cts)
		{
			try
			{
				if (!cts.IsCancellationRequested)
				{
					cts.Cancel();
				}
				cts.Dispose();
			}
			catch
			{
				// Ignore any exceptions during cleanup
			}
		}

		private static void DestroyInstance()
		{
			if (Application.isPlaying)
			{
				Destroy(_instance.gameObject);
			}
			else
			{
				DestroyImmediate(_instance.gameObject);
			}
			
			_instance = null;
		}
		
		private void OnDestroy()
		{
			if (_instance == this)
			{
				_instance = null;
			}
		}
		
		private void OnApplicationQuit()
		{
			_isCleaningUp = true;
			ClearAllTimeoutsOnQuit();
		}

		private void ClearAllTimeoutsOnQuit()
		{
			lock (_timeoutsLock)
			{
				foreach (var (cts, _) in _timeouts)
				{
					SafelyCancelAndDispose(cts);
				}
				_timeouts.Clear();
			}
		}
	}
}