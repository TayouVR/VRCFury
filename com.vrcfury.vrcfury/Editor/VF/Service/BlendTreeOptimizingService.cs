using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    [VFService]
    internal class BlendTreeOptimizingService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly DirectBlendTreeService directTree;
        [VFAutowired] private readonly ClipFactoryTrackingService clipFactoryTracking;
        
        [FeatureBuilderAction(FeatureOrder.OptimizeBlendTrees)]
        public void Optimize() {
            var fx = manager.GetFx();
            foreach (var state in new AnimatorIterator.States().From(fx.GetRaw())) {
                if (state.motion is BlendTree tree && clipFactoryTracking.Created(tree)) {
                    Optimize(tree);
                }
            }
        }

        private void Optimize(BlendTree tree) {
            MergeSubtreesWithOneWeight(tree);
            MergeClipsWithSameWeight(tree);
        }

        private void MergeSubtreesWithOneWeight([CanBeNull] Motion motion) {
            if (!(motion is BlendTree tree)) return;
            foreach (var child in tree.children) {
                MergeSubtreesWithOneWeight(child.motion);
            }
            if (tree.blendType != BlendTreeType.Direct) return;
            if (tree.GetNormalizedBlendValues()) return;
            tree.RewriteChildren(child => {
                if (child.directBlendParameter == manager.GetFx().One()
                    && child.motion is BlendTree childTree
                    && childTree.blendType == BlendTreeType.Direct
                    && !childTree.GetNormalizedBlendValues()
                ) {
                    return childTree.children;
                }
                return new ChildMotion[] { child };
            });
        }

        private void MergeClipsWithSameWeight([CanBeNull] Motion motion) {
            if (!(motion is BlendTree tree)) return;
            foreach (var child in tree.children) {
                MergeClipsWithSameWeight(child.motion);
            }
            if (tree.blendType != BlendTreeType.Direct) return;
            if (tree.GetNormalizedBlendValues()) return;

            var firstClipByWeightParam = new Dictionary<string, AnimationClip>();

            tree.RewriteChildren(child => {
                if (child.motion is AnimationClip clip) {
                    if (firstClipByWeightParam.TryGetValue(child.directBlendParameter, out var firstClip)) {
                        clip.Rewrite(AnimationRewriter.RewriteCurve((binding, curve) => {
                            if (curve.IsFloat && curve.FloatCurve.keys.Length == 1 &&
                                curve.FloatCurve.keys[0].time == 0) {
                                var firstClipCurve = firstClip.GetFloatCurve(binding);
                                if (firstClipCurve != null) {
                                    // Merge curve into first clip's
                                    firstClipCurve.keys = firstClipCurve.keys.Select(key => {
                                        key.value += curve.FloatCurve.keys[0].value;
                                        return key;
                                    }).ToArray();
                                    firstClip.SetCurve(binding, firstClipCurve);
                                } else {
                                    // Just shove it into the first clip
                                    firstClip.SetCurve(binding, curve);
                                }

                                firstClip.name = $"{clipFactory.GetPrefix()}/Flattened";
                                return (binding, null, true);
                            }

                            return (binding, curve, false);
                        }));
                        if (!clip.GetAllBindings().Any()) {
                            return new ChildMotion[] { };
                        }
                    } else {
                        firstClipByWeightParam[child.directBlendParameter] = clip;
                    }
                }

                return new ChildMotion[] { child };
            });
        }
    }
}
