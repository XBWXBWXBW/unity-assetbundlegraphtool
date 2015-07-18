using UnityEngine;
using System;
using System.IO;

namespace AssetGraph {
	public class InternalAssetData {
		public readonly string traceId;
		public readonly string absoluteSourcePath;
		public readonly string sourceBasePath;
		public readonly string fileNameAndExtension;
		public readonly string pathUnderSourceBase;
		public readonly string importedPath;
		public readonly string pathUnderConnectionId;
		public readonly string assetId;
		public readonly Type assetType;
		
		/**
			pre-assets which is generated by Loaded.
		*/
		public static InternalAssetData InternalAssetDataByLoader (string absoluteSourcePath, string sourceBasePath) {
			return new InternalAssetData(
				Guid.NewGuid().ToString(),
				absoluteSourcePath,
				sourceBasePath,
				Path.GetFileName(absoluteSourcePath),
				GetPathWithoutBasePath(absoluteSourcePath, sourceBasePath),
				null,
				null,
				null,
				null
			);
		}

		/**
			new assets which is Imported.
		*/
		public static InternalAssetData InternalAssetDataByImporter (string traceId, string absoluteSourcePath, string sourceBasePath, string fileNameAndExtension, string pathUnderSourceBase, string importedPath, string assetId, Type assetType) {
			return new InternalAssetData(
				traceId,
				absoluteSourcePath,
				sourceBasePath,
				fileNameAndExtension,
				pathUnderSourceBase,
				importedPath,
				The2LevelLowerPath(importedPath),
				assetId,
				assetType
			);
		}

		/**
			new assets which is generated on Imported or Prefabricated or Bundlized.
		*/
		public static InternalAssetData InternalAssetDataGeneratedByImporterOrPrefabricatorOrBundlizer (string importedPath, string assetId, Type assetType) {
			return new InternalAssetData(
				Guid.NewGuid().ToString(),
				null,
				null,
				Path.GetFileName(importedPath),
				null,
				importedPath,
				The2LevelLowerPath(importedPath),
				assetId,
				assetType
			);
		}

		private InternalAssetData (
			string traceId,
			string absoluteSourcePath,
			string sourceBasePath,
			string fileNameAndExtension,
			string pathUnderSourceBase,
			string importedPath,
			string pathUnderConnectionId,
			string assetId,
			Type assetType
		) {
			this.traceId = traceId;
			this.absoluteSourcePath = absoluteSourcePath;
			this.sourceBasePath = sourceBasePath;
			this.fileNameAndExtension = fileNameAndExtension;
			this.pathUnderSourceBase = pathUnderSourceBase;
			this.importedPath = importedPath;
			this.pathUnderConnectionId = pathUnderConnectionId;
			this.assetId = assetId;
			this.assetType = assetType;
		}

		private static string The2LevelLowerPath (string assetsTemp_ConnectionId_ResourcePath) {
			var splitted = assetsTemp_ConnectionId_ResourcePath.Split('/');
			var depthCount = AssetGraphSettings.APPLICATIONDATAPATH_TEMP_PATH.Split('/').Length + 1;// last +1 is connectionId's count
			var concatenated = new string[splitted.Length - depthCount];
			Array.Copy(splitted, depthCount, concatenated, 0, concatenated.Length);
			var resultPath = string.Join("/", concatenated);
			return resultPath;
		}

		public static string GetPathWithoutBasePath (string localPathWithBasePath, string basePath) {
			var replaced = localPathWithBasePath.Replace(basePath, string.Empty);
			if (replaced.StartsWith(AssetGraphSettings.UNITY_FOLDER_SEPARATER)) return replaced.Substring(1);
			return replaced;
		}
		
		public static string GetPathWithBasePath (string localPathWithoutBasePath, string basePath) {
			return Path.Combine(basePath, localPathWithoutBasePath);
		}
	}
}