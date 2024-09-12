using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {

[FeatureTitle("Avatar Scale")]
[FeatureRootOnly]
[FeatureHideInMenu]
internal class AvatarScaleBuilder : FeatureBuilder<AvatarScale2> {
    [FeatureBuilderAction]
    public void Apply() {
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        return VRCFuryEditorUtils.Error(
            "This Avatar Scale feature is no longer available as scaling is now built into VRChat."
            );
    }
}

}
