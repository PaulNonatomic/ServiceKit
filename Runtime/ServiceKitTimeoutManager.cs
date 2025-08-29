using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Nonatomic.ServiceKit
{
	public class ServiceKitTimeoutManager : MonoBehaviour
	{
		private static ServiceKitTimeoutManager _instance;

		public static ServiceKitTimeoutManager Instance
		{
			get
			{
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

		private List<(CancellationTokenSource cts, float endTime)> _timeouts = new List<(CancellationTokenSource, float)>();
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
				for (var i = _timeouts.Count - 1; i >= 0; i--)
				{
					if (_timeouts[i].cts != cts) continue;
					
					_timeouts.RemoveAt(i);
					break;
				}
			}
		}

		private void Update()
		{
			lock (_timeoutsLock)
			{
				// Use a more robust iteration pattern that handles concurrent modifications
				for (var i = _timeouts.Count - 1; i >= 0; i--)
				{
					// Double-check bounds to handle race conditions
					if (i >= _timeouts.Count) continue;
					
					try
					{
						var (cts, endTime) = _timeouts[i];
						if (cts.IsCancellationRequested)
						{
							// Verify index is still valid before removal
							if (i < _timeouts.Count)
							{
								_timeouts.RemoveAt(i);
							}
							continue;
						}

						if (Time.time < endTime) continue;
						
						cts.Cancel();
						// Verify index is still valid before removal
						if (i < _timeouts.Count)
						{
							_timeouts.RemoveAt(i);
						}
					}
					catch (ArgumentOutOfRangeException)
					{
						// Silently handle race condition where index became invalid
						// This can happen when timeouts are disposed concurrently with Update()
						break;
					}
				}
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
				_manager.RemoveTimeout(_cts);
			}
		}
	}
}