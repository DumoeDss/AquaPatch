using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using HybridCLR.Editor.Commands;
using AquaSys.Patch.Encryption;
using HybridCLR.Extension.Runtime;
using HybridCLR.Editor;
using System.Linq;
using System;
using System.Text;

namespace YooAsset.Editor
{
    public class AssembliesContext : IContextObject
    {
        public string hotUpdateRootDir;
        public string platform;
    }

    [TaskAttribute("获取Assemblies")]
    public class TaskBuildAssemblies : IBuildTask
    {
        string linkXmlPath;
        void IBuildTask.Run(BuildContext context)
		{
            linkXmlPath = $"{Application.dataPath}/{SettingsUtil.HybridCLRSettings.outputLinkFile}";
            if (!File.Exists(linkXmlPath) || SettingsUtil.HybridCLRSettings.isAutoGenerateXml)
            {
                LinkGeneratorCommand.GenerateLinkXml();
            }

            AssembliesContext assembliesContext = new AssembliesContext();
            BuildAssetBundleByTarget(assembliesContext);
            context.SetContextObject(assembliesContext);
        }

        public void BuildAssetBundleByTarget(AssembliesContext assembliesContext)
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            BuildAssemblies(AssembliesTempDir, assembliesContext, target);
        }

        private void BuildAssemblies(string tempDir, AssembliesContext assembliesContext, BuildTarget target)
        {
            CompileDllCommand.CompileDll(target);

            string platform = "";
            switch (target)
            {
                case BuildTarget.StandaloneLinux64:
                    platform = "Linux";
                    break;
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
            assembliesContext.hotUpdateRootDir = tempDir;
            assembliesContext.platform = platform;

            var aotPath = Path.Combine(tempDir, "AOT", platform);
            CreateDirectory(aotPath);

            string hotfixDllSrcDir = GetHotFixDllsOutputDirByTarget(target);
            Dictionary<string,List<string>> hotfixAssets = new Dictionary<string,List<string>>();

            foreach (var pair in SettingsUtil.HybridCLRSettings.hotUpdateAssemblies)
            {
                if (!hotfixAssets.ContainsKey(pair.Key))
                {
                    hotfixAssets[pair.Key] = new List<string>();
                }
                hotfixAssets[pair.Key].AddRange(pair.Value.list);
            }

            foreach (var pair in SettingsUtil.HybridCLRSettings.hotUpdateAssemblyDefinitions)
            {
                if (!hotfixAssets.ContainsKey(pair.Key))
                {
                    hotfixAssets[pair.Key] = new List<string>();
                }
                hotfixAssets[pair.Key].AddRange(pair.Value.list.Select(_=>_.name));
            }

            foreach (var pair in hotfixAssets)
            {
                var list = pair.Value.Distinct().ToList();
                var folder = pair.Key == SettingsUtil.HybridCLRSettings.defaultPackageName ? "HotUpdate" : pair.Key;
                var path = Path.Combine(tempDir, folder, platform);
                CreateDirectory(path);

                foreach (var dll in list)
                {
                    if(AOTMetaDlls.Contains($"{dll}.dll"))
                        AOTMetaDlls.Remove($"{dll}.dll");
                    string dllPath = $"{hotfixDllSrcDir}/{dll}.dll";
                    string dllBytesPath = $"{path}/{dll}.bytes";
                    if (File.Exists(dllBytesPath))
                    {
                        File.Delete(dllBytesPath);
                    }
                    AESEncrypt.Encrypt(dllPath, dllBytesPath,Convert.ToBase64String(Encoding.UTF8.GetBytes(SettingsUtil.HybridCLRSettings.hotUpdateDllPassword)));
                }
            }
           
            AppInitDataConfigs appDataConfigs = AssetDatabase.LoadAssetAtPath<AppInitDataConfigs>("Assets/_DynamicAssets/_DataConfigs/AppInitDataConfigs.asset");
            appDataConfigs.AotDllList = new List<string>();

            foreach (var dll in AOTMetaDlls)
            {
                string dllPath;
                string aotDllDir;
                if (SettingsUtil.HybridCLRSettings.homologousImageMode == HybridCLR.HomologousImageMode.Consistent)
                {
                    aotDllDir = GetAssembliesPostIl2CppStripDir(target);

                    dllPath = $"{aotDllDir}/{dll}";
                    if (!File.Exists(dllPath))
                    {
                        Debug.LogError($"ab中添加AOT补充元数据dll:{dllPath} 时发生错误,文件不存在。裁剪后的AOT dll在BuildPlayer时才能生成，因此需要你先构建一次游戏App后再打包。");
                        return;
                    }
                   
                }
                else
                {
                    var hotUpdateDllFolder = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
                    dllPath = $"{hotUpdateDllFolder}/{dll}";
                    if (!File.Exists(dllPath))
                    {
#if UNITY_2021_1_OR_NEWER
#if UNITY_STANDALONE_WIN
                        var unityAotPath = "MonoBleedingEdge/lib/mono/unityaot-win32";
                        var managedPath = "Playbackengines/windowsstandalonesupport/Variations/il2cpp/Managed";
#elif UNITY_ANDROID
                        var unityAotPath = "MonoBleedingEdge/lib/mono/unityaot-linux";
                        var managedPath = "Playbackengines/AndroidPlayer/Variations/il2cpp/Managed";
#elif UNITY_IOS
                        var unityAotPath = "MonoBleedingEdge/lib/mono/unityaot-macos";
                        var managedPath = "Playbackengines/iOSSupport/Variations/il2cpp/Managed";

#elif UNITY_WEBGL
                        var unityAotPath = "MonoBleedingEdge/lib/mono/unity_web";
                        var managedPath = "Playbackengines/WebGLSupport/Variations/il2cpp/Managed";
#else
#endif

#else
                        var unityAotPath = "MonoBleedingEdge/lib/mono/unityaot";
#if UNITY_STANDALONE_WIN
                        var managedPath = "Playbackengines/windowsstandalonesupport/Variations/il2cpp/Managed";
#elif UNITY_ANDROID
                        var managedPath = "Playbackengines/AndroidPlayer/Variations/il2cpp/Managed";
#elif UNITY_IOS
                        var managedPath = "Playbackengines/iOSSupport/Variations/il2cpp/Managed";
#elif UNITY_WEBGL
                        unityAotPath = "MonoBleedingEdge/lib/mono/unity_web";
                        var managedPath = "Playbackengines/WebGLSupport/Variations/il2cpp/Managed";

#endif
#endif
                        dllPath = $"{SettingsUtil.HybridCLRSettings.unityInstallRootDir}/{unityAotPath}/{dll}";
                        if (!File.Exists(dllPath))
                        {
                            dllPath = $"{SettingsUtil.HybridCLRSettings.unityInstallRootDir}/{managedPath}/{dll}";
                            if (!File.Exists(dllPath))
                            {

                                aotDllDir = GetAssembliesPostIl2CppStripDir(target);

                                dllPath = $"{aotDllDir}/{dll}";
                                if (!File.Exists(dllPath))
                                {
                                    Debug.LogError($"ab中添加AOT补充元数据dll:{dll} 时发生错误,文件不存在。裁剪后的AOT dll在BuildPlayer时才能生成，因此需要你先构建一次游戏App后再打包。");
                                    throw new NotImplementedException();
                                }
                                else
                                {
                                    Debug.LogError($"添加AOT补充元数据dll: {dll} 时发生错误,未找到完整的dll文件,当前采用裁剪后的AOT dll。建议将该dll复制到 HybridCLRData/HotUpdateDlls/{target}");
                                }
                            }
                        }

                    }
                }

                string dllBytesPath = $"{aotPath}/{dll}.bytes";
  
               // AESEncrypt.Encrypt(dllPath, dllBytesPath, Convert.ToBase64String(Encoding.UTF8.GetBytes(SettingsUtil.HybridCLRSettings.aotDllPassword)));
                File.Copy(dllPath, dllBytesPath, true);
                appDataConfigs.AotDllList.Add("aot/" + dll + ".bytes");
            }

            EditorUtility.SetDirty(appDataConfigs);
            AssetDatabase.Refresh();
        }

        void CreateDirectory(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        List<string> _AOTMetaDlls;

        public List<string> AOTMetaDlls
        {
            get
            {
                if(_AOTMetaDlls == null|| _AOTMetaDlls.Count==0)
                {
                    _AOTMetaDlls = LoadLink().Distinct().ToList();
                    for (int i = 0; i < _AOTMetaDlls.Count; i++)
                    {
                        _AOTMetaDlls[i] += ".dll";
                    }
                }
                return _AOTMetaDlls;
               
            }

            set
            {
                _AOTMetaDlls = value;
            }
        }

        private List<string> LoadLink()
        {
            var outList = new List<string>();
            var arr = File.ReadAllLines(linkXmlPath);
            foreach (var line in arr)
            {
                if (!line.Contains("assembly fullname"))
                {
                    continue;
                }

                var sp = line.Split('"');
                outList.Add(sp[1]);
            }

            return outList;
        }

        #region Folder
        public static string ToRelativeAssetPath(string s)
        {
            return s.Substring(s.IndexOf("Assets/"));
        }

        public static string HybridCLRBuildCacheDir => $"{Application.dataPath}/{SettingsUtil.HotUpdateDllsRootOutputDir}";

        public static string AssembliesTempDir => $"{HybridCLRBuildCacheDir}";

        public static string GetHotFixDllsOutputDirByTarget(BuildTarget target)
        {
            return $"{ProjectDir}/{SettingsUtil.HotUpdateDllsRootOutputDir}/{target}";
        }

        public static string GetOriginBuildStripAssembliesDir(BuildTarget target)
        {
#if UNITY_2021_1_OR_NEWER
#if UNITY_STANDALONE_WIN
            return $"{ProjectDir}/Library/Bee/artifacts/WinPlayerBuildProgram/ManagedStripped";
#elif UNITY_ANDROID
            return $"{ProjectDir}/Library/Bee/artifacts/Android/ManagedStripped";
#elif UNITY_IOS
            return $"{ProjectDir}/Library/PlayerDataCache/iOS/Data/Managed";
#elif UNITY_WEBGL
            return $"{ProjectDir}/Library/Bee/artifacts/WebGL/ManagedStripped";
#else
            throw new NotSupportedException("GetOriginBuildStripAssembliesDir");
#endif
#else
            return target == BuildTarget.Android ?
                $"{ProjectDir}/Temp/StagingArea/assets/bin/Data/Managed" :
                $"{ProjectDir}/Temp/StagingArea/Data/Managed/";
#endif
        }

        public static string ProjectDir => Directory.GetParent(Application.dataPath).ToString();

        public static string GetAssembliesPostIl2CppStripDir(BuildTarget target)
        {
            return $"{ProjectDir}/{SettingsUtil.AssembliesPostIl2CppStripDir}/{target}";
        }
        #endregion

    }
}