using AquaSys.Patch.Encryption;
using Cysharp.Threading.Tasks;
using HybridCLR.Extension.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UniFramework.Event;
using UniFramework.Module;
using UnityEngine;
using YooAsset;

namespace AquaSys.Patch
{
	public class StartUp : MonoBehaviour
	{
		public EPlayMode playMode = EPlayMode.HostPlayMode;
		public string baseUrl = "http://127.0.0.1:8080/";
		public string gameVersion;
		public string packageName = "DefaultPackage";
		public string packageVersion;
		AssetsPackage defaultPackage;

		private void Start()
		{
			InitApp();
		}

		public void InitApp()
		{

			// 初始化事件系统
			UniEvent.Initalize();

			// 初始化管理系统
			UniModule.Initialize();

			// 初始化资源系统
			YooAssets.Initialize();
			YooAssets.SetOperationSystemMaxTimeSlice(30);

			UniModule.StartCoroutine(InitPackage());

		}

		private IEnumerator InitPackage()
		{
			// 创建默认的资源包
			defaultPackage = YooAssets.TryGetAssetsPackage(packageName);
			if (defaultPackage == null)
			{
				defaultPackage = YooAssets.CreateAssetsPackage(packageName, packageVersion);
				YooAssets.SetDefaultAssetsPackage(defaultPackage);
			}

			// 编辑器下的模拟模式
			InitializationOperation initializationOperation = null;
			if (playMode == EPlayMode.EditorSimulateMode)
			{
				var createParameters = new EditorSimulateModeParameters();
				createParameters.SimulatePatchManifestPath = EditorSimulateModeHelper.SimulateBuild(packageName);
				initializationOperation = defaultPackage.InitializeAsync(createParameters);
			}

			// 单机运行模式
			if (playMode == EPlayMode.OfflinePlayMode)
			{
				var createParameters = new OfflinePlayModeParameters();
				createParameters.DecryptionServices = new GameDecryptionServices();
				initializationOperation = defaultPackage.InitializeAsync(createParameters);
			}

			// 联机运行模式
			if (playMode == EPlayMode.HostPlayMode)
			{
				var createParameters = new HostPlayModeParameters();
				createParameters.DecryptionServices = new GameDecryptionServices();
				createParameters.QueryServices = new GameQueryServices();
				createParameters.DefaultHostServer = GetHostServerURL();
				createParameters.FallbackHostServer = GetHostServerURL();
				initializationOperation = defaultPackage.InitializeAsync(createParameters);
			}

			yield return initializationOperation;
			if (defaultPackage.InitializeStatus == EOperationStatus.Succeed)
			{
				UniModule.StartCoroutine(GetStaticVersion(defaultPackage));
			}
			else
			{
				Debug.LogWarning($"{initializationOperation.Error}");
			}
		}

		/// <summary>
		/// 获取资源服务器地址
		/// </summary>
		private string GetHostServerURL()
		{
#if UNITY_EDITOR
			if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android)
				return $"{baseUrl}/Android/{gameVersion}";
			else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS)
				return $"{baseUrl}/iOS/{gameVersion}";
			else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.WebGL)
				return $"{baseUrl}/WebGL/{gameVersion}";
			else
				return $"{baseUrl}/StandaloneWindows64/{gameVersion}";
#else
		if (Application.platform == RuntimePlatform.Android)
			return $"{baseUrl}/Android/{gameVersion}";
		else if (Application.platform == RuntimePlatform.IPhonePlayer)
			return $"{baseUrl}/iOS/{gameVersion}";
		else if (Application.platform == RuntimePlatform.WebGLPlayer)
			return $"{baseUrl}/WebGL/{gameVersion}";
		else
			return $"{baseUrl}/StandaloneWindows64/{gameVersion}";
#endif
		}

		private IEnumerator GetStaticVersion(AssetsPackage package)
		{
			var operation = package.UpdatePackageVersionAsync();
			yield return operation;

			if (operation.Status == EOperationStatus.Succeed)
			{
				UniModule.StartCoroutine(UpdateManifest(package, operation.PackageVersion.crc));
			}
			else
			{
				Debug.LogWarning(operation.Error);
			}
		}

		private IEnumerator UpdateManifest(AssetsPackage package, string packageVersion)
		{
			var operation = package.UpdatePackageManifestAsync(packageVersion);
			yield return operation;

			if (operation.Status == EOperationStatus.Succeed)
			{
				UniModule.StartCoroutine(CreateDownloader());
			}
			else
			{
				Debug.LogWarning(operation.Error);
			}
		}

		IEnumerator CreateDownloader()
		{
			yield return new WaitForSecondsRealtime(0.5f);

			int downloadingMaxNum = 10;
			int failedTryAgain = 3;
			var downloader = YooAssets.CreatePatchDownloader(downloadingMaxNum, failedTryAgain);

			if (downloader.TotalDownloadCount == 0)
			{
				Debug.Log("Not found any download files !");
				var package = YooAsset.YooAssets.GetAssetsPackage("DefaultPackage");
				var operation = package.ClearUnusedCacheFilesAsync();
				operation.Completed += Operation_Completed;
			}
			else
			{
				//A total of 10 files were found that need to be downloaded
				Debug.Log($"Found total {downloader.TotalDownloadCount} files that need download ！");

				// 发现新更新文件后，挂起流程系统
				// 注意：开发者需要在下载前检测磁盘空间不足
				int totalDownloadCount = downloader.TotalDownloadCount;
				long totalDownloadBytes = downloader.TotalDownloadBytes;
				UniModule.StartCoroutine(BeginDownload(downloader));
			}
		}

		private IEnumerator BeginDownload(PatchDownloaderOperation downloader)
		{
			// 注册下载回调
			//downloader.OnDownloadErrorCallback = PatchEventDefine.WebFileDownloadFailed.SendEventMessage;
			//downloader.OnDownloadProgressCallback = PatchEventDefine.DownloadProgressUpdate.SendEventMessage;
			downloader.BeginDownload();
			yield return downloader;

			// 检测下载结果
			if (downloader.Status != EOperationStatus.Succeed)
				yield break;

			Operation_Completed(null);
		}

		private void Operation_Completed(AsyncOperationBase obj)
		{
			var appDataConfigsHandle = defaultPackage.LoadAssetSync<AppInitDataConfigs>("StartupDataConfigs/AppDataConfigs.asset");

			AppInitDataConfigs appDataConfigs = appDataConfigsHandle.AssetObject as AppInitDataConfigs;
			string startSceneAddress = appDataConfigs.StartSceneAddress;
			LoadMetadataForAOTAssembly(defaultPackage, appDataConfigs.AotDllList);
			LoadAssembly(defaultPackage, startSceneAddress).Forget();

			appDataConfigsHandle.Release();
		}

		async UniTask LoadAssembly(AssetsPackage assetsPackage, string StartSceneAddress)
		{
			await assetsPackage.LoadAssembly("HybridCLRHotDll.");

			assetsPackage.LoadSceneAsync(StartSceneAddress);
		}
		unsafe void LoadMetadataForAOTAssembly(AssetsPackage assetsPackage, List<string> aotDllList)
		{
#if !UNITY_EDITOR

			foreach (var aotDllName in aotDllList)
			{
				var textAssetHandle = assetsPackage.LoadAssetAsync<TextAsset>(aotDllName);

				var textAsset = (TextAsset)textAssetHandle.AssetObject;

				var datas = textAsset.bytes;
				try
				{
					using (MemoryStream ms = new MemoryStream(datas))
					{
						var dllBytes = AESEncrypt.Decrypt(datas, "HybridCLRAotDll.");
						fixed (byte* ptr = dllBytes)
						{
							// 加载assembly对应的dll，会自动为它hook。一旦aot泛型函数的native函数不存在，用解释器版本代码
							int err = HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly(ptr, dllBytes.Length, 1);
							Debug.Log("LoadMetadataForAOTAssembly. :" + aotDllName);
						}
						Array.Clear(dllBytes, 0, dllBytes.Length);
					}
				}
				catch (Exception ex)
				{
					Debug.LogError("LoadMetadataForAOTAssembly. ex:" + ex.ToString());
				}

			}
#endif
		}

		/// <summary>
		/// 内置文件查询服务类
		/// </summary>
		private class GameQueryServices : IQueryServices
		{
			public bool QueryStreamingAssets(string packageName, string fileName)
			{
				string buildinFolderName = YooAssets.GetStreamingAssetBuildinFolderName();
				var exist = StreamingAssetsHelper.FileExists($"{buildinFolderName}/{packageName}/{fileName}");
				Debug.Log($"{buildinFolderName}/{packageName}/{fileName} exist : {exist}");
				return exist;
			}
		}

		/// <summary>
		/// 资源文件解密服务类
		/// </summary>
		private class GameDecryptionServices : IDecryptionServices
		{
			public ulong LoadFromFileOffset(DecryptFileInfo fileInfo)
			{
				return 32;
			}

			public byte[] LoadFromMemory(DecryptFileInfo fileInfo)
			{
				throw new NotImplementedException();
			}

			public FileStream LoadFromStream(DecryptFileInfo fileInfo)
			{
				FileStream bundleStream = new FileStream(fileInfo.FilePath, FileMode.Open);
				return bundleStream;
			}

			public uint GetManagedReadBufferSize()
			{
				return 1024;
			}
		}

	}
}