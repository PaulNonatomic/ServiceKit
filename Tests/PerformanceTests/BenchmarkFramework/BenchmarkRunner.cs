using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;

namespace Nonatomic.ServiceKit.Tests.PerformanceTests.BenchmarkFramework
{
	public class BenchmarkRunner
	{
		private readonly List<BenchmarkResult> _results = new List<BenchmarkResult>();
		
		public int WarmupIterations { get; set; } = 5;
		public int BenchmarkIterations { get; set; } = 100;
		
		public void Run(string name, Action action)
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			
			// Warmup
			for (int i = 0; i < WarmupIterations; i++)
			{
				action();
			}
			
			var timer = new BenchmarkTimer();
			
			// Actual benchmark
			for (int i = 0; i < BenchmarkIterations; i++)
			{
				timer.Start();
				action();
				timer.Stop();
			}
			
			var result = timer.GetResult(name, BenchmarkIterations);
			_results.Add(result);
		}
		
		public async Task RunAsync(string name, Func<Task> asyncAction)
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			
			// Warmup
			for (int i = 0; i < WarmupIterations; i++)
			{
				await asyncAction();
			}
			
			var timer = new BenchmarkTimer();
			
			// Actual benchmark
			for (int i = 0; i < BenchmarkIterations; i++)
			{
				timer.Start();
				await asyncAction();
				timer.Stop();
			}
			
			var result = timer.GetResult(name, BenchmarkIterations);
			_results.Add(result);
		}
		
		public void RunWithSetup<T>(string name, Func<T> setup, Action<T> action, Action<T> cleanup = null)
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			
			// Warmup
			for (int i = 0; i < WarmupIterations; i++)
			{
				var state = setup();
				action(state);
				cleanup?.Invoke(state);
			}
			
			var timer = new BenchmarkTimer();
			
			// Actual benchmark
			for (int i = 0; i < BenchmarkIterations; i++)
			{
				var state = setup();
				timer.Start();
				action(state);
				timer.Stop();
				cleanup?.Invoke(state);
			}
			
			var result = timer.GetResult(name, BenchmarkIterations);
			_results.Add(result);
		}
		
		public async Task RunWithSetupAsync<T>(string name, Func<T> setup, Func<T, Task> asyncAction, Action<T> cleanup = null)
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			
			// Warmup
			for (int i = 0; i < WarmupIterations; i++)
			{
				var state = setup();
				await asyncAction(state);
				cleanup?.Invoke(state);
			}
			
			var timer = new BenchmarkTimer();
			
			// Actual benchmark
			for (int i = 0; i < BenchmarkIterations; i++)
			{
				var state = setup();
				timer.Start();
				await asyncAction(state);
				timer.Stop();
				cleanup?.Invoke(state);
			}
			
			var result = timer.GetResult(name, BenchmarkIterations);
			_results.Add(result);
		}
		
		public void PrintResults()
		{
			UnityEngine.Debug.Log("=== Benchmark Results ===");
			foreach (var result in _results)
			{
				UnityEngine.Debug.Log(result.ToString());
				UnityEngine.Debug.Log("---");
			}
		}
		
		public List<BenchmarkResult> GetResults()
		{
			return new List<BenchmarkResult>(_results);
		}
		
		public void Clear()
		{
			_results.Clear();
		}
	}
}