using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using HybridCLR.Editor;

namespace HybridCLR.Extension.Editor
{
    public static partial class BuildConfig
    {
        public static string ProjectDir => Directory.GetParent(Application.dataPath).ToString();

        public static string HybridCLRBuildCacheDir => $"{Application.dataPath}/{SettingsUtil.HotUpdateDllsRootOutputDir}"; 

        public static string AssembliesTempDir => $"{HybridCLRBuildCacheDir}";

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

        public static string GetHotFixDllsOutputDirByTarget(BuildTarget target)
        {
            return $"{ProjectDir}/{SettingsUtil.HotUpdateDllsRootOutputDir}/{target}";
        }

        public static string GetAssembliesPostIl2CppStripDir(BuildTarget target)
        {
            return $"{ProjectDir}/{SettingsUtil.AssembliesPostIl2CppStripDir}/{target}";
        }

        public static string GetAssembliesTempDirTempDirByTarget(BuildTarget target)
        {
            return $"{AssembliesTempDir}";
        }

        public static List<string> AOTMetaDlls
        {
            get
            {
                var strlist = LoadLink().Distinct().ToList();
                for (int i = 0; i < strlist.Count; i++)
                {
                    strlist[i] += ".dll";
                }
                return strlist;
            }
        }

        private static List<string> LoadLink()
        {
            var outList = new List<string>();
            var path = $"{Application.dataPath}/{SettingsUtil.HybridCLRSettings.outputLinkFile}";
            var arr = File.ReadAllLines(path);
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
    }
}
