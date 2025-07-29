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
				DontDestroyOnLoad(go);
				_instance = go.AddComponent<ServiceKitTimeoutManager>();
				
				return _instance;
			}
		}

		private List<(CancellationTokenSource cts, float endTime)> _timeouts = new List<(CancellationTokenSource, float)>();

		public IDisposable RegisterTimeout(CancellationTokenSource cts, float duration)
		{
			var endTime = Time.time + duration;
			_timeouts.Add((cts, endTime));
			return new TimeoutRegistration(this, cts);
		}

		private void RemoveTimeout(CancellationTokenSource cts)
		{
			for (var i = _timeouts.Count - 1; i >= 0; i--)
			{
				if (_timeouts[i].cts != cts) continue;
				
				_timeouts.RemoveAt(i);
				break;
			}
		}

		private void Update()
		{
			for (var i = _timeouts.Count - 1; i >= 0; i--)
			{
				var (cts, endTime) = _timeouts[i];
				if (cts.IsCancellationRequested)
				{
					_timeouts.RemoveAt(i);
					continue;
				}

				if (Time.time < endTime) continue;
				
				cts.Cancel();
				_timeouts.RemoveAt(i);
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