using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace Nonatomic.ServiceKit.Tests.PerformanceTests.BenchmarkFramework
{
	public class BenchmarkTimer
	{
		private readonly Stopwatch _stopwatch = new Stopwatch();
		private readonly List<long> _measurements = new List<long>();
		
		public void Start()
		{
			_stopwatch.Restart();
		}
		
		public void Stop()
		{
			_stopwatch.Stop();
			_measurements.Add(_stopwatch.ElapsedTicks);
		}
		
		public BenchmarkResult GetResult(string name, int iterations)
		{
			if (_measurements.Count == 0)
				return new BenchmarkResult { Name = name, Iterations = iterations };
			
			var ticksPerMillisecond = Stopwatch.Frequency / 1000.0;
			var timesInMs = _measurements.Select(t => t / ticksPerMillisecond).ToList();
			
			return new BenchmarkResult
			{
				Name = name,
				Iterations = iterations,
				TotalTimeMs = timesInMs.Sum(),
				AverageTimeMs = timesInMs.Average(),
				MinTimeMs = timesInMs.Min(),
				MaxTimeMs = timesInMs.Max(),
				MedianTimeMs = GetMedian(timesInMs),
				StandardDeviationMs = CalculateStandardDeviation(timesInMs)
			};
		}
		
		private double GetMedian(List<double> values)
		{
			var sorted = values.OrderBy(v => v).ToList();
			int count = sorted.Count;
			
			if (count == 0)
				return 0;
			
			if (count % 2 == 0)
				return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
			
			return sorted[count / 2];
		}
		
		private double CalculateStandardDeviation(List<double> values)
		{
			if (values.Count <= 1)
				return 0;
			
			double mean = values.Average();
			double sumOfSquares = values.Sum(v => Math.Pow(v - mean, 2));
			return Math.Sqrt(sumOfSquares / (values.Count - 1));
		}
		
		public void Clear()
		{
			_measurements.Clear();
		}
	}
	
	public class BenchmarkResult
	{
		public string Name { get; set; }
		public int Iterations { get; set; }
		public double TotalTimeMs { get; set; }
		public double AverageTimeMs { get; set; }
		public double MinTimeMs { get; set; }
		public double MaxTimeMs { get; set; }
		public double MedianTimeMs { get; set; }
		public double StandardDeviationMs { get; set; }
		
		public double OperationsPerSecond => Iterations / (TotalTimeMs / 1000.0);
		
		public override string ToString()
		{
			return $"{Name}: {Iterations} iterations\n" +
				   $"  Total: {TotalTimeMs:F3}ms\n" +
				   $"  Average: {AverageTimeMs:F3}ms\n" +
				   $"  Min: {MinTimeMs:F3}ms\n" +
				   $"  Max: {MaxTimeMs:F3}ms\n" +
				   $"  Median: {MedianTimeMs:F3}ms\n" +
				   $"  StdDev: {StandardDeviationMs:F3}ms\n" +
				   $"  Ops/sec: {OperationsPerSecond:F0}";
		}
	}
}