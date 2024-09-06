using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * Handles creating the DirectTree for properties that need correction when scaling the avatar
     */
    [VFService]
    internal class ScalePropertyCompensationService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly ScaleFactorService scaleFactorService;
        [VFAutowired] private readonly DirectBlendTreeService directTree;
        [VFAutowired] private readonly ClipFactoryService clipFactory;

        public void AddScaledProp(VFGameObject scaleReference, IList<(UnityEngine.Component component, string PropertyName, float LocalValue)> properties) {
            var worldSpace = GameObjects.Create("WorldSpace", scaleReference);
            ConstraintUtils.MakeWorldSpace(worldSpace);
            var scaleFactor = scaleFactorService.Get(scaleReference, worldSpace);
            if (scaleFactor == null) return;
            AddScaledProp(scaleFactor, properties);
        }

        public void AddScaledProp(VFAFloat scaleFactor, IList<(UnityEngine.Component component, string PropertyName, float LocalValue)> properties) {
            if (scaleFactor == null) return;
            var zeroClip = clipFactory.NewClip($"scaleComp_zero");
            directTree.Add(zeroClip);

            var scaleClip = clipFactory.NewClip($"scaleComp_one");
            directTree.Add(scaleFactor, scaleClip);

            foreach (var prop in properties) {
                scaleClip.SetCurve(
                    prop.component,
                    prop.PropertyName,
                    prop.LocalValue
                );
                zeroClip.SetCurve(
                    prop.component,
                    prop.PropertyName,
                    0
                );
            }
        }
    }
}
