﻿using System.Reflection;
using UnityEngine;
using VF.Builder;

namespace VF {
    public static class UnityCompatUtils {
        public static void OpenPrefab(string path, VFGameObject focus) {
#if UNITY_2022_1_OR_NEWER
            UnityEditor.SceneManagement.PrefabStageUtility.OpenPrefab(path, focus);
#else
            var prefabStageUtility = ReflectionUtils.GetTypeFromAnyAssembly(
                "UnityEditor.Experimental.SceneManagement.PrefabStageUtility");
            var open = prefabStageUtility.GetMethod("OpenPrefab",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(GameObject) },
                null
            );
            open.Invoke(null, new object[] { path, focus.gameObject });
#endif
        }
    }
}
