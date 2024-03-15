using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Injector;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Service {

    [VFService]
    public class ClipBuilderService {
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly AvatarBindingStateService bindingStateService;
        private VFGameObject baseObject => globals.avatarObject;

        public AnimationClip MergeSingleFrameClips(params (float, AnimationClip)[] sources) {
            var output = new AnimationClip();
            foreach (var binding in sources.SelectMany(tuple => tuple.Item2.GetFloatBindings()).Distinct()) {
                var exists = bindingStateService.GetFloat(binding, out var defaultValue);
                if (!exists && binding.path == "" && binding.type == typeof(Animator)) {
                    exists = true;
                    defaultValue = 0;
                }
                if (!exists) continue;
                var outputCurve = new AnimationCurve();
                foreach (var (time,sourceClip) in sources) {
                    var sourceCurve = sourceClip.GetFloatCurve(binding);
                    if (sourceCurve != null && sourceCurve.keys.Length >= 1) {
                        outputCurve.AddKey(new Keyframe(time, sourceCurve.keys[0].value, 0f, 0f));
                    } else {
                        outputCurve.AddKey(new Keyframe(time, defaultValue, 0f, 0f));
                    }
                }
                output.SetFloatCurve(binding, outputCurve);
            }
            foreach (var binding in sources.SelectMany(tuple => tuple.Item2.GetObjectBindings()).Distinct()) {
                var exists = bindingStateService.GetObject(binding, out var defaultValue);
                if (!exists) continue;
                var outputCurve = new List<ObjectReferenceKeyframe>();
                foreach (var (time,sourceClip) in sources) {
                    var sourceCurve = sourceClip.GetObjectCurve(binding);
                    if (sourceCurve != null && sourceCurve.Length >= 1) {
                        outputCurve.Add(new ObjectReferenceKeyframe { time = time, value = sourceCurve[0].value });
                    } else {
                        outputCurve.Add(new ObjectReferenceKeyframe { time = time, value = defaultValue });
                    }
                }
                output.SetObjectCurve(binding, outputCurve.ToArray());
            }
            return output;
        }

        public void Enable(AnimationClip clip, VFGameObject obj, bool active = true) {
            var path = GetPath(obj);
            var binding = EditorCurveBinding.FloatCurve(path, typeof(GameObject), "m_IsActive");
            clip.SetCurve(binding, active ? 1 : 0);
        }

        public void Enable(AnimationClip clip, IConstraint constraint, bool active = true) {
            var component = constraint as UnityEngine.Component;
            var path = component.owner().GetPath(baseObject);
            var binding = EditorCurveBinding.FloatCurve(path, constraint.GetType(), "m_Active");
            clip.SetCurve(binding, active ? 1 : 0);
        }
        
        public void Scale(AnimationClip clip, VFGameObject obj, Vector3 scale) {
            var path = GetPath(obj);
            var binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), "");

            binding.propertyName = "m_LocalScale.x";
            clip.SetCurve(binding, scale.x);
            binding.propertyName = "m_LocalScale.y";
            clip.SetCurve(binding, scale.y);
            binding.propertyName = "m_LocalScale.z";
            clip.SetCurve(binding, scale.z);
        }

        public string GetPath(VFGameObject gameObject) {
            return gameObject.GetPath(baseObject);
        }

        public static Tuple<AnimationClip, AnimationClip> SplitRangeClip(Motion motion) {
            if (!(motion is AnimationClip clip)) return null;
            var times = new HashSet<float>();
            foreach (var (binding,curve) in clip.GetAllCurves()) {
                if (curve.IsFloat) {
                    times.UnionWith(curve.FloatCurve.keys.Select(key => key.time));
                } else {
                    times.UnionWith(curve.ObjectCurve.Select(key => key.time));
                }
            }

            if (!times.Contains(0)) return null;
            if (times.Count > 2) return null;

            var startClip = new AnimationClip();
            var endClip = new AnimationClip();
            
            foreach (var (binding,curve) in clip.GetAllCurves()) {
                if (curve.IsFloat) {
                    var first = true;
                    foreach (var key in curve.FloatCurve.keys) {
                        if (first) {
                            startClip.SetCurve(binding, key.value);
                            first = false;
                        }
                        endClip.SetCurve(binding, key.value);
                    }
                } else {
                    var first = true;
                    foreach (var key in curve.ObjectCurve) {
                        if (first) {
                            startClip.SetCurve(binding, key.value);
                            first = false;
                        }
                        endClip.SetCurve(binding, key.value);
                    }
                }
            }

            return Tuple.Create(startClip, endClip);
        }

    }

}
