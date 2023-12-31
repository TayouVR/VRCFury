using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Component;
using VF.Model;
using VF.Utils;

namespace VF.Inspector {
    public class VRCFuryComponentEditor<T> : UnityEditor.Editor where T : VRCFuryComponent {

        /*
        public override bool UseDefaultMargins() {
            return false;
        }
        
        private Action detachChanges = null;

        private void Detach() {
            if (detachChanges != null) detachChanges();
            detachChanges = null;
        }

        private void CreateHeaderOverlay(VisualElement el) {
            Detach();
            
            var inspectorRoot = el.parent?.parent;
            if (inspectorRoot == null) return;

            var header = inspectorRoot.Children().ToList()
                .FirstOrDefault(c => c.name.EndsWith("Header"));
            var footer = inspectorRoot.Children().ToList()
                .FirstOrDefault(c => c.name.EndsWith("Footer"));
            if (header != null) header.style.display = DisplayStyle.None;
            if (footer != null) footer.style.display = DisplayStyle.None;

            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    height = 21,
                    borderTopWidth = 1,
                    borderTopColor = Color.black,
                    backgroundColor = new Color(0.3f, 0.3f, 0.3f),
                }
            };
            VRCFuryEditorUtils.HoverHighlight(row);
            var normalLabelColor = new Color(0.05f, 0.05f, 0.05f);
            var label = new Label("VRCF") {
                style = {
                    color = new Color(0.8f, 0.4f, 0f),
                    borderTopRightRadius = 0,
                    borderBottomRightRadius = 0,
                    paddingLeft = 10,
                    paddingRight = 7,
                    backgroundColor = normalLabelColor,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    flexShrink = 1,
                }
            };
            row.Add(label);

            row.Add(new VisualElement {
                style = {
                    borderLeftColor = normalLabelColor,
                    borderTopColor = normalLabelColor,
                    borderLeftWidth = 5,
                    borderTopWidth = 10,
                    borderRightWidth = 5,
                    borderBottomWidth = 10,
                }
            });

            var name = new Label("Toggle") {
                style = {
                    //color = Color.white,
                    flexGrow = 1,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    paddingLeft = 5
                }
            };
            row.Add(name);
            void ContextMenu(Vector2 pos) {
                var displayMethod = typeof(EditorUtility)
                    .GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                    .Where(method => method.Name == "DisplayObjectContextMenu")
                    .Where(method => method.GetParameters()[1].ParameterType.IsArray)
                    .First();
                displayMethod?.Invoke(null, new object[] {
                    new Rect(pos.x, pos.y, 0.0f, 0.0f),
                    targets,
                    0
                });
            }
            row.RegisterCallback<MouseUpEvent>(e => {
                if (e.button == 0) {
                    Debug.Log("Toggle " + target);
                    e.StopImmediatePropagation();
                    InternalEditorUtility.SetIsInspectorExpanded(target, !InternalEditorUtility.GetIsInspectorExpanded(target));
                }
                if (e.button == 1) {
                    e.StopImmediatePropagation();
                    ContextMenu(e.mousePosition);
                }
            });
            {
                var up = new Label("↑") {
                    style = {
                        width = 16,
                        height = 16,
                        marginTop = 2,
                        unityTextAlign = TextAnchor.MiddleCenter,
                    }
                };
                up.RegisterCallback<MouseUpEvent>(e => {
                    if (e.button != 0) return;
                    e.StopImmediatePropagation();
                    ComponentUtility.MoveComponentUp(target as Component);
                });
                row.Add(up);
                VRCFuryEditorUtils.HoverHighlight(up);
            }
            {
                var menu = new Image {
                    image = EditorGUIUtility.FindTexture("_Menu@2x"),
                    scaleMode = ScaleMode.StretchToFill,
                    style = {
                        width = 16,
                        height = 16,
                        marginTop = 2,
                        marginRight = 4,
                    }
                };
                menu.RegisterCallback<MouseUpEvent>(e => {
                    if (e.button != 0) return;
                    e.StopImmediatePropagation();
                    ContextMenu(e.mousePosition);
                });
                row.Add(menu);
                VRCFuryEditorUtils.HoverHighlight(menu);
            }

            detachChanges = () => {
                if (header != null) header.style.display = DisplayStyle.Flex;
                if (footer != null) footer.style.display = DisplayStyle.Flex;
                row.parent.Remove(row);
            };

            inspectorRoot.Insert(0, row);
        }
        */

        private GameObject dummyObject;

        public sealed override VisualElement CreateInspectorGUI() {
            VisualElement content;
            try {
                content = CreateInspectorGUIUnsafe();
            } catch (Exception e) {
                Debug.LogException(new Exception("Failed to render editor", e));
                content = VRCFuryEditorUtils.Error("Failed to render editor (see unity console)");
            }
            
            var versionLabel = new Label(SceneViewOverlay.GetOutputString() + " " + VRCFPackageUtils.Version);
            versionLabel.AddToClassList("vfVersionLabel");
            versionLabel.pickingMode = PickingMode.Ignore;
            
            var contentWithVersion = new VisualElement();
            contentWithVersion.styleSheets.Add(VRCFuryEditorUtils.GetResource<StyleSheet>("VRCFuryStyle.uss"));
            contentWithVersion.Add(versionLabel);
            contentWithVersion.Add(content);
            return contentWithVersion;
        }

        private VisualElement CreateInspectorGUIUnsafe() {
            if (!(target is UnityEngine.Component c)) {
                return VRCFuryEditorUtils.Error("This isn't a component?");
            }
            if (!(c is T v)) {
                return VRCFuryEditorUtils.Error("Unexpected type?");
            }

            var loadError = v.GetBrokenMessage();
            if (loadError != null) {
                return VRCFuryEditorUtils.Error(
                    $"This VRCFury component failed to load ({loadError}). It's likely that your VRCFury is out of date." +
                    " Please try Tools -> VRCFury -> Update VRCFury. If this doesn't help, let us know on the " +
                    " discord at https://vrcfury.com/discord");
            }
            
            var isInstance = PrefabUtility.IsPartOfPrefabInstance(v);

            var container = new VisualElement();

            container.Add(CreateOverrideLabel());

            if (isInstance) {
                // We prevent users from adding overrides on prefabs, because it does weird things (at least in unity 2019)
                // when you apply modifications to an object that lives within a SerializedReference. Some properties not overridden
                // will just be thrown out randomly, and unity will dump a bunch of errors.
                container.Add(CreatePrefabInstanceLabel(v));
            }

            VisualElement body;
            if (isInstance) {
                var copy = CopyComponent(v);
                try {
                    VRCFury.RunningFakeUpgrade = true;
                    copy.Upgrade();
                } finally {
                    VRCFury.RunningFakeUpgrade = false;
                }
                copy.gameObjectOverride = v.gameObject;
                var copySo = new SerializedObject(copy);
                body = CreateEditor(copySo, copy);
                body.SetEnabled(false);
                // We have to delay adding the editor to the inspector, because otherwise unity will call Bind()
                // on this visual element immediately after we return from this method, binding it back
                // to the original (non-temporary-upgraded) object
                var added = false;
                container.RegisterCallback<AttachToPanelEvent>(e => {
                    if (!added) {
                        added = true;
                        container.Add(body);
                        body.Bind(copySo);
                    }
                });
            } else {
                v.Upgrade();
                serializedObject.Update();
                body = CreateEditor(serializedObject, v);
                container.Add(body);
            }

            /*
            el.RegisterCallback<AttachToPanelEvent>(e => {
                CreateHeaderOverlay(el);
            });
            el.RegisterCallback<DetachFromPanelEvent>(e => {
                Detach();
            });
            el.style.marginBottom = 4;
            */
            return container;
        }

        private C CopyComponent<C>(C original) where C : UnityEngine.Component {
            OnDestroy();
            dummyObject = new GameObject();
            dummyObject.SetActive(false);
            dummyObject.hideFlags |= HideFlags.HideAndDontSave;
            var copy = dummyObject.AddComponent<C>();
            UnitySerializationUtils.CloneSerializable(original, copy);
            return copy;
        }

        public void OnDestroy() {
            if (dummyObject) {
                DestroyImmediate(dummyObject);
            }
        }

        public virtual VisualElement CreateEditor(SerializedObject serializedObject, T target) {
            return new VisualElement();
        }
        
        private VisualElement CreateOverrideLabel() {
            var baseText = "The VRCFury features in this prefab are overridden on this instance. Please revert them!" +
                           " If you apply, it may corrupt data in the changed features.";
            var overrideLabel = VRCFuryEditorUtils.Error(baseText);
            overrideLabel.SetVisible(false);

            double lastCheck = 0;
            void CheckOverride() {
                if (this == null) return; // The editor was deleted
                var vrcf = (VRCFuryComponent)target;
                var now = EditorApplication.timeSinceStartup;
                if (lastCheck < now - 1) {
                    lastCheck = now;
                    var mods = VRCFPrefabFixer.GetModifications(vrcf);
                    var isModified = mods.Count > 0;
                    overrideLabel.SetVisible(isModified);
                    if (isModified) {
                        overrideLabel.Clear();
                        overrideLabel.Add(VRCFuryEditorUtils.WrappedLabel(baseText + "\n\n" + string.Join(", ", mods.Select(m => m.propertyPath))));
                    }
                }
                EditorApplication.delayCall += CheckOverride;
            }
            CheckOverride();

            return overrideLabel;
        }

        private VisualElement CreatePrefabInstanceLabel(UnityEngine.Component component) {
            void Open() {
                var componentInBasePrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(component);
                var prefabPath = AssetDatabase.GetAssetPath(componentInBasePrefab);
                UnityCompatUtils.OpenPrefab(prefabPath, component.gameObject);
            }

            var label = new Button()
                .OnClick(Open)
                .Text("You are viewing a prefab instance\nClick here to edit VRCFury on the base prefab")
                .TextAlign(TextAnchor.MiddleCenter)
                .TextWrap()
                .Padding(5)
                .BorderColor(Color.black);
            label.style.paddingTop = 5;
            label.style.paddingBottom = 5;
            label.style.borderTopLeftRadius = 5;
            label.style.borderTopRightRadius = 5;
            label.style.borderBottomLeftRadius = 0;
            label.style.borderBottomRightRadius = 0;
            label.style.marginTop = 5;
            label.style.marginLeft = 20;
            label.style.marginRight = 20;
            label.style.marginBottom = 0;
            label.style.borderTopWidth = 1;
            label.style.borderLeftWidth = 1;
            label.style.borderRightWidth = 1;
            label.style.borderBottomWidth = 0;
            return label;
        }
    }
}
