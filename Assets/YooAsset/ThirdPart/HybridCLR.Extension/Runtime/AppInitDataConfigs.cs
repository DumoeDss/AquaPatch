using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace HybridCLR.Extension.Runtime
{
    [UnityEngine.Scripting.Preserve]
    [CreateAssetMenu(fileName = "AppInitDataConfigs", menuName = "ScriptableObject/Create AppInitDataConfigs")]
    public class AppInitDataConfigs : ScriptableObject
    {
        public string StartSceneAddress;
        public List<string> AotDllList;
    }
}