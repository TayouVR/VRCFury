using nadena.dev.ndmf;
using nadena.dev.ndmf.fluent;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VRC.SDK3.Avatars.Components;

[assembly: ExportsPlugin(
    typeof(VF.NDMF.VRCFuryPlugin)
)]

namespace VF.NDMF {
#if USE_NDMF
    public class VRCFuryPlugin : Plugin<VRCFuryPlugin> {
        
        public override string QualifiedName => "com.vrcfury.vrcfury";

        public override string DisplayName => "VRCFury";
        
        protected override void Configure() {
            
            // The proper way would be to put VRCFury features in the groups here and let them run alongside other plugins.
            // VRCFury is just going to run after Modular Avatar as a whole, in which phase doesn't really matter as it is going to run there as the whole thing.
            
            Sequence sequence = InPhase(BuildPhase.Resolving).BeforePlugin("nadena.dev.modular-avatar");
            sequence.Run("VRCFury", ctx => Postprocess(ctx));
            
            sequence = InPhase(BuildPhase.Resolving);
            // Do Resolving Operations here
            
            // CleanupLegacy,
            // 
            // // Needs to happen before everything
            // FixDoubleFx,
            // 
            // // Needs to happen before ForceObjectState
            // FullControllerToggle,
            // 
            // // Needs to happen before anything starts using the Animator
            // ResetAnimatorBefore,
            // 
            // // Needs to happen before toggles begin getting processed
            // ForceObjectState,
            // RemoveEditorOnly,
            // 
            // // Needs to be the first thing to instantiate the ControllerManagers
            // AnimatorLayerControlRecordBase,
            
            sequence = InPhase(BuildPhase.Generating);
            // Do Generating Operations here
            
            sequence = InPhase(BuildPhase.Transforming);
            // Do Transforming Operations here
            
            // // Needs to happen before any objects are moved, so otherwise the imported
            // // animations would not be adjusted to point to the new moved object paths
            // FullController,
            //
            // UpgradeLegacyHaptics,
            //
            // // Needs to run after all haptic components are in place
            // // Needs to run before Toggles, because of its "After Bake" action
            // BakeHapticPlugs,
            // BakeHapticSockets,
            // BakeHapticVersions,
            //
            // ApplyRestState1,
            //
            // Default,
            //
            // // Needs to happen after AdvancedVisemes so that gestures affecting the jaw override visemes
            // SenkyGestureDriver,
            //
            // // Needs to happen after builders have scanned their prop children objects for any purpose (since this action
            // // may move objects out of the props and onto the avatar base). One example is the FullController which
            // // scans the prop children for contact receivers.
            // ArmatureLinkBuilder,
            //
            // // Needs to run after all possible toggles have been created and applied
            // CollectToggleExclusiveTags,
            //
            // // Needs to run after any builders have added their "disable blinking" models (gesture builders mostly)
            // Blinking,
            //
            // // Needs to happen after any new skinned meshes have been added
            // BoundingBoxFix,
            // AnchorOverrideFix,
            //
            // // Needs to run before ObjectMoveBuilderFixAnimations, but after anything that needs
            // // an object moved onto the fake head bone
            // FakeHeadBuilder,
            //
            // // Needs to happen after toggles
            // HapticsAnimationRewrites,
            //
            // // Needs to run after all TPS materials are done
            // // Needs to run after toggles are in place
            // // Needs to run after HapticsAnimationRewrites
            // TpsScaleFix,
            // DpsTipScaleFix,
            //
            // FixTouchingContacts,
            
            sequence = InPhase(BuildPhase.Optimizing);
            // Do Optimizations here
            
            // // Needs to run after everything else is done messing with rest state
            // ApplyRestState2,
            // ApplyToggleRestingState,
            // ApplyRestState3,
            //
            // // Finalize Controllers
            // BlendShapeLinkFixAnimations, // Needs to run after most things are done messing with animations, since it'll make copies of the blendshape curves
            // DirectTreeOptimizer, // Needs to run after animations are done, but before RecordDefaults
            // RecordAllDefaults,
            // BlendshapeOptimizer, // Needs to run after RecordDefaults
            // Slot4Fix,
            // CleanupEmptyLayers,
            // PullMusclesOutOfFx,
            // RemoveDefaultedAdditiveLayer,
            // FixUnsetPlayableLayers,
            // FixMasks,
            // FixMaterialSwapWithMask,
            // ControllerConflictCheck,
            // AdjustWriteDefaults,
            // FixEmptyMotions,
            // AnimatorLayerControlFix,
            // RemoveNonQuestMaterials,
            // RemoveBadControllerTransitions,
            // FinalizeController,
            //
            // // Finalize Menus
            // MoveSpsMenus,
            // MoveMenuItems,
            // FinalizeMenu,
            //
            // // Finalize Parameters
            // FixBadParameters,
            // FinalizeParams,
            //
            // MarkThingsAsDirtyJustInCase,
            //
            // RemoveJunkAnimators,
            //
            // // Needs to be at the very end, because it places immutable clips into the avatar
            // RestoreProxyClips,
            // // Needs to happen after everything is done using the animator
            // ResetAnimatorAfter,
        }

        private bool Postprocess(BuildContext ctx) {
            VFGameObject vrcCloneObject = ctx.AvatarRootObject;

            if (!VRCFuryBuilder.ShouldRun(vrcCloneObject)) {
                return true;
            }
            
            if (EditorApplication.isPlaying) {
                EditorUtility.DisplayDialog(
                    "VRCFury",
                    "Something is causing VRCFury to build while play mode is still initializing. This may cause unity to crash!!\n\n" +
                    "If you use Av3Emulator, consider using Gesture Manager instead, or uncheck 'Run Preprocess Avatar Hook' on the Av3 Emulator Control object.",
                    "Ok"
                );
            }

            // When vrchat is uploading our avatar, we are actually operating on a clone of the avatar object.
            // Let's get a reference to the original avatar, so we can apply our changes to it as well.
            var cloneObjectName = vrcCloneObject.name;

            if (!cloneObjectName.EndsWith("(Clone)")) {
                Debug.LogError("Seems that we're not operating on a vrc avatar clone? Bailing. Please report this to VRCFury.");
                return false;
            }

            // Clean up junk from the original avatar, in case it still has junk from way back when we used to
            // dirty the original
            GameObject original = null;
            {
                foreach (var desc in Object.FindObjectsOfType<VRCAvatarDescriptor>()) {
                    if (desc.owner().name + "(Clone)" == cloneObjectName && desc.gameObject.activeInHierarchy) {
                        original = desc.gameObject;
                        break;
                    }
                }
            }

            var builder = new VRCFuryBuilder();
            var vrcFurySuccess = builder.SafeRun(vrcCloneObject, original);
            if (!vrcFurySuccess) return false;

            // Make absolutely positively certain that we've removed every non-standard component from the avatar
            // before it gets uploaded
            VRCFuryBuilder.StripAllVrcfComponents(vrcCloneObject);

            return true;
        }
    }
#endif
}
