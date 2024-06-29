﻿using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;

namespace VF.Feature {
    internal class SecurityRestrictedBuilder : FeatureBuilder<SecurityRestricted> {
        [VFAutowired] private readonly ObjectMoveService mover;
        [VFAutowired] private readonly DirectBlendTreeService directTree;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        
        [FeatureBuilderAction(FeatureOrder.SecurityRestricted)]
        public void Apply() {
            if (featureBaseObject == avatarObject) {
                throw new Exception("The root object of your avatar cannot be security restricted, sorry!");
            }
            
            var parent = featureBaseObject.parent;
            while (parent != null && parent != avatarObject) {
                if (parent.GetComponents<VRCFury>()
                    .SelectMany(vf => vf.GetAllFeatures())
                    .Any(f => f is SecurityRestricted)) {
                    // some parent is restricted, so we can skip this one and just let the parent handle it
                    return;
                }
                parent = parent.parent;
            }

            var security = allBuildersInRun.OfType<SecurityLockBuilder>().FirstOrDefault();
            if (security == null) {
                Debug.LogWarning("Security pin not set, restriction disabled");
                return;
            }

            var wrapper = GameObjects.Create(
                $"Security Restriction for {featureBaseObject.name}",
                featureBaseObject.parent,
                featureBaseObject.parent);
            
            mover.Move(featureBaseObject, wrapper);

            wrapper.active = false;

            var clip = clipFactory.NewClip("Unlock");
            clipBuilder.Enable(clip, wrapper);
            directTree.Add(security.GetEnabled().AsFloat(), clip);
        }

        public override string GetEditorTitle() {
            return "Security Restricted";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            return VRCFuryEditorUtils.Info(
                "This object will be forcefully disabled until a Security Pin is entered in your avatar's menu." +
                "Note: You MUST have a Security Pin Number component on your avatar root with a pin number set, or this will not do anything!"
            );
        }
    }
}
