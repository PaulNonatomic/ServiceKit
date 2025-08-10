using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;

namespace Nonatomic.ServiceKit.Samples
{
	internal static class ConfigureAnalyzerImporter
	{
		private const string _dllName = "ServiceKit.Analyzers.dll";

		[DidReloadScripts]
		private static void Configure()
		{
			var dllGuid = AssetDatabase.FindAssets($"{Path.GetFileNameWithoutExtension(_dllName)} t:DefaultAsset")
				.FirstOrDefault();

			if (string.IsNullOrEmpty(dllGuid))
				return;

			var path = AssetDatabase.GUIDToAssetPath(dllGuid);
			if (!path.EndsWith(_dllName))
				return;

			var importer = AssetImporter.GetAtPath(path) as PluginImporter;
			if (importer == null)
				return;

			// Editor only
			importer.SetCompatibleWithAnyPlatform(false);
			importer.SetCompatibleWithEditor(true);

			#if UNITY_2022_3_OR_NEWER
			// Silence Roslyn dependency validation
			importer.SetValidateReferences(false);
			#endif
			importer.SaveAndReimport();
		}
	}
}