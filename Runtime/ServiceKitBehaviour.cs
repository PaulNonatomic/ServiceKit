using UnityEngine;

namespace Nonatomic.ServiceKit
{
	public abstract class ServiceKitBehaviour<T> : ServiceKitBehaviourBase<T> where T : class
	{
		[SerializeField] private ServiceKitLocator serviceKitLocator;

		protected override ServiceKitLocator ServiceKitLocator
		{
			get => serviceKitLocator;
			set => serviceKitLocator = value;
		}
	}
}