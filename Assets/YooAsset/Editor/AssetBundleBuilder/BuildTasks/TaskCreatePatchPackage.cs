using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace YooAsset.Editor
{
	[TaskAttribute("制作补丁包")]
	public class TaskCreatePatchPackage : IBuildTask
	{
		void IBuildTask.Run(BuildContext context)
		{
			var buildParameters = context.GetContextObject<BuildParametersContext>();
			var buildMapContext = context.GetContextObject<BuildMapContext>();
			var patchManifestContext = context.GetContextObject<PatchManifestContext>();
			
			var buildMode = buildParameters.Parameters.BuildMode;
			if (buildMode == EBuildMode.ForceRebuild || buildMode == EBuildMode.IncrementalBuild)
			{
				CopyPatchFiles(buildParameters, buildMapContext, patchManifestContext);
			}
		}

		/// <summary>
		/// 拷贝补丁文件到补丁包目录
		/// </summary>
		private void CopyPatchFiles(BuildParametersContext buildParametersContext, BuildMapContext buildMapContext, PatchManifestContext patchManifestContext)
		{
			var buildParameters = buildParametersContext.Parameters;
			string pipelineOutputDirectory = buildParametersContext.GetPipelineOutputDirectory();
			string packageOutputDirectory = buildParametersContext.GetPackageOutputDirectory();
			BuildRunner.Log($"开始拷贝补丁文件到补丁包目录：{packageOutputDirectory}");

		

			foreach (var PatchManifest in patchManifestContext.PatchManifests)
			{
				var package = PatchManifest.Key.Split('_')[1];
				string dir = $"{packageOutputDirectory}/{package}";

				// 拷贝所有补丁文件
				int progressValue = 0;
				PatchManifest patchManifest = PatchManifest.Value;
				int patchFileTotalCount = patchManifest.BundleList.Count;
				foreach (var patchBundle in patchManifest.BundleList)
				{
					var bundleInfo = buildMapContext.BundleInfos.Find(_ => _.BundleName == patchBundle.BundleName);
					//EditorTools.CopyFile(bundleInfo.PatchInfo.BuildOutputFilePath, bundleInfo.PatchInfo.PatchOutputFilePath, true);

					string sourcePath = bundleInfo.PatchInfo.BuildOutputFilePath;//$"{packageOutputDirectory}/{YooAssetSettings.OutputFolderName}/{patchBundle.BundleName}";
					var fileNames = patchBundle.BundleName.Split('.');
					var extension = $".{fileNames[fileNames.Length - 1]}";
					var fileName = patchBundle.BundleName.Replace(extension, $"_{patchBundle.FileHash}{extension}");
					string destPath = $"{dir}/{fileName}";
					EditorTools.CopyFile(sourcePath, destPath, true);

					EditorTools.DisplayProgressBar("拷贝补丁文件", ++progressValue, patchFileTotalCount);
				}
			}

			if (buildParameters.BuildPipeline == EBuildPipeline.ScriptableBuildPipeline)
			{
				// 拷贝构建日志
				{
					string sourcePath = $"{pipelineOutputDirectory}/buildlogtep.json";
					string destPath = $"{packageOutputDirectory}/buildlogtep.json";
					EditorTools.CopyFile(sourcePath, destPath, true);
				}

				// 拷贝代码防裁剪配置
				if (buildParameters.SBPParameters.WriteLinkXML)
				{
					string sourcePath = $"{pipelineOutputDirectory}/link.xml";
					string destPath = $"{packageOutputDirectory}/link.xml";
					EditorTools.CopyFile(sourcePath, destPath, true);
				}
			}
			else if (buildParameters.BuildPipeline == EBuildPipeline.BuiltinBuildPipeline)
			{
				// 拷贝UnityManifest序列化文件
				{
					string sourcePath = $"{pipelineOutputDirectory}";
					string destPath = $"{packageOutputDirectory}/{YooAssetSettings.OutputFolderName}";
					EditorTools.CopyDirectory(sourcePath, destPath);
					EditorTools.DeleteDirectory(sourcePath);
				}
			}
			else
			{
				throw new System.NotImplementedException();
			}

			EditorTools.ClearProgressBar();
		}
	}
}