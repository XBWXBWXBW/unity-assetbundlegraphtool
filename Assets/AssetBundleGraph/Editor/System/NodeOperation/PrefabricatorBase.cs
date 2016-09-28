using UnityEngine;
using UnityEditor;

using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;

namespace AssetBundleGraph {
	public class PrefabricatorBase : INodeOperationBase {
		public void Setup (BuildTarget target, 
			NodeData node, 
			ConnectionData connection, 
			Dictionary<string, List<Asset>> groupedSources, 
			List<string> alreadyCached, 
			Action<NodeData, ConnectionData, Dictionary<string, List<Asset>>, List<string>> Output) 
		{
			var invalids = new List<string>();
			foreach (var sources in groupedSources.Values) {
				foreach (var source in sources) {
					if (string.IsNullOrEmpty(source.importFrom)) {
						invalids.Add(source.absoluteAssetPath);
					}
				}
			}

			if (invalids.Any()) {
				throw new NodeException(string.Join(", ", invalids.ToArray()) + " are not imported yet. These assets need to be imported before prefabricated.", node.Id);
			}

			var prefabOutputDir = FileUtility.EnsurePrefabricatorCacheDirExists(target, node);				
			var outputDict = new Dictionary<string, List<Asset>>();
			
			foreach (var groupKey in groupedSources.Keys) {
				var inputSources = groupedSources[groupKey];

				/*
					ready input resource info for execute. not contains cache in this node.
				*/
				var assets = new List<DepreacatedAssetInfo>();
				foreach (var assetData in inputSources) {
					var assetName = assetData.fileNameAndExtension;
					var assetType = assetData.assetType;
					var assetPath = assetData.importFrom;
					var assetDatabaseId = assetData.assetDatabaseId;
					assets.Add(new DepreacatedAssetInfo(assetName, assetType, assetPath, assetDatabaseId));
				}

				// collect generated prefab path.
				var generated = new List<string>();
				
				/*
					Prefabricate(string prefabName) method.
				*/
				Func<string, string> Prefabricate = (string prefabName) => {
					var newPrefabOutputPath = Path.Combine(prefabOutputDir, prefabName);
					generated.Add(newPrefabOutputPath);
					isPrefabricateFunctionCalled = true;

					return newPrefabOutputPath;
				};

				ValidateCanCreatePrefab(target, node, groupKey, assets, prefabOutputDir, Prefabricate);

				if (!isPrefabricateFunctionCalled) {
					Debug.LogWarning(node.Name +": Prefabricate delegate was not called. Prefab might not be created properly.");
				}

				foreach (var generatedPrefabPath in generated) {
					var newAsset = Asset.CreateNewAssetWithImportPathAndStatus(
						generatedPrefabPath,
						true,// absolutely new in setup.
						false
					);

					if (!outputDict.ContainsKey(groupKey)) outputDict[groupKey] = new List<Asset>();
					outputDict[groupKey].Add(newAsset);
				}
				outputDict[groupKey].AddRange(inputSources);
			
			} 				

			Output(node, connection, outputDict, new List<string>());

		}

		public void Run (BuildTarget target, 
			NodeData node, 
			ConnectionData connection, 
			Dictionary<string, List<Asset>> groupedSources, 
			List<string> alreadyCached, 
			Action<NodeData, ConnectionData, Dictionary<string, List<Asset>>, List<string>> Output) 
		{
			var usedCache = new List<string>();
			
			var invalids = new List<string>();
			foreach (var sources in groupedSources.Values) {
				foreach (var source in sources) {
					if (string.IsNullOrEmpty(source.importFrom)) {
						invalids.Add(source.absoluteAssetPath);
					}
				}
			}
			
			if (invalids.Any()) {
				throw new NodeException(string.Join(", ", invalids.ToArray()) + " are not imported yet. These assets need to be imported before prefabricated.", node.Id);
			}

			var prefabOutputDir = FileUtility.EnsurePrefabricatorCacheDirExists(target, node);
			var outputDict = new Dictionary<string, List<Asset>>();
			var cachedOrGenerated = new List<string>();

			foreach (var groupKey in groupedSources.Keys) {
				var inputSources = groupedSources[groupKey];

				/*
					ready input resource info for execute. not contains cache in this node.
				*/
				var assets = new List<DepreacatedAssetInfo>();
				foreach (var assetData in inputSources) {
					var assetName = assetData.fileNameAndExtension;
					var assetType = assetData.assetType;
					var assetPath = assetData.importFrom;
					var assetDatabaseId = assetData.assetDatabaseId;
					assets.Add(new DepreacatedAssetInfo(assetName, assetType, assetPath, assetDatabaseId));
				}

				// collect generated prefab path.
				var generated = new List<string>();
				var outputSources = new List<Asset>();
				
				
				/*
					Prefabricate(GameObject baseObject, string prefabName, bool forceGenerate) method.
				*/
				Func<GameObject, string, bool, string> Prefabricate = (GameObject baseObject, string prefabName, bool forceGenerate) => {
					var newPrefabOutputPath = Path.Combine(prefabOutputDir, prefabName);
					
					if (forceGenerate || !SystemDataUtility.IsAllAssetsCachedAndUpdated(inputSources, alreadyCached, newPrefabOutputPath)) {
						// not cached, create new.
						UnityEngine.Object prefabFile = PrefabUtility.CreateEmptyPrefab(newPrefabOutputPath);
					
						// export prefab data.
						PrefabUtility.ReplacePrefab(baseObject, prefabFile);

						// save prefab.
						AssetDatabase.Refresh(ImportAssetOptions.ImportRecursive);
						AssetDatabase.SaveAssets();
						generated.Add(newPrefabOutputPath);
						cachedOrGenerated.Add(newPrefabOutputPath);
						Debug.Log(node.Name + " created new prefab: " + newPrefabOutputPath );
					} else {
						// cached.
						usedCache.Add(newPrefabOutputPath);
						cachedOrGenerated.Add(newPrefabOutputPath);
						Debug.Log(node.Name + " used cached prefab: " + newPrefabOutputPath );
					}

					isPrefabricateFunctionCalled = true;

					return newPrefabOutputPath;
				};

				/*
					execute inheritee's input method.
				*/
				try {
					CreatePrefab(target, node, groupKey, assets, prefabOutputDir, Prefabricate);
				} catch (Exception e) {
					Debug.LogError(node.Name + " Error:" + e);
					throw new NodeException(node.Name + " Error:" + e, node.Id);
				}

				if (!isPrefabricateFunctionCalled) {
					Debug.LogWarning(node.Name +": Prefabricate delegate was not called. Prefab might not be created properly.");
				}

				/*
					ready assets-output-data from this node to next output.
					it contains "cached" or "generated as prefab" or "else" assets.
					output all assets.
				*/
				var currentAssetsInThisNode = FileUtility.FilePathsInFolder(prefabOutputDir);
				foreach (var generatedCandidateAssetPath in currentAssetsInThisNode) {
					
					/*
						candidate is new, regenerated prefab.
					*/
					if (generated.Contains(generatedCandidateAssetPath)) {
						var newAsset = Asset.CreateNewAssetWithImportPathAndStatus(
							generatedCandidateAssetPath,
							true,
							false
						);
						outputSources.Add(newAsset);
						continue;
					}
					
					/*
						candidate is not new prefab.
					*/
					var cachedPrefabAsset = Asset.CreateNewAssetWithImportPathAndStatus(
						generatedCandidateAssetPath,
						false,
						false
					);
					outputSources.Add(cachedPrefabAsset);
				}


				/*
					add current resources to next node's resources.
				*/
				outputSources.AddRange(inputSources);

				outputDict[groupKey] = outputSources;
			}

			// delete unused cached prefabs.
			var unusedCachePaths = alreadyCached.Except(cachedOrGenerated).Where(path => !FileUtility.IsMetaFile(path)).ToList();
			foreach (var unusedCachePath in unusedCachePaths) {
				// unbundlize unused prefabricated cached asset.
				var assetImporter = AssetImporter.GetAtPath(unusedCachePath);
  				assetImporter.assetBundleName = string.Empty;

				FileUtility.DeleteFileThenDeleteFolderIfEmpty(unusedCachePath);
			}


			Output(node, connection, outputDict, usedCache);
		}

		private bool isPrefabricateFunctionCalled = false;

		public virtual void ValidateCanCreatePrefab (BuildTarget target, NodeData node, string groupKey, List<DepreacatedAssetInfo> sources, string prefabOutputDir, Func<string, string> Prefabricate) {
			Debug.LogError(node.Name + ":Subclass did not implement \"ValidateCanCreatePrefab ()\" method:" + this);
			throw new NodeException(node.Name + ":Subclass did not implement \"ValidateCanCreatePrefab ()\" method:" + this, node.Id);
		}

		public virtual void CreatePrefab (BuildTarget target, NodeData node, string groupKey, List<DepreacatedAssetInfo> sources, string prefabOutputDir, Func<GameObject, string, bool, string> Prefabricate) {
			Debug.LogError(node.Name + ":Subclass did not implement \"CreatePrefab ()\" method:" + this);
			throw new NodeException(node.Name + ":Subclass did not implement \"ValidateCanCreatePrefab ()\" method:" + this, node.Id);
		}


		public static void ValidatePrefabScriptClassName (string prefabScriptClassName, Action NullOrEmpty, Action PrefabTypeIsNull) {
			if (string.IsNullOrEmpty(prefabScriptClassName)) NullOrEmpty();
			var loadedType = System.Reflection.Assembly.GetExecutingAssembly().CreateInstance(prefabScriptClassName);
			if (loadedType == null) PrefabTypeIsNull();
		}
	}
}