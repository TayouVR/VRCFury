using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    [VFService]
    internal class FixMasksBuilder : FeatureBuilder {

        [VFAutowired] private readonly AnimatorLayerControlOffsetBuilder animatorLayerControlManager;
        [VFAutowired] private readonly LayerSourceService layerSourceService;

        [FeatureBuilderAction(FeatureOrder.FixGestureFxConflict)]
        public void FixGestureFxConflict() {
            if (manager.GetAllUsedControllers().All(c => c.GetType() != VRCAvatarDescriptor.AnimLayerType.Gesture)) {
                // No customized gesture controller
                return;
            }

            var gesture = manager.GetController(VRCAvatarDescriptor.AnimLayerType.Gesture);
            var newFxLayers = new List<AnimatorControllerLayer>();

            var copyForFx = MutableManager.CopyRecursiveAdv(gesture.GetRaw().GetRaw());
            var copyForFxLayers = copyForFx.output.layers;
            foreach (var pair in copyForFx.originalToCopy) {
                if (pair.Key is VRCAnimatorLayerControl from && pair.Value is VRCAnimatorLayerControl to) {
                    animatorLayerControlManager.Alias(from, to);
                }
            }

            gesture.GetRaw().layers = gesture.GetRaw().layers.Select((layerForGesture,i) => {
                
                var propTypes = new AnimatorIterator.Clips().From(new VFLayer(null,layerForGesture.stateMachine))
                    .SelectMany(clip => {
                        if (clip.IsProxyClip()) return new[]{ EditorCurveBindingType.Muscle };
                        return clip.GetAllBindings().Select(b => b.GetPropType());
                    })
                    .ToImmutableHashSet();

                if (!propTypes.Contains(EditorCurveBindingType.Fx) && !propTypes.Contains(EditorCurveBindingType.Aap)) {
                    // Keep it only in gesture
                    return layerForGesture;
                }

                var layerForFx = copyForFxLayers[i];
                newFxLayers.Add(layerForFx);
                animatorLayerControlManager.Alias(layerForGesture.stateMachine, layerForFx.stateMachine);
                layerSourceService.CopySource(layerForGesture.stateMachine, layerForFx.stateMachine);

                if (propTypes.Contains(EditorCurveBindingType.Muscle) || propTypes.Contains(EditorCurveBindingType.Aap)) {
                    // We're keeping both layers
                    // Remove behaviours from the fx copy
                    AnimatorIterator.ForEachBehaviourRW(new VFLayer(null,layerForFx.stateMachine), (behaviour, add) => false);
                    return layerForGesture;
                } else {
                    // We're only keeping it in FX
                    // Delete it from Gesture
                    return null;
                }
            }).NotNull().ToArray();

            if (newFxLayers.Count > 0) {
                fx.GetRaw().layers = newFxLayers.Concat(fx.GetRaw().layers).ToArray();
                foreach (var p in gesture.GetRaw().parameters) {
                    fx.GetRaw().NewParam(p.name, p.type, n => {
                        n.defaultBool = p.defaultBool;
                        n.defaultFloat = p.defaultFloat;
                        n.defaultInt = p.defaultInt;
                    });
                }
            }
            
            // This clip cleanup must happen after the merge is all finished,
            // because otherwise `propTypes` above could be wrong for some layers that use shared clips
            // if we cleanup while iterating through the layers
            
            // Remove fx bindings from the gesture copy
            foreach (var clip in new AnimatorIterator.Clips().From(gesture.GetRaw())) {
                clip.Rewrite(AnimationRewriter.RewriteBinding(b => {
                    if (b.GetPropType() == EditorCurveBindingType.Fx) return null;
                    return b;
                }));
            }

            foreach (var layerForGesture in gesture.GetLayers()) {
                if (layerForGesture.mask != null) {
                    layerForGesture.mask.AllowAllTransforms();
                }
            }

            // Remove muscle control from the fx copy
            foreach (var clip in new AnimatorIterator.Clips().From(fx.GetRaw())) {
                clip.Rewrite(AnimationRewriter.RewriteBinding(b => {
                    if (b.GetPropType() == EditorCurveBindingType.Muscle) return null;
                    return b;
                }));
            }

            foreach (var layerForFx in fx.GetLayers()) {
                if (layerForFx.mask != null && layerForFx.mask.AllowsAllTransforms()) {
                    layerForFx.mask = null;
                }
            }
        }
        
        [FeatureBuilderAction(FeatureOrder.FixMasks)]
        public void FixMasks() {
            foreach (var layer in GetFx().GetLayers()) {
                // Remove redundant FX masks if they're not needed
                if (layer.mask != null && layer.mask.AllowsAllTransforms()) {
                    layer.mask = null;
                }
            }

            foreach (var c in manager.GetAllUsedControllers()) {
                var ctrl = c.GetRaw();

                AvatarMask expectedMask = null;
                if (c.GetType() == VRCAvatarDescriptor.AnimLayerType.Gesture) {
                    expectedMask = GetGestureMask(c);
                }

                var layer0 = ctrl.GetLayer(0);
                var createEmptyBaseLayer = false;
                if (layer0 == null) {
                    // If there are no layers, we still create a base layer because the VRCSDK freaks out if there is a controller with no layers
                    createEmptyBaseLayer = true;
                } else if (layer0.mask != expectedMask) {
                    // If the mask on layer 0 doesn't match what the base mask needs to be, we need to correct it by making a new base layer
                    createEmptyBaseLayer = true;
                } else if (c.GetType() == VRCAvatarDescriptor.AnimLayerType.FX && layer0.stateMachine.states.Length > 1) {
                    // On FX, do not allow a layer with transitions to live as the base layer, because for some reason transition times can break
                    //   and animate immediately when performed within the base layer
                    createEmptyBaseLayer = true;
                }

                if (createEmptyBaseLayer) {
                    c.EnsureEmptyBaseLayer().mask = expectedMask;
                }
            }
        }

        /**
         * We build the gesture base mask by unioning all the masks from the other layers.
         */
        private AvatarMask GetGestureMask(ControllerManager gesture) {
            var mask = AvatarMaskExtensions.Empty();
            foreach (var layer in gesture.GetLayers()) {
                if (layer.mask == null) throw new Exception("Gesture layer unexpectedly contains no mask");
                mask.UnionWith(layer.mask);
            }
            return mask;
        }
    }
}
