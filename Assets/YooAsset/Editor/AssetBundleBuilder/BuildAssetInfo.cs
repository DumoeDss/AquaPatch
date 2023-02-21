using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace YooAsset.Editor
{
	public class BuildAssetInfo
	{
		private bool _isAddAssetTags = false;
		private readonly HashSet<string> _referenceBundleNames = new HashSet<string>();

		/// <summary>
		/// 收集器类型
		/// </summary>
		public ECollectorType CollectorType { private set; get; }

		/// <summary>
		/// 资源包完整名称
		/// </summary>
		public string BundleName { private set; get; }

		/// <summary>
		/// 可寻址地址
		/// </summary>
		public string Address { private set; get; }

		/// <summary>
		/// 资源路径
		/// </summary>
		public string AssetPath { private set; get; }

		/// <summary>
		/// 是否为原生资源
		/// </summary>
		public bool IsRawAsset { private set; get; }

		/// <summary>
		/// 是否为着色器资源
		/// </summary>
		public bool IsShaderAsset { private set; get; }

		/// <summary>
		/// 是否为动态库资源
		/// </summary>
		public bool IsAssemblyAsset { private set; get; }

		/// <summary>
		/// 资源的分类标签
		/// </summary>
		public readonly List<string> AssetTags = new List<string>();

		/// <summary>
		/// 资源包的分类标签
		/// </summary>
		public readonly List<string> BundleTags = new List<string>();

		/// <summary>
		/// 依赖的所有资源
		/// 注意：包括零依赖资源和冗余资源（资源包名无效）
		/// </summary>
		public List<BuildAssetInfo> AllDependAssetInfos { private set; get; }

		/// <summary>
		/// 包名
		/// </summary>
		public string PackageName { private set; get; }

		public bool IncludeInBuild { private set; get; }

		public BuildAssetInfo(BuildAssetInfo clone)
		{
			PackageName = clone.PackageName;
			if(!string.IsNullOrEmpty(clone.BundleName))
				BundleName = $"{PackageName}@{clone.BundleName}";
			CollectorType = clone.CollectorType;
			Address = clone.Address;
			IncludeInBuild = clone.IncludeInBuild;
			AssetPath = clone.AssetPath;
			IsRawAsset = clone.IsRawAsset;
			IsShaderAsset = clone.IsShaderAsset;
			if (clone.AllDependAssetInfos != null)
				AllDependAssetInfos = new List<BuildAssetInfo>(clone.AllDependAssetInfos);
			AssetTags = new List<string>(clone.AssetTags);
			BundleTags = new List<string>(clone.BundleTags);
			_referenceBundleNames = new HashSet<string>(clone._referenceBundleNames);
		}


		public BuildAssetInfo(ECollectorType collectorType, string packageName, bool includeInBuild, string bundleName, string address, string assetPath, bool isRawAsset, bool isAssemblyAsset)
		{
			CollectorType = collectorType;
			BundleName = bundleName;
			Address = address;
			PackageName = packageName;
			IncludeInBuild = includeInBuild;
			AssetPath = assetPath;
			IsRawAsset = isRawAsset;
			IsAssemblyAsset = isAssemblyAsset;

			System.Type assetType = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(assetPath);
			if (assetType == typeof(UnityEngine.Shader) || assetType == typeof(UnityEngine.ShaderVariantCollection))
				IsShaderAsset = true;
			else
				IsShaderAsset = false;
		}
		public BuildAssetInfo(string assetPath, string packageName, bool includeInBuild)
		{
			CollectorType = ECollectorType.None;
			Address = string.Empty;
			PackageName = packageName;
			IncludeInBuild = includeInBuild;
			AssetPath = assetPath;
			IsRawAsset = false;
			IsAssemblyAsset = false;

			System.Type assetType = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(assetPath);
			if (assetType == typeof(UnityEngine.Shader) || assetType == typeof(UnityEngine.ShaderVariantCollection))
				IsShaderAsset = true;
			else
				IsShaderAsset = false;
		}


		/// <summary>
		/// 设置所有依赖的资源
		/// </summary>
		public void SetAllDependAssetInfos(List<BuildAssetInfo> dependAssetInfos)
		{
			if (AllDependAssetInfos != null)
				throw new System.Exception("Should never get here !");

			AllDependAssetInfos = dependAssetInfos;
		}

		/// <summary>
		/// 添加资源的分类标签
		/// 说明：原始定义的资源分类标签
		/// </summary>
		public void AddAssetTags(List<string> tags)
		{
			if (_isAddAssetTags)
				throw new Exception("Should never get here !");
			_isAddAssetTags = true;

			foreach (var tag in tags)
			{
				if (AssetTags.Contains(tag) == false)
				{
					AssetTags.Add(tag);
				}
			}
		}

		/// <summary>
		/// 添加资源包的分类标签
		/// 说明：传染算法统计到的分类标签
		/// </summary>
		public void AddBundleTags(List<string> tags)
		{
			foreach (var tag in tags)
			{
				if (BundleTags.Contains(tag) == false)
				{
					BundleTags.Add(tag);
				}
			}
		}

		/// <summary>
		/// 资源包名是否存在
		/// </summary>
		public bool HasBundleName()
		{
			if (string.IsNullOrEmpty(BundleName))
				return false;
			else
				return true;
		}

		/// <summary>
		/// 添加关联的资源包名称
		/// </summary>
		public void AddReferenceBundleName(string bundleName)
		{
			if (string.IsNullOrEmpty(bundleName))
				throw new Exception("Should never get here !");

			if (_referenceBundleNames.Contains(bundleName) == false)
				_referenceBundleNames.Add(bundleName);
		}

		/// <summary>
		/// 计算共享资源包的完整包名
		/// </summary>
		public void CalculateShareBundleName(bool uniqueBundleName, string packageName)
		{
			if (CollectorType != ECollectorType.None)
				return;				

			if (IsRawAsset)
				throw new Exception("Should never get here !");

			//if (IsShaderAsset)
			//{
			//	BundleName = shadersBundleName;
			//}
			//else
			{
				if (_referenceBundleNames.Count > 0)
				{
					IPackRule packRule = PackDirectory.StaticPackRule;
					PackRuleResult packRuleResult = packRule.GetPackRuleResult(new PackRuleData(AssetPath));
					string prefix = "share";
					if (_referenceBundleNames.Count == 1)
					{
						prefix = "auto_dependencies";
					}

					BundleName = packRuleResult.GetShareBundleName(packageName, prefix, uniqueBundleName);

					var package = AssetBundleCollectorSettingData.Setting.Packages.Find(_ => _.PackageName == PackageName);


					if (package != null)
					{
						var group = package.Groups.Find(_ => _.GroupName == BundleName);
						if (group == null)
						{
							group = new AssetBundleCollectorGroup()
							{
								GroupName = BundleName,
								GroupDesc = "自动依赖",
							};

							package.Groups.Add(group);

						}
						group.Collectors.Add(new AssetBundleCollector()
						{
							CollectPath = AssetPath,
							CollectorType = ECollectorType.DependAssetCollector,
							PackRuleName = "PackGroup",
						});
					}
				}
				else
				{
					// 注意：被引用次数小于1的资源不需要设置资源包名称
					BundleName = string.Empty;
				}
			}

		}
	}
}