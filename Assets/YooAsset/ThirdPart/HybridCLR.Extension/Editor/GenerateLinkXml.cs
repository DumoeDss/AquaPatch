using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using HybridCLR.Editor;

namespace HybridCLR.Extension.Editor
{
    public static class GenerateLinkXml
    {
        /// <summary>
        /// 所有热更程序集的Dll
        /// </summary>
        private static List<string> hotfixDll = SettingsUtil.HotUpdateAssemblyFilesIncludePreserved;
        public static string path = $"{Application.dataPath}/{SettingsUtil.HybridCLRSettings.outputLinkFile}";

        public static void Generate()
        {
            var CheckList = new List<string>();
            foreach (var dllPath in hotfixDll)
            {
                var dllBytes = File.ReadAllBytes(BuildConfig.GetHotFixDllsOutputDirByTarget(EditorUserBuildSettings.activeBuildTarget)+"/"+dllPath);
                var tempAssembly = Assembly.Load(dllBytes);
                var names = tempAssembly.GetReferencedAssemblies();
                foreach (var assemblyName in names)
                {
                    CheckList.Add(assemblyName.Name);
                }
            }

            var sb = GetLinkSb();
            var linkDlls = LoadLink();
            var list1 = CheckList.Where(x => !linkDlls.Contains(x)).Distinct().ToList();
            foreach (var item in hotfixDll)
            {
                for (int i = 0; i < list1.Count; i++)
                {
                    if (item.Contains(list1[i])){
                        list1.RemoveAt(i);
                        break;
                    }
                }
            }
            foreach (var dll in list1)
            {
                sb.Add($"\t<assembly fullname=\"{dll}\" preserve=\"all\"/>");
            }

            sb.Add("</linker>");
            for (int i = 0; i < sb.Count; i++)
            {
                var line = sb[i];
                sb[i] = line.Replace($"preserve=\"all\"/>", "preserve=\"all\"/>");
            }

            File.WriteAllLines(path, sb, Encoding.UTF8);
            AssetDatabase.Refresh();
        }

        private static List<string> LoadLink()
        {
            var outList = new List<string>();
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

        private static List<string> GetLinkSb()
        {
            var sb = new List<string>();
            var arr = File.ReadAllLines(path);
            foreach (var line in arr)
            {
                if (line.Contains($"</linker>"))
                {
                    continue;
                }

                sb.Add(line);
            }

            return sb;
        }
    }
}