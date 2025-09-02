using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace Nonatomic.ServiceKit.Tests.PlayMode
{
	/// <summary>
	/// Tracks memory allocations for performance testing
	/// </summary>
	public class MemoryAllocationTracker
	{
		private long _startTotalMemory;
		private long _startGCMemory;
		private int _startGCCount;
		private readonly Dictionary<string, AllocationResult> _results = new Dictionary<string, AllocationResult>();
		
		public class AllocationResult
		{
			public string OperationName { get; set; }
			public long BytesAllocated { get; set; }
			public int GCCollections { get; set; }
			public int Iterations { get; set; }
			public double BytesPerOperation { get; set; }
			public TimeSpan Duration { get; set; }
			
			public override string ToString()
			{
				return $"{OperationName}: {BytesPerOperation:F1} bytes/op, Total: {BytesAllocated:N0} bytes, GCs: {GCCollections}, Time: {Duration.TotalMilliseconds:F2}ms";
			}
		}
		
		public void StartTracking()
		{
			// Force GC before measuring to get clean baseline
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			
			_startGCCount = GC.CollectionCount(0);
			_startTotalMemory = Profiler.GetTotalAllocatedMemoryLong();
			_startGCMemory = GC.GetTotalMemory(false);
		}
		
		public AllocationResult StopTracking(string operationName, int iterations = 1)
		{
			var endTotalMemory = Profiler.GetTotalAllocatedMemoryLong();
			var endGCMemory = GC.GetTotalMemory(false);
			var endGCCount = GC.CollectionCount(0);
			
			var result = new AllocationResult
			{
				OperationName = operationName,
				BytesAllocated = Math.Max(0, endGCMemory - _startGCMemory),
				GCCollections = endGCCount - _startGCCount,
				Iterations = iterations,
				BytesPerOperation = Math.Max(0, (endGCMemory - _startGCMemory) / (double)iterations)
			};
			
			_results[operationName] = result;
			return result;
		}
		
		public void MeasureOperation(string operationName, Action operation, int iterations = 1)
		{
			StartTracking();
			var startTime = DateTime.Now;
			
			for (int i = 0; i < iterations; i++)
			{
				operation();
			}
			
			var result = StopTracking(operationName, iterations);
			result.Duration = DateTime.Now - startTime;
		}
		
		public void MeasureOperationWithWarmup(string operationName, Action operation, int warmupIterations = 10, int iterations = 100)
		{
			// Warmup to ensure JIT compilation and caches are primed
			for (int i = 0; i < warmupIterations; i++)
			{
				operation();
			}
			
			MeasureOperation(operationName, operation, iterations);
		}
		
		public Dictionary<string, AllocationResult> GetResults()
		{
			return new Dictionary<string, AllocationResult>(_results);
		}
		
		public void PrintResults()
		{
			Debug.Log("=== Memory Allocation Results ===");
			foreach (var kvp in _results)
			{
				Debug.Log(kvp.Value.ToString());
			}
		}
		
		public void SaveResultsToCSV(string filePath)
		{
			using (var writer = new System.IO.StreamWriter(filePath))
			{
				writer.WriteLine("Operation,BytesPerOp,TotalBytes,GCCollections,Iterations,DurationMs");
				foreach (var kvp in _results)
				{
					var r = kvp.Value;
					writer.WriteLine($"{r.OperationName},{r.BytesPerOperation:F1},{r.BytesAllocated},{r.GCCollections},{r.Iterations},{r.Duration.TotalMilliseconds:F2}");
				}
			}
		}
	}
}