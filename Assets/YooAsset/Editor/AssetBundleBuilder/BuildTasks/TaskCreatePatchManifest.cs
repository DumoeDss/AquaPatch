using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using HybridCLR.Editor;
using AquaSys.Tools;
using AquaSys.Patch.Encryption;
using System.Text;

namespace YooAsset.Editor
{
	public class PatchManifestContext : IContextObject
	{
		internal Dictionary<string,PatchManifest> PatchManifests { get; set; }
	}

	[TaskAttribute("创建补丁清单文件")]
	public class TaskCreatePatchManifest : IBuildTask
	{
		void IBuildTask.Run(BuildContext context)
		{
			CreatePatchManifestFile(context);
		}

		/// <summary>
		/// 创建补丁清单文件到输出目录
		/// </summary>
		private void CreatePatchManifestFile(BuildContext context)
		{
			var buildMapContext = context.GetContextObject<BuildMapContext>();
			var buildParametersContext = context.GetContextObject<BuildParametersContext>();
			var buildParameters = buildParametersContext.Parameters;
			string packageOutputDirectory = buildParametersContext.GetPackageOutputDirectory();
			FileUtility.CreateDirectory(packageOutputDirectory);

			var patchBundleDic = GetAllPatchBundle(context);
			//获取所有package的assembly列表
			Dictionary<string, List<string>> assemblyBundlesDict = new Dictionary<string, List<string>>();
			//assembly对应的address xxx.dll -> assembly/xxx.dll.bytes
			Dictionary<string, string> assemblyBundleLocation = new Dictionary<string, string>();
			foreach (var patchBundlePair in patchBundleDic)
			{
				var assemblyBundles = patchBundlePair.Value.Select(_ => _).Where(_ => _.IsAssemblyAsset).ToList();
				var assemblyNameList = new List<string>();

				foreach (var item in assemblyBundles)
				{
					var assemblyAddresses = item.AssemblyAddresses.Split(';');
					for (int i = 0; i < assemblyAddresses.Length; i++)
					{
						if (!string.IsNullOrEmpty(assemblyAddresses[i]))
						{
							string assembly = assemblyAddresses[i].Replace(".bytes", "");
							if (assembly.Contains("/"))
							{
								var names = assembly.Split('/');
								assembly = names[names.Length - 1];
							}
							assemblyNameList.Add(assembly);
							assemblyBundleLocation[assembly] = assemblyAddresses[i];
						}
					}
				}
				if (!assemblyBundlesDict.ContainsKey(patchBundlePair.Key))
					assemblyBundlesDict[patchBundlePair.Key] = new List<string>();
				assemblyBundlesDict[patchBundlePair.Key].AddRange(assemblyNameList);
			}
			var assembliesContext = context.GetContextObject<AssembliesContext>();
			foreach (var patchBundlePair in patchBundleDic)
            {
				var packageName = patchBundlePair.Key;
				// 创建新补丁清单
				var patchManifest = new PatchManifest();
				patchManifest.FileVersion = YooAssetSettings.PatchManifestFileVersion;
				patchManifest.EnableAddressable = buildMapContext.EnableAddressable;
				patchManifest.OutputNameStyle = (int)buildParameters.OutputNameStyle;
				patchManifest.PackageName = packageName;
				patchManifest.PackageVersion = buildParameters.PackageVersion;
				patchManifest.BundleList = patchBundlePair.Value;
				var dependAssemblyAddressList = new List<string>();
				FileUtility.CreateDirectory($"{packageOutputDirectory}/{packageName}");

				var assemblyBundle = patchManifest.BundleList.Select(_ => _).Where(_ => _.IsAssemblyAsset);
                if (assemblyBundle != null)
                {
                    Dictionary<string, int> weights = new Dictionary<string, int>();
                    Dictionary<string, List<string>> dependencies = new Dictionary<string, List<string>>();

                    //对当前package的assembly列表进行处理
                    for (int i = 0; i < assemblyBundlesDict[patchManifest.PackageName].Count; i++)
                    {
                        var currentAssemblyName = assemblyBundlesDict[patchManifest.PackageName][i];
						//加载Assembly,查看依赖
						var packageFolder = patchManifest.PackageName == SettingsUtil.HybridCLRSettings.defaultPackageName ? "HotUpdate" : patchManifest.PackageName;
						var filePath = $"{assembliesContext.hotUpdateRootDir}/{packageFolder}/{assembliesContext.platform}/{currentAssemblyName}.bytes";
						var dllBytes = AESEncrypt.DecryptFile(filePath, Convert.ToBase64String(Encoding.UTF8.GetBytes(SettingsUtil.HybridCLRSettings.hotUpdateDllPassword)));
                        try
                        {
							var tempAssembly = Assembly.Load(dllBytes);
							var assemblyNames = tempAssembly.GetReferencedAssemblies();

							dependencies[currentAssemblyName] = new List<string>();
							weights[currentAssemblyName] = 0;

							//分析当前Assembly的依赖
							foreach (var assemblyName in assemblyNames)
							{
								var name = assemblyName.Name ;
								//查找是否有跨包依赖
								foreach (var item in assemblyBundlesDict)
								{
									if (item.Value.Contains(name))
									{
										if (item.Key == patchManifest.PackageName)
										{
											//对同包的列表进行权重计算
											if (!dependencies.ContainsKey(currentAssemblyName))
											{
												dependencies[currentAssemblyName] = new List<string>();
											}
											dependencies[currentAssemblyName].Add(name);


										}
										else
										{
											//跨包依赖
											dependAssemblyAddressList.Add(
												$"{item.Key}@{assemblyBundleLocation[name]}"
												);
										}
									}
								}
							}
						}catch (Exception ex)
                        {
							Debug.LogError($"Assembly.Load Error! {filePath} {ex.ToString()}");
                        }
                       
                        patchManifest.DependAssemblyAddresses = dependAssemblyAddressList.ToArray();
                    }

                    foreach (var item in dependencies)
                    {
                        weights[item.Key] = CalcWeight(item.Key, dependencies);
                    }
                    List<string> assemblyAddresses = new List<string>(assemblyBundlesDict[patchManifest.PackageName]);
                    assemblyAddresses.Sort((_1, _2) => { return weights[_1].CompareTo(weights[_2]); });
                    var assemblyAddresseList = new List<string>();
                    foreach (var item in assemblyAddresses)
                    {
                        assemblyAddresseList.Add(assemblyBundleLocation[item]);
                    }
                    patchManifest.AssemblyAddresses = assemblyAddresseList.ToArray();
                }

                List<string> bundleNameList = new List<string>();
				List<string> dependBundleNameList = new List<string>();
				patchManifest.AssetList = GetAllPatchAsset(context, patchManifest.PackageName, bundleNameList, dependBundleNameList);
				patchManifest.BundleNameList = bundleNameList.ToArray();
				patchManifest.DependBundleNameList = dependBundleNameList.ToArray();

				//// 更新Unity内置资源包的引用关系
				//string shadersBunldeName = YooAssetSettingsData.GetUnityShadersBundleFullName(buildMapContext.UniqueBundleName, packageName);
				//if (buildParameters.BuildPipeline == EBuildPipeline.ScriptableBuildPipeline)
				//{
				//	if (buildParameters.BuildMode == EBuildMode.IncrementalBuild)
				//	{
				//		var buildResultContext = context.GetContextObject<TaskBuilding_SBP.BuildResultContext>();
				//		UpdateBuiltInBundleReference(patchManifest, buildResultContext.Results, shadersBunldeName);
				//	}
				//}

				// 创建补丁清单二进制文件
				{
					string fileName = YooAssetSettingsData.GetManifestBinaryFileName(packageName,"tmp");
					string filePath = $"{packageOutputDirectory}/{fileName}";
					PatchManifestTools.SerializeToBinary(filePath, patchManifest);

					var crc = GetFileCRC(filePath);
					var newFileName = YooAssetSettingsData.GetManifestBinaryFileName(packageName, crc);
					string newFilePath = $"{packageOutputDirectory}/{packageName}/{newFileName}";
					BuildRunner.Log($"创建补丁清单二进制文件：{newFilePath}");

					if (File.Exists(newFilePath))
						File.Delete(newFilePath);
					File.Move(filePath, newFilePath);

					// 创建补丁清单文本文件
					string manifestJsonFileName = YooAssetSettingsData.GetManifestJsonFileNameWitchCrc(packageName, crc);
					string manifestJsonFilePath = $"{packageOutputDirectory}/{packageName}/{manifestJsonFileName}";
					PatchManifestTools.SerializeToJson(manifestJsonFilePath, patchManifest);
					BuildRunner.Log($"创建补丁清单Json文件：{manifestJsonFilePath}");
					PatchManifestContext patchManifestContext;
					try
                    {
						patchManifestContext = context.GetContextObject<PatchManifestContext>();
                    }
                    catch
                    {
						patchManifestContext = new PatchManifestContext();
						context.SetContextObject(patchManifestContext);
					}
					
					byte[] bytesData = FileUtility.ReadAllBytes(newFilePath);
                    if (patchManifestContext.PatchManifests == null)
                    {
						patchManifestContext.PatchManifests = new Dictionary<string, PatchManifest>();
					}
					patchManifestContext.PatchManifests[newFileName.Replace(".bytes", "")] = PatchManifestTools.DeserializeFromBinary(bytesData);


					// 创建补丁清单版本文件
					YooAssetVersion yooAssetVersion = new YooAssetVersion()
					{
						crc = crc,
						version = 1,
						size = GetFileSize(newFilePath)
					};

					var json = JsonUtility.ToJson(yooAssetVersion);
					string manifestVersionFilePath = YooAssetSettingsData.GetPackageVersionFileName(packageName, crc);
					FileUtility.CreateFile($"{packageOutputDirectory}/{packageName}/{manifestVersionFilePath}", json);
					BuildRunner.Log($"创建补丁清单版本文件：{manifestVersionFilePath}");
				}
			}
		}

		int CalcWeight(string target, Dictionary<string, List<string>> dependices)
		{
			if (dependices[target] == null || dependices[target].Count == 0)
			{
				return 0;
			}
			else if (dependices[target].Count == 1)
			{
				return CalcWeight(dependices[target][0], dependices) + 1;
			}
			else
			{
				int sum = 1;
				foreach (var depend in dependices[target])
				{
					sum += CalcWeight(depend, dependices);
				}
				return sum;
			}
		}

		/// <summary>
		/// 获取资源包列表
		/// </summary>
		private Dictionary<string, List<PatchBundle>> GetAllPatchBundle(BuildContext context)
		{
			Dictionary<string, List<PatchBundle>> result = new Dictionary<string, List<PatchBundle>>(1000);

			var buildMapContext = context.GetContextObject<BuildMapContext>();
			var buildParametersContext = context.GetContextObject<BuildParametersContext>();


			foreach (var bundleInfo in buildMapContext.BundleInfos)
			{
				var patchBundle = bundleInfo.CreatePatchBundle();

				if (result.ContainsKey(bundleInfo.PackageName) && result[bundleInfo.PackageName] != null)
				{
					result[bundleInfo.PackageName].Add(patchBundle);
				}
				else
				{
					List<PatchBundle> bundles = new List<PatchBundle>(1000);
					bundles.Add(patchBundle);
					result[bundleInfo.PackageName] = bundles;
				}
			}

			return result;
		}

		/// <summary>
		/// 获取资源包列表
		/// </summary>
		private Dictionary<string, List<PatchBundle>> GetAllPatchBundles(BuildContext context)
		{
			Dictionary<string, List<PatchBundle>> result = new Dictionary<string, List<PatchBundle>>(1000);

			var buildMapContext = context.GetContextObject<BuildMapContext>();

			foreach (var bundleInfo in buildMapContext.BundleInfos)
			{
                if (!bundleInfo.IncludeInBuild)
                {
					continue;
                }

				var patchBundle = bundleInfo.CreatePatchBundle();

				if (result.ContainsKey(bundleInfo.PackageName)&& result[bundleInfo.PackageName]!=null)
                {
					result[bundleInfo.PackageName].Add(patchBundle);
				}
                else
                {
					List<PatchBundle> bundles = new List<PatchBundle>(1000);
					bundles.Add(patchBundle);
					result[bundleInfo.PackageName] = bundles;
				}
			}

			return result;
		}

		private string GetFileCRC(string filePath, bool standardBuild = true)
		{
			if (standardBuild)
				return Crc32Helper.CalcHash(filePath);
			else
				return "00000000"; //8位
		}
		private long GetFileSize(string filePath, bool standardBuild = true)
		{
			if (standardBuild)
				return FileUtility.GetFileSize(filePath);
			else
				return 0;
		}

		/// <summary>
		/// 获取资源列表
		/// </summary>
		private List<PatchAsset> GetAllPatchAsset(BuildContext context, string packageName,  List<string> bundleNameList,  List<string> dependBundleNameList)
		{
			var buildMapContext = context.GetContextObject<BuildMapContext>();

			List<PatchAsset> result = new List<PatchAsset>(1000);

			foreach (var bundleInfo in buildMapContext.BundleInfos)
			{
				if (bundleInfo.PackageName == packageName)
                {
					var assetInfos = bundleInfo.GetAllPatchAssetInfos();
					foreach (var assetInfo in assetInfos)
					{
						PatchAsset patchAsset = new PatchAsset();
						if (buildMapContext.EnableAddressable)
							patchAsset.Address = assetInfo.Address;
						else
							patchAsset.Address = string.Empty;
						patchAsset.AssetPath = assetInfo.AssetPath;
						patchAsset.AssetTags = assetInfo.AssetTags.ToArray();
						var bundleName = assetInfo.BundleName;
						if (bundleNameList.Contains(bundleName))
                        {
							patchAsset.BundleID = bundleNameList.IndexOf(bundleName);
						}
						else
                        {
							bundleNameList.Add(assetInfo.BundleName);
							patchAsset.BundleID = bundleNameList.Count - 1;
						}
						patchAsset.DependIDs = GetAssetBundleDependIDs(bundleName, assetInfo, packageName,dependBundleNameList);
						result.Add(patchAsset);
					}
                }					
			}
			return result;
		}
		private int[] GetAssetBundleDependIDs(string mainBundleName, BuildAssetInfo assetInfo,string packageName,  List<string> dependBundleNameList)
		{
			List<int> result = new List<int>();
			if(dependBundleNameList==null)
				dependBundleNameList = new List<string>();
			if (assetInfo.AllDependAssetInfos != null)
            {
				foreach (var dependAssetInfo in assetInfo.AllDependAssetInfos)
				{
					if (dependAssetInfo.HasBundleName())
					{
						var bundleName = dependAssetInfo.BundleName;
                        if (dependAssetInfo.PackageName != packageName)
                        {
							bundleName = $"{bundleName}";//{dependAssetInfo.PackageName}@

						}
                        if (dependBundleNameList.Contains(bundleName))
                        {
							var bundleID = dependBundleNameList.IndexOf(bundleName);
							if (mainBundleName != bundleName && !result.Contains(bundleID))
                            {
								result.Add(bundleID);
							}
						}
                        else
                        {
							dependBundleNameList.Add(bundleName);
							result.Add(dependBundleNameList.Count-1);
						}
					}
				}
			}
			return result.ToArray();
		}
		private int GetAssetBundleID(string bundleName, PatchManifest patchManifest)
		{
			for (int index = 0; index < patchManifest.BundleList.Count; index++)
			{
				if (patchManifest.BundleList[index].BundleName == bundleName)
					return index;
			}
			throw new Exception($"Not found bundle name : {bundleName}");
		}


		/// <summary>
		/// 更新Unity内置资源包的引用关系
		/// </summary>
		private void UpdateBuiltInBundleReference(PatchManifest patchManifest, IBundleBuildResults buildResults, string shadersBunldeName)
		{
			// 获取所有依赖着色器资源包的资源包列表
			List<string> shaderBundleReferenceList = new List<string>();
			foreach (var valuePair in buildResults.BundleInfos)
			{
				if (valuePair.Value.Dependencies.Any(t => t == shadersBunldeName))
					shaderBundleReferenceList.Add(valuePair.Key);
			}

			// 注意：没有任何资源依赖着色器
			if (shaderBundleReferenceList.Count == 0)
				return;

			// 获取着色器资源包索引
			Predicate<PatchBundle> predicate = new Predicate<PatchBundle>(s => s.BundleName == shadersBunldeName);
			var shaderBundle = patchManifest.BundleList.Find(s => s.BundleName == shadersBunldeName);
			if(shaderBundle == null)
				throw new Exception("没有发现着色器资源包！");

			// 检测依赖交集并更新依赖ID
			foreach (var patchAsset in patchManifest.AssetList)
			{
				List<string> dependBundles = GetPatchAssetAllDependBundles(patchManifest, patchAsset);
				List<string> conflictAssetPathList = dependBundles.Intersect(shaderBundleReferenceList).ToList();
				if (conflictAssetPathList.Count > 0)
				{
					List<string> newDependNames = new List<string>();
					for (int i = 0; i < conflictAssetPathList.Count; i++)
                    {
						if (!newDependNames.Contains(conflictAssetPathList[i]) )
							newDependNames.Add(conflictAssetPathList[i]);
                    }
                    
					if (newDependNames.Contains(shaderBundle.BundleName) == false)
						newDependNames.Add(shaderBundle.BundleName);
					var newDependIDs = new List<int>();
					var dependNameList = new List<string>(patchManifest.DependBundleNameList);
					for (int i = 0; i < newDependNames.Count; i++)
                    {
						var id = dependNameList.IndexOf(newDependNames[i]);
						if (!newDependIDs.Contains(id))
                        {
							newDependIDs.Add(id);
						}
					}
					patchAsset.DependIDs = newDependIDs.ToArray();
				}
			}
		}
		private List<string> GetPatchAssetAllDependBundles(PatchManifest patchManifest, PatchAsset patchAsset)
		{
			List<string> result = new List<string>();
			string mainBundle = patchManifest.BundleNameList[patchAsset.BundleID];
			result.Add(mainBundle);
			foreach (var dependID in patchAsset.DependIDs)
			{
				result.Add(patchManifest.DependBundleNameList[dependID]);
			}
			return result;
		}
	}
}