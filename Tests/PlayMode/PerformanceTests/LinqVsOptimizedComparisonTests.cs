using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Nonatomic.ServiceKit.Tests.PlayMode
{
	/// <summary>
	/// Compares memory allocations between LINQ and optimized implementations
	/// </summary>
	public class LinqVsOptimizedComparisonTests
	{
		private MemoryAllocationTracker _tracker;
		private List<TestItem> _testData;
		private const int DataSize = 100;
		private const int Iterations = 1000;
		
		private class TestItem
		{
			public int Id { get; set; }
			public string Name { get; set; }
			public string Category { get; set; }
			public List<string> Tags { get; set; }
		}
		
		[SetUp]
		public void Setup()
		{
			_tracker = new MemoryAllocationTracker();
			_testData = new List<TestItem>();
			
			for (int i = 0; i < DataSize; i++)
			{
				_testData.Add(new TestItem
				{
					Id = i,
					Name = $"Item_{i}",
					Category = i % 2 == 0 ? "Even" : "Odd",
					Tags = new List<string> { $"tag_{i % 5}", $"tag_{i % 3}" }
				});
			}
		}
		
		[Test]
		public void Compare_WhereSelect_Allocations()
		{
			// LINQ version
			_tracker.MeasureOperationWithWarmup(
				"LINQ - Where().Select().ToList()",
				() => {
					var result = _testData
						.Where(x => x.Category == "Even")
						.Select(x => x.Name)
						.ToList();
				},
				10,
				Iterations
			);
			
			// Optimized version
			_tracker.MeasureOperationWithWarmup(
				"Optimized - Manual loop",
				() => {
					var result = new List<string>();
					foreach (var item in _testData)
					{
						if (item.Category == "Even")
						{
							result.Add(item.Name);
						}
					}
				},
				10,
				Iterations
			);
			
			// Optimized with capacity
			_tracker.MeasureOperationWithWarmup(
				"Optimized - Pre-sized list",
				() => {
					var result = new List<string>(DataSize / 2);
					foreach (var item in _testData)
					{
						if (item.Category == "Even")
						{
							result.Add(item.Name);
						}
					}
				},
				10,
				Iterations
			);
			
			PrintComparison();
		}
		
		[Test]
		public void Compare_Any_Allocations()
		{
			var searchTags = new[] { "tag_1", "tag_2" };
			
			// LINQ version
			_tracker.MeasureOperationWithWarmup(
				"LINQ - Any()",
				() => {
					foreach (var item in _testData)
					{
						var hasTag = item.Tags.Any(t => searchTags.Contains(t));
					}
				},
				10,
				Iterations
			);
			
			// Optimized version
			_tracker.MeasureOperationWithWarmup(
				"Optimized - Nested loops",
				() => {
					foreach (var item in _testData)
					{
						var hasTag = false;
						for (int i = 0; i < item.Tags.Count; i++)
						{
							for (int j = 0; j < searchTags.Length; j++)
							{
								if (item.Tags[i] == searchTags[j])
								{
									hasTag = true;
									break;
								}
							}
							if (hasTag) break;
						}
					}
				},
				10,
				Iterations
			);
			
			PrintComparison();
		}
		
		[Test]
		public void Compare_StringJoin_Allocations()
		{
			var strings = new[] { "Hello", "World", "From", "ServiceKit" };
			
			// String.Join with LINQ
			_tracker.MeasureOperationWithWarmup(
				"String.Join + LINQ",
				() => {
					var result = string.Join(", ", strings.Select(s => s.ToUpper()));
				},
				10,
				Iterations
			);
			
			// String concatenation
			_tracker.MeasureOperationWithWarmup(
				"String concatenation (+)",
				() => {
					var result = "";
					for (int i = 0; i < strings.Length; i++)
					{
						if (i > 0) result += ", ";
						result += strings[i].ToUpper();
					}
				},
				10,
				Iterations
			);
			
			// StringBuilder
			_tracker.MeasureOperationWithWarmup(
				"StringBuilder",
				() => {
					var sb = new System.Text.StringBuilder();
					for (int i = 0; i < strings.Length; i++)
					{
						if (i > 0) sb.Append(", ");
						sb.Append(strings[i].ToUpper());
					}
					var result = sb.ToString();
				},
				10,
				Iterations
			);
			
			// StringBuilder with pooling (simulated)
			var pooledStringBuilder = new System.Text.StringBuilder(256);
			_tracker.MeasureOperationWithWarmup(
				"StringBuilder (pooled)",
				() => {
					pooledStringBuilder.Clear();
					for (int i = 0; i < strings.Length; i++)
					{
						if (i > 0) pooledStringBuilder.Append(", ");
						pooledStringBuilder.Append(strings[i].ToUpper());
					}
					var result = pooledStringBuilder.ToString();
				},
				10,
				Iterations
			);
			
			PrintComparison();
		}
		
		[Test]
		public void Compare_CollectionCreation_Allocations()
		{
			// New List every time
			_tracker.MeasureOperationWithWarmup(
				"new List<T>()",
				() => {
					var list = new List<string>();
					for (int i = 0; i < 10; i++)
					{
						list.Add($"Item_{i}");
					}
				},
				10,
				Iterations
			);
			
			// Pre-sized List
			_tracker.MeasureOperationWithWarmup(
				"new List<T>(capacity)",
				() => {
					var list = new List<string>(10);
					for (int i = 0; i < 10; i++)
					{
						list.Add($"Item_{i}");
					}
				},
				10,
				Iterations
			);
			
			// Array
			_tracker.MeasureOperationWithWarmup(
				"new T[size]",
				() => {
					var array = new string[10];
					for (int i = 0; i < 10; i++)
					{
						array[i] = $"Item_{i}";
					}
				},
				10,
				Iterations
			);
			
			// Simulated pooling
			var pooledList = new List<string>(10);
			_tracker.MeasureOperationWithWarmup(
				"Pooled List",
				() => {
					pooledList.Clear();
					for (int i = 0; i < 10; i++)
					{
						pooledList.Add($"Item_{i}");
					}
				},
				10,
				Iterations
			);
			
			PrintComparison();
		}
		
		[Test]
		public void Measure_Boxing_Allocations()
		{
			int value = 42;
			
			// Boxing
			_tracker.MeasureOperationWithWarmup(
				"Boxing (object cast)",
				() => {
					object boxed = value;
					int unboxed = (int)boxed;
				},
				10,
				Iterations * 10
			);
			
			// Generic (no boxing)
			_tracker.MeasureOperationWithWarmup(
				"Generic<T> (no boxing)",
				() => {
					ProcessGeneric(value);
				},
				10,
				Iterations * 10
			);
			
			// Interface boxing
			_tracker.MeasureOperationWithWarmup(
				"Interface (IComparable)",
				() => {
					IComparable comparable = value;
					var result = comparable.CompareTo(41);
				},
				10,
				Iterations * 10
			);
			
			PrintComparison();
		}
		
		private void ProcessGeneric<T>(T value)
		{
			var temp = value;
		}
		
		private void PrintComparison()
		{
			Debug.Log("=== Allocation Comparison ===");
			var results = _tracker.GetResults();
			
			// Find the best performer
			var minBytes = double.MaxValue;
			string bestMethod = "";
			
			foreach (var kvp in results)
			{
				if (kvp.Value.BytesPerOperation < minBytes)
				{
					minBytes = kvp.Value.BytesPerOperation;
					bestMethod = kvp.Key;
				}
			}
			
			// Print comparison
			foreach (var kvp in results)
			{
				var result = kvp.Value;
				var ratio = minBytes > 0 ? result.BytesPerOperation / minBytes : 0;
				var improvement = ratio > 1 ? $" ({ratio:F1}x more allocations)" : " ✅ BEST";
				
				Debug.Log($"{result.OperationName}: {result.BytesPerOperation:F1} bytes/op{improvement}");
			}
			
			Debug.Log($"Winner: {bestMethod} with {minBytes:F1} bytes/op");
		}
	}
}