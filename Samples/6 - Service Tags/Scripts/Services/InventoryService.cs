using System.Collections.Generic;
using UnityEngine;

namespace ServiceKitSamples.ServiceTagsExample
{
	/// <summary>
	/// Inventory service - tagged as "saveable" and "resettable".
	/// </summary>
	public class InventoryService : ISaveable, IResettable
	{
		private Dictionary<string, int> _items = new();

		public string SaveKey => "inventory";

		public void AddItem(string itemId, int count = 1)
		{
			if (!_items.ContainsKey(itemId))
				_items[itemId] = 0;
			_items[itemId] += count;
			Debug.Log($"[InventoryService] Added {count}x {itemId}. Total: {_items[itemId]}");
		}

		public int GetItemCount(string itemId)
		{
			return _items.TryGetValue(itemId, out var count) ? count : 0;
		}

		public object GetSaveData()
		{
			return new Dictionary<string, int>(_items);
		}

		public void LoadSaveData(object data)
		{
			if (data is Dictionary<string, int> items)
			{
				_items = new Dictionary<string, int>(items);
				Debug.Log($"[InventoryService] Loaded {_items.Count} item types");
			}
		}

		public void Reset()
		{
			_items.Clear();
			Debug.Log("[InventoryService] Reset - inventory cleared");
		}
	}
}
