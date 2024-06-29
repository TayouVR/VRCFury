﻿using System;
using UnityEditor.Animations;
using UnityEngine;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    [VFService]
    [VFPrototypeScope]
    internal class ClipFactoryService {
        [VFAutowired] private readonly VFInjectorParent parent;
        [VFAutowired] private readonly ClipFactoryTrackingService clipFactoryTracking;

        public AnimationClip GetEmptyClip() {
            return NewClip("Empty");
        }
        public AnimationClip NewClip(string name) {
            var clip = VrcfObjectFactory.Create<AnimationClip>();
            clipFactoryTracking.MarkCreated(clip);
            clip.name = $"{GetPrefix()}/{name}";
            return clip;
        }

        private BlendTree NewBlendTree(string name, BlendTreeType type) {
            var tree = VrcfObjectFactory.Create<BlendTree>();
            clipFactoryTracking.MarkCreated(tree);
            tree.name = $"{GetPrefix()}/{name}";
            tree.useAutomaticThresholds = false;
            tree.blendType = type;
            return tree;
        }
        
        public VFBlendTreeDirect NewDBT(string name) {
            var tree = NewBlendTree(name, BlendTreeType.Direct);
            return new VFBlendTreeDirect(tree);
        }
        
        public VFBlendTree1D New1D(string name, string blendParameter) {
            var tree = NewBlendTree(name, BlendTreeType.Simple1D);
            tree.blendParameter = blendParameter;
            return new VFBlendTree1D(tree);
        }
        
        public VFBlendTree2D NewSimpleDirectional2D(string name, string blendParameterX, string blendParameterY) {
            var tree = NewBlendTree(name, BlendTreeType.SimpleDirectional2D);
            tree.blendParameter = blendParameterX;
            tree.blendParameterY = blendParameterY;
            return new VFBlendTree2D(tree);
        }
        
        public VFBlendTree2D NewFreeformDirectional2D(string name, string blendParameterX, string blendParameterY) {
            var tree = NewBlendTree(name, BlendTreeType.FreeformDirectional2D);
            tree.blendParameter = blendParameterX;
            tree.blendParameterY = blendParameterY;
            return new VFBlendTree2D(tree);
        }

        public string GetPrefix() {
            var name = $"{parent.parent.GetType().Name}";
            if (parent.parent is FeatureBuilder builder) {
                name += $" #{builder.uniqueModelNum}";
                var prefix = builder.GetClipPrefix();
                if (prefix != null) name += $" ({prefix})";
            }
            return name;
        }
    }
}
