using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using HybridCLR.Editor.Commands;
using AquaSys.Patch.Encryption;
using HybridCLR.Extension.Runtime;
using HybridCLR.Editor;
using HybridCLR.Extension.Editor;

namespace YooAsset.Editor
{
    [TaskAttribute("获取Assemblies")]
    public class TaskBuildAssemblies : IBuildTask
    {
		void IBuildTask.Run(BuildContext context)
		{
         
        }
        public static string HybridCLRBuildCacheDir => Application.dataPath + "/HybridCLRBuildCache";

        public static string AssetBundleSourceDataTempDir => $"{HybridCLRBuildCacheDir}/AssetBundleSourceData";

        public static string GetAssetBundleTempDirByTarget(BuildTarget target)
        {
            return $"{AssetBundleSourceDataTempDir}/{target}";
        }

        public static string ToRelativeAssetPath(string s)
        {
            return s.Substring(s.IndexOf("Assets/"));
        }
        public static List<string> ModDlls = new List<string>()
        {


        };

        public static void BuildAssetBundleByTarget(BuildTarget target)
        {
            BuildAssemblies(BuildConfig.GetAssembliesTempDirTempDirByTarget(target), EditorUserBuildSettings.activeBuildTarget);
        }

        private static void BuildAssemblies(string tempDir, BuildTarget target)
        {
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(tempDir + "/AOT");
            Directory.CreateDirectory(tempDir + "/HotUpdate");
            CompileDllCommand.CompileDll(target);

            string platform = "";
            switch (target)
            {
                case BuildTarget.StandaloneOSX:
                    platform = "OSX";
                    break;
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    platform = "Windows";
                    break;
                case BuildTarget.iOS:
                    platform = "iOS";
                    break;
                case BuildTarget.Android:
                    platform = "Android";
                    break;
            }
            Directory.CreateDirectory(tempDir + "/AOT/" + platform);
            Directory.CreateDirectory(tempDir + "/HotUpdate/" + platform);

            List<string> notSceneAssets = new List<string>();

            string hotfixDllSrcDir = BuildConfig.GetHotFixDllsOutputDirByTarget(target);
            foreach (var dll in SettingsUtil.HotUpdateAssemblyFilesIncludePreserved)
            {
                string dllPath = $"{hotfixDllSrcDir}/{dll}";
                string dllBytesPath = $"{tempDir}/HotUpdate/{platform}/{dll}.bytes";
                //foreach (var item in ModDlls)
                //{
                //    if (dll.Contains(item))
                //    {
                //        dllBytesPath = $"{tempDir}/{item}/{platform}/{dll}.bytes";
                //        break;
                //    }
                //}
                if (File.Exists(dllBytesPath))
                {
                    File.Delete(dllBytesPath);
                }
                AESEncrypt.Encrypt(dllPath, dllBytesPath, "hotfix");
                notSceneAssets.Add(dllBytesPath);
            }

            string aotDllDir = BuildConfig.GetAssembliesPostIl2CppStripDir(target);

            foreach (var dll in BuildConfig.AOTMetaDlls)
            {
                string dllPath = $"{aotDllDir}/{dll}";
                if (!File.Exists(dllPath))
                {
                    Debug.LogError($"ab中添加AOT补充元数据dll:{dllPath} 时发生错误,文件不存在。裁剪后的AOT dll在BuildPlayer时才能生成，因此需要你先构建一次游戏App后再打包。");
                    return;
                }
                string dllBytesPath = $"{tempDir}/AOT/{platform}/{dll}.bytes";
                if (File.Exists(dllBytesPath))
                {
                    File.Delete(dllBytesPath);
                }
                AESEncrypt.Encrypt(dllPath, dllBytesPath, "aot");
                notSceneAssets.Add(dllBytesPath);
            }

            AppInitDataConfigs appDataConfigs = AssetDatabase.LoadAssetAtPath<AppInitDataConfigs>(
                "Assets/DataConfigs/AppDataConfigs.asset");
            appDataConfigs.AotDllList = new List<string>();
            foreach (var item in BuildConfig.AOTMetaDlls)
            {
                appDataConfigs.AotDllList.Add("aot/" + item + ".bytes");
            }
            EditorUtility.SetDirty(appDataConfigs);
            AssetDatabase.Refresh();
        }
    }
}