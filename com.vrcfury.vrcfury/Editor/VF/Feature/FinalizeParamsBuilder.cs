using System;
using System.Linq;
using UnityEngine;
using VF.Feature.Base;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Feature {
    public class FinalizeParamsBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.FinalizeParams)]
        public void Apply() {
            var p = manager.GetParams();
            var maxParams = VRCExpressionParameters.MAX_PARAMETER_COST;
            if (maxParams > 9999) {
                // Some versions of the VRChat SDK have a broken value for this
                maxParams = 256;
            }
            int totalParamCost = p.GetRaw().CalcTotalCost();
            Debug.Log($"Parameters in avatar {manager.AvatarObject.name} ({totalParamCost}/{maxParams}):\n {string.Join("\n", p.GetRaw().parameters.Select(x => $"[{x.valueType}] {x.name}"))}");
            if (totalParamCost > maxParams) {
                throw new Exception(
                    "Avatar is out of space for parameters! Used "
                    + totalParamCost + "/" + maxParams
                    + ". Delete some params from your avatar's param file, or disable some VRCFury features.");
            }

            var contacts = avatarObject.GetComponentsInSelfAndChildren<VRCContactReceiver>().Length;
            contacts += avatarObject.GetComponentsInSelfAndChildren<VRCContactSender>().Length;
            if (contacts > 256) {
                throw new Exception(
                    "Avatar is over allowed contact limit! Used "
                    + contacts + "/256"
                    + ". Delete some contacts from your avatar, or remove some VRCFury haptics.");
            }
        }
    }
}
