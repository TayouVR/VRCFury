﻿using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks {
    /**
     * When you go into play mode with a bunch of VRC contact receivers on an avatar with an Animator
     * that has no controller, it dumps a "Animator is not playing an AnimatorController" warning to console for every one,
     * when it tries to access Animator.properties. This fixes the issue by subbing in null if the animator doesn't have a controller.
     */
    internal static class FixNotPlayingWarningHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            var methodToPatch = ReflectionUtils.GetTypeFromAnyAssembly("VRC.Dynamics.AnimParameterAccessAvatarSDK")?.GetConstructor(
                new [] { typeof(Animator), typeof(string) }
            );

            var prefix = typeof(FixNotPlayingWarningHook).GetMethod(
                nameof(Prefix),
                BindingFlags.Static | BindingFlags.NonPublic
            );

            HarmonyUtils.Patch(methodToPatch, prefix);
        }

        static void Prefix(ref Animator __0) {
            if (__0 != null && __0.runtimeAnimatorController == null) {
                __0 = null;
            }
        }
    }
}
