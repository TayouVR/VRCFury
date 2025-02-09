using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature;
using VF.Inspector;
using VF.Service;
using VRC.SDK3.Avatars.Components;

namespace VF.Utils.Controller {
    internal class VFController {
        private readonly AnimatorController ctrl;

        private VFController(AnimatorController ctrl) {
            this.ctrl = ctrl;
        }
    
        public static implicit operator VFController(AnimatorController d) => new VFController(d);
        public static implicit operator AnimatorController(VFController d) => d?.ctrl;
        public static implicit operator bool(VFController d) => d?.ctrl != null;
        public static bool operator ==(VFController a, VFController b) => a?.Equals(b) ?? b?.Equals(null) ?? true;
        public static bool operator !=(VFController a, VFController b) => !(a == b);
        public override bool Equals(object other) {
            return (other is VFController a && ctrl == a.ctrl)
                   || (other is AnimatorController b && ctrl == b)
                   || (other == null && ctrl == null);
        }
        public override int GetHashCode() => Tuple.Create(ctrl).GetHashCode();

        public AnimatorController GetRaw() {
            return ctrl;
        }

        public VFLayer NewLayer(string name, int insertAt = -1) {
            // Unity breaks if name contains .
            name = name.Replace(".", "");

            ctrl.AddLayer(name);
            var layer = new VFLayer(this, ctrl.layers.Last().stateMachine);
            if (insertAt >= 0) {
                layer.Move(insertAt);
            }
            layer.weight = 1;
            layer.stateMachine.anyStatePosition = VFState.CalculateOffsetPosition(layer.stateMachine.entryPosition, 0, 1);
            VrcfObjectFactory.Register(layer.stateMachine);
            return layer;
        }

        public void RemoveParameter(int i) {
            ctrl.RemoveParameter(i);
        }

        public VFABool NewBool(string name, bool def = false) {
            var p = NewParam(name, AnimatorControllerParameterType.Bool, param => param.defaultBool = def);
            return new VFABool(p.name, p.defaultBool);
        }
        public VFAFloat NewFloat(string name, float def = 0) {
            var p = NewParam(name, AnimatorControllerParameterType.Float, param => param.defaultFloat = def);
            return new VFAFloat(p.name, p.defaultFloat);
        }
        public VFAInteger NewInt(string name, int def = 0) {
            var p = NewParam(name, AnimatorControllerParameterType.Int, param => param.defaultInt = def);
            return new VFAInteger(p.name, p.defaultInt);
        }
        public AnimatorControllerParameter NewParam(string name, AnimatorControllerParameterType type, Action<AnimatorControllerParameter> with = null) {
            var exists = GetParam(name);
            if (exists != null) {
                return exists;
            }
            ctrl.AddParameter(name, type);
            var ps = ctrl.parameters;
            var param = ps[ps.Length-1];
            with?.Invoke(param);
            ctrl.parameters = ps;
            return param;
        }

        public AnimatorControllerParameter GetParam(string name) {
            return Array.Find(ctrl.parameters, other => other.name == name);
        }
    
        public IEnumerable<VFLayer> GetLayers() {
            return ctrl.layers.Select(l => new VFLayer(this, l.stateMachine));
        }

        public bool ContainsLayer(AnimatorStateMachine stateMachine) {
            return ctrl.layers.Any(l => l.stateMachine == stateMachine);
        }

        public int GetLayerId(AnimatorStateMachine stateMachine) {
            var id = ctrl.layers
                .Select((l, i) => (l, i))
                .Where(tuple => tuple.Item1.stateMachine == stateMachine)
                .Select(tuple => tuple.Item2)
                .DefaultIfEmpty(-1)
                .First();
            if (id == -1) {
                throw new Exception("Layer not found in controller. It may have been accessed after it was removed.");
            }
            return id;
        }

        [CanBeNull]
        public VFLayer GetLayer(int index) {
            var layers = ctrl.layers;
            if (index < 0 || index >= layers.Length) return null;
            return new VFLayer(this, layers[index].stateMachine);
        }

        public VFLayer GetLayer(AnimatorStateMachine stateMachine) {
            return GetLayer(GetLayerId(stateMachine));
        }

        public AnimatorControllerLayer[] layers {
            get => ctrl.layers;
            set => ctrl.layers = value;
        }

        public AnimatorControllerParameter[] parameters {
            get => ctrl.parameters;
            set => ctrl.parameters = value;
        }

        [CanBeNull]
        public static VFController CopyAndLoadController(RuntimeAnimatorController ctrl, VRCAvatarDescriptor.AnimLayerType type) {
            if (ctrl == null) {
                return null;
            }

            // Make a copy of everything
            ctrl = ctrl.Clone(addPrefix: $"Copied from {ctrl.name}/");

            // Collect any override controllers wrapping the main controller
            var overrides = new List<AnimatorOverrideController>();
            while (ctrl is AnimatorOverrideController ov) {
                overrides.Add(ov);
                ctrl = ov.runtimeAnimatorController;
            }

            // Bail if we hit a dead end
            if (!(ctrl is AnimatorController ac)) {
                return null;
            }
            
            var output = new VFController(ac);
            output.RemoveInvalidParameters();
            output.FixNullStateMachines();
            output.CheckForBadBehaviours();
            output.ReplaceSyncedLayers();
            output.RemoveDuplicateStateMachines();

            // Apply override controllers
            if (overrides.Count > 0) {
                AnimatorIterator.ReplaceClips(ac, clip => {
                    return overrides
                        .Select(ov => ov[clip])
                        .Where(overrideClip => overrideClip != null)
                        .DefaultIfEmpty(clip)
                        .First();
                });
            }

            // Make sure all masks are unique, so we don't modify one and affect another
            foreach (var layer in output.GetLayers()) {
                if (layer.mask != null) {
                    layer.mask = layer.mask.Clone();
                }
            }
            
            output.FixLayer0Weight();
            output.ApplyBaseMask(type);
            NoBadControllerParamsService.RemoveWrongParamTypes(output);
            return output;
        }

        /**
         * Some people have corrupt controller layers containing no state machine.
         * The simplest fix for this is for us to just stuff an empty state machine into it.
         * We can't just delete it because it would interfere with the layer index numbers.
         */
        private void FixNullStateMachines() {
            ctrl.layers = ctrl.layers.Select(layer => {
                if (layer.stateMachine == null) {
                    var sm = VrcfObjectFactory.Create<AnimatorStateMachine>();
                    sm.name = layer.name;
                    layer.stateMachine = sm;
                }
                return layer;
            }).ToArray();
        }

        /**
         * Layer 0 is always treated as fully weighted by unity, regardless of its actually setting.
         * This can do weird things when we move that layer around, so we always ensure that the
         * setting of the base layer is properly set to 1.
         */
        private void FixLayer0Weight() {
            var layer0 = GetLayer(0);
            if (layer0 == null) return;
            layer0.weight = 1;
        }

        /**
         * VRCF's handles masks by "applying" the base mask to every mask in the controller. This makes things like
         * merging controllers and features much easier. Later on, we recalculate a new base mask in FixMasksBuilder. 
         */
        private void ApplyBaseMask(VRCAvatarDescriptor.AnimLayerType type) {
            var layer0 = GetLayer(0);
            if (layer0 == null) return;

            var baseMask = layer0.mask;
            if (type == VRCAvatarDescriptor.AnimLayerType.FX) {
                if (baseMask == null) {
                    baseMask = AvatarMaskExtensions.DefaultFxMask();
                } else {
                    baseMask = baseMask.Clone();
                }
            } else if (type == VRCAvatarDescriptor.AnimLayerType.Gesture) {
                if (baseMask == null) {
                    // Technically, we should throw here. The VRCSDK will complain and prevent the user from uploading
                    // until they fix this. But we fix it here for them temporarily so they can use play mode for now.
                    // Gesture controllers merged using Full Controller with no base mask will slip through and be allowed
                    // by this.
                    baseMask = AvatarMaskExtensions.Empty();
                    baseMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
                    baseMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
                } else {
                    baseMask = baseMask.Clone();
                    // If the base mask is just one hand, assume that they put in controller with just a left and right hand layer,
                    // and meant to have both in the base mask.
                    if (baseMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers))
                        baseMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
                    if (baseMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers))
                        baseMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
                }
            } else {
                // VRChat does not use the base mask on any other controller types
                return;
            }

            // Because of some unity bug, ONLY the muscle part of the base mask is actually applied to the child layers
            // The transform part of the base mask DOES NOT impact lower layers!!
            baseMask.AllowAllTransforms();

            foreach (var layer in GetLayers()) {
                if (layer.mask == null) {
                    layer.mask = baseMask.Clone();
                } else {
                    layer.mask.IntersectWith(baseMask);
                }
            }
        }

        private static void RemoveBadBehaviours(string location, object obj) {
            var field = obj.GetType()
                .GetProperty("behaviours_Internal", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) {
                // 2022+
                var raw = field.GetValue(obj) as ScriptableObject[];
                if (raw == null) return;
                var clean = raw.OfType<StateMachineBehaviour>().Cast<ScriptableObject>().ToArray();
                if (raw.Length != clean.Length) {
                    field.SetValue(obj, clean);
                    Debug.LogWarning($"{location} contained a corrupt behaviour. It has been removed.");
                }
            } else {
                // 2019
                var oldField = obj.GetType().GetProperty("behaviours", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (oldField == null) return;
                var raw = oldField.GetValue(obj) as StateMachineBehaviour[];
                if (raw == null) return;
                var clean = raw.Cast<object>().OfType<StateMachineBehaviour>().ToArray();
                if (raw.Length != clean.Length) {
                    oldField.SetValue(obj, clean);
                    Debug.LogWarning($"{location} contained a corrupt behaviour. It has been removed.");
                }
            }
        }

        private void CheckForBadBehaviours() {
            foreach (var layer in GetLayers()) {
                foreach (var stateMachine in AnimatorIterator.GetAllStateMachines(layer)) {
                    RemoveBadBehaviours($"{layer.debugName} StateMachine `{stateMachine.name}`", stateMachine);
                }

                foreach (var state in new AnimatorIterator.States().From(layer)) {
                    RemoveBadBehaviours($"{layer.debugName} State `{state.name}`", state);
                }
            }
        }

        /**
         * Synced layers are... "very" broken in unity and in vrchat. In unity, if you delete a layer higher than the synced
         * layer, the synced layer is randomly deleted. In vrchat, synced layers don't really work properly at all.
         * When dealing with write defaults issues, synced layers break everything because the motions in the synced layer may need
         * a different WD setting than the states in the other layer.
         * We can "sorta" work around this issue by just replacing the synced layer with an identical copy. This means that timing sync won't work,
         * but it's better than nothing.
         */
        private void ReplaceSyncedLayers() {
            layers = layers.Select((layer, id) => {
                if (layer.syncedLayerIndex < 0 || layer.syncedLayerIndex == id) {
                    layer.syncedLayerIndex = -1;
                    return layer;
                }
                if (layer.syncedLayerIndex >= layers.Length) {
                    var sm = VrcfObjectFactory.Create<AnimatorStateMachine>();
                    sm.name = layer.name;
                    layer.stateMachine = sm;
                    layer.syncedLayerIndex = -1;
                    return layer;
                }

                var copy = layers[layer.syncedLayerIndex].stateMachine.Clone();
                layer.syncedLayerIndex = -1;
                layer.stateMachine = copy;
                foreach (var state in new AnimatorIterator.States().From(new VFLayer(new VFController(ctrl), layer.stateMachine))) {
                    var originalState = state.GetCloneSource();
                    state.motion = layer.GetOverrideMotion(originalState);
                    state.behaviours = layer.GetOverrideBehaviours(originalState);
                    layer.SetOverrideMotion(originalState, null);
                    layer.SetOverrideBehaviours(originalState, Array.Empty<StateMachineBehaviour>());
                }

                return layer;
            }).ToArray();

        }

        /**
         * Some systems (modular avatar) can improperly add multiple layers with the same state machine.
         * This wrecks havoc, as making changes to one of the layers can impact both, while typically there is
         * expected to be no cross-talk. Since there's basically no legitimate reason for the same state machine
         * to be used more than once in the same controller, we can just nuke the copies.
         */
        private void RemoveDuplicateStateMachines() {
            var seenStateMachines = new HashSet<AnimatorStateMachine>();
            layers = layers.Select(layer => {
                if (layer.stateMachine != null) {
                    if (seenStateMachines.Contains(layer.stateMachine)) {
                        return null;
                    }
                    seenStateMachines.Add(layer.stateMachine);
                }
                return layer;
            }).NotNull().ToArray();
        }
        
        /**
         * Some tools add parameters with an invalid type (not bool, trigger, float, int, etc)
         * This causes the VRCSDK to blow up and break the mirror clone and throw exceptions in console.
         * https://feedback.vrchat.com/bug-reports/p/invalid-parameter-type-within-a-controller-breaks-mirror-clone-and-spams-output
         */
        private void RemoveInvalidParameters() {
            ctrl.parameters = ctrl.parameters.Where(p => VRCFEnumUtils.IsValid(p.type)).ToArray();
        }

        public void RewriteParameters(Func<string, string> rewriteParamNameNullUnsafe, bool includeWrites = true, ICollection<AnimatorStateMachine> limitToLayers = null) {
            string RewriteParamName(string str) {
                if (string.IsNullOrEmpty(str)) return str;
                return rewriteParamNameNullUnsafe(str);
            }
            var affectsLayers = GetLayers()
                .Where(l => limitToLayers == null || limitToLayers.Contains(l.stateMachine))
                .ToArray();
            
            // Params
            if (includeWrites && limitToLayers == null) {
                var prms = ctrl.parameters;
                foreach (var p in prms) {
                    p.name = RewriteParamName(p.name);
                }

                ctrl.parameters = prms;
                VRCFuryEditorUtils.MarkDirty(ctrl);
            }

            // States
            foreach (var state in new AnimatorIterator.States().From(affectsLayers)) {
                if (state.speedParameterActive) {
                    state.speedParameter = RewriteParamName(state.speedParameter);
                }
                if (state.cycleOffsetParameterActive) {
                    state.cycleOffsetParameter = RewriteParamName(state.cycleOffsetParameter);
                }
                if (state.mirrorParameterActive) {
                    state.mirrorParameter = RewriteParamName(state.mirrorParameter);
                }
                if (state.timeParameterActive) {
                    state.timeParameter = RewriteParamName(state.timeParameter);
                }
                VRCFuryEditorUtils.MarkDirty(state);
            }
            
            foreach (var b in new AnimatorIterator.Behaviours().From(affectsLayers)) {
                // VRCAvatarParameterDriver
                if (includeWrites && b is VRCAvatarParameterDriver oldB) {
                    foreach (var p in oldB.parameters) {
                        p.name = RewriteParamName(p.name);
#if VRCSDK_HAS_DRIVER_COPY
                        p.source = RewriteParamName(p.source);
#endif
                    }
                    VRCFuryEditorUtils.MarkDirty(b);
                }

                // VRCAnimatorPlayAudio
#if VRCSDK_HAS_ANIMATOR_PLAY_AUDIO
                if (b is VRCAnimatorPlayAudio audio) {
                    audio.ParameterName = RewriteParamName(audio.ParameterName);
                }
#endif
            }

            // Parameter Animations
            if (includeWrites) {
                foreach (var clip in new AnimatorIterator.Clips().From(affectsLayers)) {
                    clip.Rewrite(AnimationRewriter.RewriteBinding(binding => {
                        if (binding.GetPropType() != EditorCurveBindingType.Aap) return binding;
                        binding.propertyName = RewriteParamName(binding.propertyName);
                        return binding;
                    }));
                }
            }

            // Blend trees
            foreach (var tree in new AnimatorIterator.Trees().From(affectsLayers)) {
                tree.RewriteParameters(RewriteParamName);
            }

            // Transitions
            foreach (var layer in affectsLayers) {
                AnimatorIterator.RewriteConditions(layer, cond => {
                    cond.parameter = RewriteParamName(cond.parameter);
                    return cond;
                });
            }
        }
    }
}
