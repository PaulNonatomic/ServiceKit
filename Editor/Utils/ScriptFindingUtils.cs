using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nonatomic.ServiceKit.Editor.Utils
{
	public static class ScriptFindingUtils
	{
		public static T CreateInstanceInProject<T>(bool selectInstance = true) where T : ScriptableObject
		{
			var asset = ScriptableObject.CreateInstance<T>();
			var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            
			if (string.IsNullOrEmpty(path))
			{
				path = "Assets";
			}
			else if (Path.GetExtension(path) != "")
			{
				path = path.Replace(Path.GetFileName(AssetDatabase.GetAssetPath(Selection.activeObject)), "");
			}

			var assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(path + "/New " + typeof(T).Name + ".asset");

			AssetDatabase.CreateAsset(asset, assetPathAndName);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			EditorUtility.FocusProjectWindow();
			if(selectInstance) Selection.activeObject = asset;

			return asset;
		}
		
		public static MonoScript FindScriptForType(Type type)
		{
			var script = FindExactTypeScript(type);
			if (script != null) return script;
			
			// Second attempt: If it's an interface, try more aggressive search
			if (type.IsInterface)
			{
				script = FindInterfaceScript(type);
				if (script != null) return script;
			}
			
			// Third attempt: Use the first script with a matching name
			var typeName = type.Name;
			var guids = AssetDatabase.FindAssets($"t:MonoScript {typeName}");

			if (guids.Length <= 0) return null;
			
			var path = AssetDatabase.GUIDToAssetPath(guids[0]);
			return AssetDatabase.LoadAssetAtPath<MonoScript>(path);

		}
		
		public static List<MonoScript> FindPotentialScriptsForType(Type type)
		{
			var results = new List<MonoScript>();
			var searchTerm = type.IsInterface ? type.Name.Replace("I", "") : type.Name;
			var guids = AssetDatabase.FindAssets($"{searchTerm} t:MonoScript");
			
			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
				if (script != null) results.Add(script);
			}
			
			return results;
		}
		
		private static MonoScript FindExactTypeScript(Type type)
		{
			var typeName = type.Name;
			var guids = AssetDatabase.FindAssets($"t:MonoScript {typeName}");
			
			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

				if (script == null || script.GetClass() != type) continue;
				return script;
			}
			
			return null;
		}
		
		private static MonoScript FindInterfaceScript(Type interfaceType)
		{
			// Try to find by checking script content
			var allScriptGuids = AssetDatabase.FindAssets("t:MonoScript");
			var interfaceName = interfaceType.Name;
			var fullName = interfaceType.FullName;
			
			foreach (var guid in allScriptGuids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				
				// Skip obvious non-matches
				if (!path.EndsWith(".cs")) continue;
				
				var content = File.ReadAllText(path);
				
				// Look for interface declaration pattern
				if (content.Contains($"interface {interfaceName}") || 
					(fullName != null && content.Contains(fullName)))
				{
					return AssetDatabase.LoadAssetAtPath<MonoScript>(path);
				}
			}
			
			return null;
		}
	}
}