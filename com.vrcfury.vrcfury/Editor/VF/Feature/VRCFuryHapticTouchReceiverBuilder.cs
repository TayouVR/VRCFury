using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Service;

namespace VF.Feature {
    [VFService]
    internal class VRCFuryHapticTouchReceiverBuilder {
        [VFAutowired] private readonly HapticContactsService hapticContacts;
        [VFAutowired] private readonly UniqueHapticNamesService uniqueHapticNamesService;
        [VFAutowired] private readonly VFGameObject avatarObject;

        [FeatureBuilderAction]
        public void Apply() {
            foreach (var receiver in avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticTouchReceiver>()) {
                var name = receiver.name;
                if (string.IsNullOrWhiteSpace(name)) {
                    name = HapticUtils.GetName(receiver.owner());
                }
                name = uniqueHapticNamesService.GetUniqueName(name);
                var paramPrefix = "VFH/Zone/Touch/" + name.Replace('/','_');
                
                hapticContacts.AddReceiver(new HapticContactsService.ReceiverRequest() {
                    obj = receiver.owner(),
                    paramName = paramPrefix + "/Self",
                    objName = "Self",
                    radius = receiver.radius,
                    tags = HapticUtils.SelfContacts.Concat(new [] { HapticUtils.CONTACT_PEN_CLOSE }).ToArray(),
                    party = HapticUtils.ReceiverParty.Self,
                    worldScale = false,
                    usePrefix = false,
                    localOnly = true
                });
                hapticContacts.AddReceiver(new HapticContactsService.ReceiverRequest() {
                    obj = receiver.owner(),
                    paramName = paramPrefix + "/Others",
                    objName = "Others",
                    radius = receiver.radius,
                    tags = HapticUtils.BodyContacts.Concat(new [] { HapticUtils.CONTACT_PEN_CLOSE }).ToArray(),
                    party = HapticUtils.ReceiverParty.Others,
                    worldScale = false,
                    usePrefix = false,
                    localOnly = true
                });
            }
        }
        
        [CustomEditor(typeof(VRCFuryHapticTouchReceiver), true)]
        public class VRCFuryHapticTouchReceiverEditor : VRCFuryComponentEditor<VRCFuryHapticTouchReceiver> {
            protected override VisualElement CreateEditor(SerializedObject serializedObject, VRCFuryHapticTouchReceiver target) {
                var container = new VisualElement();
                
                container.Add(VRCFuryEditorUtils.Info(
                    "This will add an extra VRCFury Haptic Touch Zone (for purposes unrelated to Plugs / Sockets), which will activate OGB haptics when touched. " +
                    "Haptic level will increase to 100% at the center of the sphere. " +
                    "This touch zone can be activated by Hands, Fingers, Feet, SPS Plugs, VRCF Haptic Touch Senders, and Heads (other players only)."));

                container.Add(VRCFuryHapticPlugEditor.ConstraintWarning(target));
            
                container.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("name"), "Name in connected apps"));
                container.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("radius"), "Radius"));

                return container;
            }
        
            [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.InSelectionHierarchy)]
            static void DrawGizmo(VRCFuryHapticTouchReceiver c, GizmoType gizmoType) {
                var worldPos = c.owner().worldPosition;
                var worldScale = c.owner().worldScale.x;
                VRCFuryGizmoUtils.DrawSphere(worldPos, worldScale * c.radius, Color.red);
                VRCFuryGizmoUtils.DrawText(worldPos, "Touch Receiver", Color.white, true);
            }
        }
    }
}
