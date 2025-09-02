using System;
using System.Collections.Generic;
using System.Text;

namespace Nonatomic.ServiceKit
{
	internal static class ServiceKitObjectPool
	{
		private const int MaxPoolSize = 10;
		private const int InitialStringBuilderCapacity = 256;
		private const int MaxStringBuilderCapacity = 1024;
		
		private static readonly Stack<List<ServiceInfo>> ServiceInfoListPool = new();
		private static readonly Stack<List<Type>> TypeListPool = new();
		private static readonly Stack<List<string>> StringListPool = new();
		private static readonly Stack<StringBuilder> StringBuilderPool = new();
		
		public static List<ServiceInfo> RentServiceInfoList()
		{
			return RentFromPool(ServiceInfoListPool) ?? new List<ServiceInfo>();
		}
		
		public static void ReturnServiceInfoList(List<ServiceInfo> list)
		{
			ReturnToPool(ServiceInfoListPool, list);
		}
		
		public static List<Type> RentTypeList()
		{
			return RentFromPool(TypeListPool) ?? new List<Type>();
		}
		
		public static void ReturnTypeList(List<Type> list)
		{
			ReturnToPool(TypeListPool, list);
		}
		
		public static List<string> RentStringList()
		{
			return RentFromPool(StringListPool) ?? new List<string>();
		}
		
		public static void ReturnStringList(List<string> list)
		{
			ReturnToPool(StringListPool, list);
		}
		
		public static StringBuilder RentStringBuilder()
		{
			lock (StringBuilderPool)
			{
				return RentFromPool(StringBuilderPool) ?? new StringBuilder(InitialStringBuilderCapacity);
			}
		}
		
		public static void ReturnStringBuilder(StringBuilder stringBuilder)
		{
			if (!IsValidForReturn(stringBuilder)) return;
			
			PrepareStringBuilderForReuse(stringBuilder);
			ReturnStringBuilderToPool(stringBuilder);
		}
		
		private static bool IsValidForReturn(StringBuilder stringBuilder)
		{
			return stringBuilder != null;
		}
		
		private static void PrepareStringBuilderForReuse(StringBuilder stringBuilder)
		{
			stringBuilder.Clear();
		}
		
		private static void ReturnStringBuilderToPool(StringBuilder stringBuilder)
		{
			lock (StringBuilderPool)
			{
				if (CanReturnStringBuilderToPool(stringBuilder))
				{
					StringBuilderPool.Push(stringBuilder);
				}
			}
		}
		
		private static bool CanReturnStringBuilderToPool(StringBuilder stringBuilder)
		{
			return StringBuilderPool.Count < MaxPoolSize && 
				   stringBuilder.Capacity < MaxStringBuilderCapacity;
		}
		
		private static T RentFromPool<T>(Stack<T> pool) where T : class
		{
			lock (pool)
			{
				return pool.Count > 0 ? pool.Pop() : null;
			}
		}
		
		private static void ReturnToPool<T>(Stack<List<T>> pool, List<T> list)
		{
			if (list == null) return;
			
			PrepareListForReuse(list);
			lock (pool)
			{
				if (pool.Count < MaxPoolSize)
				{
					pool.Push(list);
				}
			}
		}
		
		private static void PrepareListForReuse<T>(List<T> list)
		{
			list.Clear();
		}
	}
}