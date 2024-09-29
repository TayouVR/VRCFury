using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Model;
using VF.Model.Feature;
using VRC.SDK3.Avatars.Components;

namespace VF.PlayMode {
    internal static class FixWriteDefaultsLater {
        private const string Key = "vrcfFixWd";
        
        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.ExitingEditMode) {
                    EditorPrefs.DeleteKey(Key);
                }
                
                if (state == PlayModeStateChange.EnteredEditMode) {
                    var data = GetData();
                    EditorPrefs.DeleteKey(Key);
                    foreach (var entry in data.entries) {
                        var avatars = Resources.FindObjectsOfTypeAll<VRCAvatarDescriptor>()
                            .Select(avatar => avatar.owner())
                            .Where(obj => obj.scene.IsValid()) // Exclude prefabs
                            .Where(obj => obj.name == entry.name);
                        foreach (var avatar in avatars) {
                            SaveNow(avatar, entry.auto);
                        }
                    }
                }
            };
        }

        public static void Save(VFGameObject avatar, bool auto) {
            if (Application.isPlaying) {
                SaveLater(avatar, auto);
            } else {
                SaveNow(avatar, auto);
            }
        }

        private static void SaveLater(VFGameObject avatar, bool auto) {
            var data = GetData();
            data.entries.Add(new Entry() { auto = auto, name = avatar.name });
            SetData(data);
        }

        private static void SaveNow(VFGameObject avatar, bool auto) {
            if (avatar.GetComponentsInSelfAndChildren<VRCFury>()
                .SelectMany(v => v.GetAllFeatures())
                .Any(f => f is FixWriteDefaults)) {
                return;
            }

            var vf = avatar.AddComponent<VRCFury>();
            vf.content = new FixWriteDefaults() {
                mode = auto
                    ? FixWriteDefaults.FixWriteDefaultsMode.Auto
                    : FixWriteDefaults.FixWriteDefaultsMode.Disabled
            };
        }

        private static Data GetData() {
            var text = EditorPrefs.GetString(Key);
            if (string.IsNullOrEmpty(text)) return new Data();
            try {
                return JsonUtility.FromJson<Data>(text);
            } catch (Exception) {
                return new Data();
            }
        }

        private static void SetData(Data data) {
            var str = JsonUtility.ToJson(data);
            EditorPrefs.SetString(Key, str);
        }

        [Serializable]
        private class Data {
            public List<Entry> entries = new List<Entry>();
        }

        [Serializable]
        private class Entry {
            public string name;
            public bool auto;
        }
    }
}
