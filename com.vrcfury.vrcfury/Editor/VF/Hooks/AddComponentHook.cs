﻿using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Feature;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;

namespace VF.Hooks {
    internal static class AddComponentHook {
        private static bool addedThisFrame = false;
        
        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.delayCall += AddToMenu;
            if (MenuChangedAddHandler != null) {
                Action onMenuChange = () => {
                    if (addedThisFrame) return;
                    EditorApplication.delayCall -= AddToMenu;
                    EditorApplication.delayCall += AddToMenu;
                };
                MenuChangedAddHandler.Invoke(null, new object[] { onMenuChange });
            }
        } 

        private static readonly MethodInfo MenuChangedAddHandler = typeof(UnityEditor.Menu).GetEvent("menuChanged",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetAddMethod(true);
        private static readonly MethodInfo RemoveMenuItem = typeof(UnityEditor.Menu).GetMethod("RemoveMenuItem",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo AddMenuItem = typeof(UnityEditor.Menu).GetMethod("AddMenuItem",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static void Add(string path, string shortcut, bool @checked, int priority, Action execute, Func<bool> validate) =>
            AddMenuItem?.Invoke(null, new object[] { path, shortcut, @checked, priority, execute, validate });
        private static void Remove(string path) => RemoveMenuItem?.Invoke(null, new object[] { path });

        private static void ResetAddedThisFrame() {
            addedThisFrame = false;
        }

        private static void AddToMenu() {
            //Debug.Log("Adding VRCFury components to menu");
            addedThisFrame = true;
            EditorApplication.delayCall -= ResetAddedThisFrame;
            EditorApplication.delayCall += ResetAddedThisFrame;

            Remove("Component/UI/Toggle");
            Remove("Component/UI/Legacy/Dropdown");
            Remove("Component/UI/Toggle Group");

            foreach (var feature in FeatureFinder.GetAllFeaturesForMenu()) {
                var editorInst = (FeatureBuilder)Activator.CreateInstance(feature.Value);
                var title = editorInst.GetEditorTitle();
                if (title != null) {
                    Add(
                        "Component/VRCFury/VRCFury | " + title,
                        "",
                        false,
                        0,
                        () => {
                            var failureMsg = editorInst.FailWhenAdded();
                            if (failureMsg != null) {
                                EditorUtility.DisplayDialog($"Error adding {title}", failureMsg, "Ok");
                                return;
                            }
                            foreach (var obj in Selection.gameObjects) {
                                if (obj == null) continue;
                                var modelInst = Activator.CreateInstance(feature.Key) as FeatureModel;
                                if (modelInst == null) continue;
                                if (modelInst is ArmatureLink al) {
                                    al.propBone = ArmatureLinkBuilder.GuessLinkFrom(obj);
                                }

                                var c = Undo.AddComponent<VRCFury>(obj);
                                var so = new SerializedObject(c);
                                so.FindProperty("content").managedReferenceValue = modelInst;
                                so.ApplyModifiedPropertiesWithoutUndo();
                            }
                        },
                        null
                    );
                }
            }
        }
    }
}
