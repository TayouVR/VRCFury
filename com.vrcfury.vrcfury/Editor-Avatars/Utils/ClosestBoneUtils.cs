using System.Collections.Generic;
using System.Linq;
#if MA_EXISTS 
using nadena.dev.modular_avatar.core; 
#endif 
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Hooks;
using VF.Injector;
using VF.Model;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Utils {
    [VFService]
    internal class ClosestBoneUtils {
        private static readonly Dictionary<VFGameObject, ClosestBoneUtils> perFrame
            = new Dictionary<VFGameObject, ClosestBoneUtils>();

        private readonly VRCFObjectPathCache objectPaths;
        private readonly VRCFArmatureCache armatureCache;
        private readonly Dictionary<VFGameObject, HumanBodyBones?> results = new();
        private readonly Dictionary<VFGameObject, List<ArmatureLink>> armatureLinks = new();
#if MA_EXISTS 
        private readonly Dictionary<VFGameObject, List<ModularAvatarBoneProxy>> maBoneProxies = new();
#endif 

        [VFAutowired]
        public ClosestBoneUtils(VRCFObjectPathCache objectPaths, VRCFArmatureCache armatureCache) {
            this.objectPaths = objectPaths;
            this.armatureCache = armatureCache;
        }

        public static ClosestBoneUtils GetPerFrame(VFGameObject avatarObject) {
            return perFrame.GetOrCreate(
                avatarObject,
                () => new ClosestBoneUtils(
                    VRCFObjectPathCache.GetPerFrame(avatarObject),
                    VRCFArmatureCache.GetPerFrame(avatarObject)
                )
            );
        }

        [VFInit]
        private static void Init() {
            Scheduler.Schedule(perFrame.Clear, 0);
        }

        private List<ArmatureLink> GetArmatureLinks(VFGameObject rootObject) {
            if (armatureLinks.TryGetValue(rootObject, out var cached)) return cached;
            return armatureLinks[rootObject] = rootObject
                .GetComponentsInSelfAndChildren<VRCFury>()
                .SelectMany(v => v.GetAllFeatures())
                .OfType<ArmatureLink>()
                .ToList();
        }
 
#if MA_EXISTS 
        private List<ModularAvatarBoneProxy> GetMaBoneProxies(VFGameObject rootObject) { 
            if (maBoneProxies.TryGetValue(rootObject, out var cached)) return cached; 
            return maBoneProxies[rootObject] = rootObject 
                .GetComponentsInSelfAndChildren<ModularAvatarBoneProxy>() 
                .ToList(); 
        } 
 
        public static VFGameObject GetProbableBoneProxyParent( 
            ModularAvatarBoneProxy boneProxy, 
            VFGameObject avatarObject, 
            VFGameObject obj, 
            VRCFObjectPathCache objectPaths, 
            VRCFArmatureCache armatureCache 
        ) { 
            try { 
                var linkFrom = boneProxy.gameObject; 
                if (linkFrom == null || !obj.IsSameOrChildOf(linkFrom)) return null; 
                return boneProxy.target.gameObject; 
            } catch (System.Exception) { 
                return null; 
            } 
        } 
#endif 

        public HumanBodyBones? GetClosestHumanoidBone(VFGameObject obj) {
            return results.GetOrCreate(obj, () => GetClosestHumanoidBoneUncached(obj));
        }

        [CanBeNull]
        public VFGameObject GetBone(VFGameObject obj, HumanBodyBones bone) {
            return armatureCache.FindBoneOnArmatureOrNull(bone);
        }

        private HumanBodyBones? GetClosestHumanoidBoneUncached(VFGameObject obj) {
            var avatarObject = obj.GetAvatarRoot();

            var followConstraints = true;
            var followArmatureLink = true;
            
#if MA_EXISTS 
            var followMaBoneProxy = true; 
            var maBoneProxies = GetMaBoneProxies(avatarObject); 
#endif 

            var armatureLinks = GetArmatureLinks(avatarObject);

            var humanoidBones = armatureCache.GetAllBones()
                .ToDictionary(x => x.Value, x => x.Key);
            var alreadyChecked = new HashSet<VFGameObject>();
            var current = obj;
            while (current != null) {
                if (humanoidBones.TryGetValue(current, out var bone))
                    return bone;

                alreadyChecked.Add(current);

                if (followArmatureLink) {
                    VFGameObject foundParent = null;
                    foreach (var armatureLink in armatureLinks) {
                        var p = ArmatureLinkService.GetProbableParent(armatureLink, avatarObject, current, objectPaths, armatureCache);
                        if (p != null && !alreadyChecked.Contains(p)) {
                            foundParent = p;
                            break;
                        }
                    }

                    if (foundParent != null) {
                        current = foundParent;
                        continue;
                    }
                }
 
#if MA_EXISTS 
                if (followMaBoneProxy) { 
                    VFGameObject foundParent = null; 
                    foreach (var boneProxy in maBoneProxies) { 
                        var p = GetProbableBoneProxyParent(boneProxy, avatarObject, current, objectPaths, armatureCache); 
                        if (p != null && !alreadyChecked.Contains(p)) { 
                            foundParent = p; 
                            break; 
                        } 
                    } 
 
                    if (foundParent != null) { 
                        current = foundParent; 
                        continue; 
                    } 
                } 
#endif 
                
                if (followConstraints) {
                    var positionTo = current.GetConstraints()
                        .Where(c => c.IsParent() || c.IsPosition())
                        .Select(c => c.GetFirstSource())
                        .NotNull()
                        .FirstOrDefault();
                    if (positionTo != null && !alreadyChecked.Contains(positionTo)) {
                        current = positionTo;
                        continue;
                    }
                }
                current = current.parent;
            }
            return null;
        }
    }

    [VFService]
    internal class SpsAvatarAutoTagGenerator : SpsAutoTagGenerator {
        [VFAutowired] private readonly ClosestBoneUtils closestBoneUtils;
        [VFAutowired] [CanBeNull] private readonly VRCAvatarDescriptor avatar;

        public HumanBodyBones? GetClosestBone(VFGameObject obj) {
            return closestBoneUtils.GetClosestHumanoidBone(obj);
        }

        [CanBeNull]
        public VFGameObject GetBone(VFGameObject obj, HumanBodyBones bone) {
            return closestBoneUtils.GetBone(obj, bone);
        }

        public Vector3? GetAvatarViewPosition(VFGameObject obj) {
            return (avatar ?? obj.GetAvatarRoot().GetComponent<VRCAvatarDescriptor>())?.ViewPosition;
        }
    }
}
