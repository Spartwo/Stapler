using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

using KSP.UI.Screens;
using KSP.UI.TooltipTypes;
using KSP.UI;
using EditorGizmos;
using Highlighting;
using System.Collections;

using UnityEngine.EventSystems;

namespace Stapler
{
    // This class interfaces with EditorToolsUI to add an extra tool to change the direct parent of a part
    //[KSPAddon(KSPAddon.Startup.EditorAny, false)]
    internal class StaplerEditorGUI : MonoBehaviour
    {
        Toggle reparentButton;
        KeyBinding hotKey;
        EditorToolsUI editorToolsUI;

        KFSMEvent on_goToModeReparent;

        KFSMEvent on_goToModeScale;
        KFSMEvent on_scaleSelect;
        KFSMEvent on_scaleDeselect;
        KFSMEvent on_scaleReset;

        KFSMState st_scale_select;
        KFSMState st_scale_tweak;

        // Highlighting.
        public float rimFallOff = 1.5f;
        public float userHighlighterLimit = 1f;


        const ConstructionMode reparentConstructionMode = (ConstructionMode)3;

        void Start()
        {
            editorToolsUI = GetComponent<EditorToolsUI>();
            

            hotKey = new KeyBinding(KeyCode.Alpha5, ControlTypes.EDITOR_GIZMO_TOOLS | ControlTypes.KEYBOARDINPUT);

            CreateToolButton();

            GameEvents.onEditorConstructionModeChange.Remove(EditorLogic.fetch.onConstructionModeChanged);
            GameEvents.onEditorConstructionModeChange.Add(onConstructionModeChanged);

            PatchEditorFSM();
        }

        void CreateToolButton()
        {
            reparentButton = GameObject.Instantiate(editorToolsUI.rootButton);
            RectTransform buttonTransform = reparentButton.transform as RectTransform;
            Vector2 buttonPosition = buttonTransform.anchoredPosition;
            buttonPosition.x += ((editorToolsUI.rootButton.transform as RectTransform).anchoredPosition.x - (editorToolsUI.rotateButton.transform as RectTransform).anchoredPosition.x)*2;
            buttonTransform.SetParent(editorToolsUI.rootButton.transform.parent, false);
            buttonTransform.anchoredPosition = buttonPosition;

            reparentButton.gameObject.name = "scaleButton";
            reparentButton.GetComponent<TooltipController_Text>().SetText("Tool: Reparent");

            Texture2D offIconTexture = GameDatabase.Instance.GetTexture("Stapler/Icons/reparent_off", false);
            Texture2D onIconTexture = GameDatabase.Instance.GetTexture("Stapler/Icons/reparent_on", false);
            var oldSprite = reparentButton.image.sprite;
            reparentButton.image.sprite = Sprite.Create(offIconTexture, oldSprite.rect, oldSprite.pivot);

            (reparentButton.graphic as Image).sprite = Sprite.Create(onIconTexture, oldSprite.rect, oldSprite.pivot);

            reparentButton.onValueChanged.AddListener(onReparentButtonInput);
        }

        Part selectedPart
        {
            get => EditorLogic.fetch.selectedPart;
            set { EditorLogic.fetch.selectedPart = value; }
        }

        IEnumerator HighlightPart(Part part, Color color)
        {
            part.SetHighlightColor(Color.clear);
            part.SetHighlightType(Part.HighlightType.Disabled);

            part.highlighter.FlashingOn();

            yield return new WaitForSeconds(0.5f);

            part.highlighter.FlashingOff();
        }

        IEnumerator DehighlightPart(Part part)
        {
            part.SetHighlightColor(Color.clear);
            part.SetHighlightType(Part.HighlightType.Disabled);

            yield return null;
        }
        void PatchEditorFSM()
        {
            KerbalFSM fsm = EditorLogic.fetch.fsm;
            int layerMask = EditorLogic.fetch.layerMask | 4 | 0x200000;

            // add states

            /*st_scale_select = new KFSMState("st_scale_select")
            {
                OnUpdate = Combine(
                    EditorLogic.fetch.UndoRedoInputUpdate,
                    EditorLogic.fetch.snapInputUpdate,
                    EditorLogic.fetch.partSearchUpdate)
            };
            fsm.AddState(st_scale_select);

            st_scale_tweak = new KFSMState("st_scale_tweak")
            {
                OnEnter = delegate
                {
                    selectedPart.onEditorStartTweak();
                    Transform referenceTransform = selectedPart.GetReferenceTransform();
                    EditorLogic.fetch.symUpdateMode = selectedPart.symmetryCounterparts.Count;
                    if (EditorLogic.fetch.ship.Contains(selectedPart))
                    {
                        EditorLogic.fetch.symUpdateParent = selectedPart.parent;
                        EditorLogic.fetch.symUpdateAttachNode = selectedPart.FindAttachNodeByPart(EditorLogic.fetch.symUpdateParent);
                    }
                    else
                    {
                        EditorLogic.fetch.symUpdateParent = EditorLogic.fetch.attachment.potentialParent;
                        EditorLogic.fetch.symUpdateAttachNode = EditorLogic.fetch.attachment.callerPartNode;
                    }

                    if (EditorLogic.fetch.symUpdateAttachNode != null)
                    {
                        EditorLogic.fetch.gizmoPivot = referenceTransform.TransformPoint(EditorLogic.fetch.symUpdateAttachNode.position);
                    }
                    else
                    {
                        EditorLogic.fetch.gizmoPivot = referenceTransform.transform.position;
                    }

                    gizmoScale = GizmoOffset.Attach(referenceTransform, selectedPart.initRotation, null, onScaleGizmoUpdated, EditorLogic.fetch.editorCamera);
                    gizmoScale.transform.position = gizmoScale.trfPos0 = EditorLogic.fetch.gizmoPivot;
                    gizmoScale.useGrid = true; // HACK: we use this to test whether we need to rebind the handle events in partscaleInputUpdate
                    GameEvents.onEditorSnapModeChange.Remove(gizmoScale.onEditorSnapChanged);
                    EditorLogic.fetch.audioSource.PlayOneShot(EditorLogic.fetch.tweakGrabClip);
                },
                OnUpdate = Combine(
                    partscaleInputUpdate,
                    EditorLogic.fetch.UndoRedoInputUpdate,
                    EditorLogic.fetch.snapInputUpdate,
                    EditorLogic.fetch.deleteInputUpdate,
                    EditorLogic.fetch.partSearchUpdate),
                OnLeave = (KFSMState to) =>
                {
                    gizmoScale.Detach();
                    EditorLogic.fetch.symUpdateMode = 0;
                    EditorLogic.fetch.symUpdateParent = null;
                    EditorLogic.fetch.symUpdateAttachNode = null;
                    if (to != EditorLogic.fetch.st_offset_tweak && to != EditorLogic.fetch.st_rotate_tweak && selectedPart != null)
                    {
                        selectedPart.onEditorEndTweak();
                        if (to == EditorLogic.fetch.st_idle)
                        {
                            selectedPart = null;
                        }
                    }

                    EditorLogic.fetch.audioSource.PlayOneShot(EditorLogic.fetch.tweakReleaseClip);
                }
            };
            fsm.AddState(st_scale_tweak);
            */
            // add events

            on_goToModeReparent = new KFSMEvent("on_goToModeReparent")
            {
                updateMode = KFSMUpdateMode.MANUAL_TRIGGER,
                OnEvent = delegate
                {
                    /*if (EditorLogic.fetch.selectedPart == null)
                    {
                        ScreenMessages.PostScreenMessage("Select a part to Scale", EditorLogic.fetch.modeMsg);
                        on_goToModeReparent.GoToStateOnEvent = st_scale_select;
                    }
                    else if (!EditorLogic.fetch.ship.Contains(EditorLogic.fetch.selectedPart))
                    {
                        on_goToModeReparent.GoToStateOnEvent = EditorLogic.fetch.st_place;
                        EditorLogic.fetch.on_partPicked.OnEvent();
                    }
                    else
                    {
                        on_goToModeScale.GoToStateOnEvent = st_scale_tweak;
                    }*/
                }
            };
            fsm.AddEvent(on_goToModeReparent, EditorLogic.fetch.st_idle, EditorLogic.fetch.st_offset_select, EditorLogic.fetch.st_offset_tweak, EditorLogic.fetch.st_rotate_select, EditorLogic.fetch.st_rotate_tweak, EditorLogic.fetch.st_root_unselected, EditorLogic.fetch.st_root_select);

            /*on_scaleSelect = new KFSMEvent("on_scaleSelect")
            {
                updateMode = KFSMUpdateMode.UPDATE,
                OnCheckCondition = delegate
                {
                    if (Input.GetMouseButtonUp(0) && !EventSystem.current.IsPointerOverGameObject())
                    {
                        selectedPart = EditorLogic.fetch.pickPart(layerMask, Input.GetKey(KeyCode.LeftShift), pickRootIfFrozen: false);
                        if (selectedPart != null)
                        {
                            if (!EditorLogic.fetch.ship.Contains(selectedPart))
                            {
                                on_scaleSelect.GoToStateOnEvent = EditorLogic.fetch.st_place;
                                EditorLogic.fetch.on_partPicked.OnEvent();
                                return false;
                            }

                            if (!selectedPart.HasModuleImplementing<TweakScale>())
                            {
                                ScreenMessages.PostScreenMessage("Part does not support scaling", 1, ScreenMessageStyle.LOWER_CENTER);
                                selectedPart = null;
                                return false;
                            }

                            on_scaleSelect.GoToStateOnEvent = st_scale_tweak;
                            return true;
                        }
                    }

                    return false;
                }
            };
            fsm.AddEvent(on_scaleSelect, st_scale_select);

            on_scaleDeselect = new KFSMEvent("on_scaleDeselect")
            {
                GoToStateOnEvent = st_scale_select,
                updateMode = KFSMUpdateMode.UPDATE,
                OnCheckCondition = delegate
                {
                    if (Mouse.Left.GetButtonDown() && !Mouse.Left.WasDragging() && !gizmoScale.GetMouseOverGizmo && !EventSystem.current.IsPointerOverGameObject())
                    {
                        Part pickedPart = EditorLogic.fetch.pickPart(layerMask, Input.GetKey(KeyCode.LeftShift), pickRootIfFrozen: false);
                        if (pickedPart == null)
                        {
                            selectedPart.onEditorEndTweak();
                            selectedPart.gameObject.SetLayerRecursive(0, filterTranslucent: true, 2097152);
                            selectedPart = null;
                            return true;
                        }

                        if (EditorGeometryUtil.GetPixelDistance(gizmoScale.transform.position, Input.mousePosition, EditorLogic.fetch.editorCamera) > 75f)
                        {
                            selectedPart.onEditorEndTweak();
                            selectedPart.gameObject.SetLayerRecursive(0, filterTranslucent: true, 2097152);
                            selectedPart = pickedPart;
                            return true;
                        }
                    }

                    return false;
                }
            };
            fsm.AddEvent(on_scaleDeselect, st_scale_tweak);

            on_scaleReset = new KFSMEvent("on_scaleReset")
            {
                GoToStateOnEvent = st_scale_tweak,
                updateMode = KFSMUpdateMode.MANUAL_TRIGGER
            };
            fsm.AddEvent(on_scaleReset, st_scale_tweak);

            EditorLogic.fetch.on_undoRedo.OnEvent += delegate
            {
                if (fsm.currentState == st_scale_tweak)
                {
                    EditorLogic.fetch.on_undoRedo.GoToStateOnEvent = st_scale_select;
                }
            };

            EditorLogic.fetch.st_place.OnUpdate -= EditorLogic.fetch.partRotationInputUpdate;
            EditorLogic.fetch.st_place.OnUpdate += st_place_OnUpdate;

            // add existing events to our new states
            fsm.AddEvent(EditorLogic.fetch.on_partDeleted, st_scale_tweak);
            fsm.AddEvent(EditorLogic.fetch.on_goToModeRotate, st_scale_select, st_scale_tweak);
            fsm.AddEvent(EditorLogic.fetch.on_goToModePlace, st_scale_select, st_scale_tweak);
            fsm.AddEvent(EditorLogic.fetch.on_goToModeOffset, st_scale_select, st_scale_tweak);
            fsm.AddEvent(EditorLogic.fetch.on_goToModeRoot, st_scale_select, st_scale_tweak);
            fsm.AddEvent(EditorLogic.fetch.on_undoRedo, st_scale_tweak);
            fsm.AddEvent(EditorLogic.fetch.on_podDeleted, st_scale_select, st_scale_tweak);
            fsm.AddEvent(EditorLogic.fetch.on_partCreated, st_scale_select, st_scale_tweak);
            fsm.AddEvent(EditorLogic.fetch.on_partOverInventoryPAW, st_scale_select, st_scale_tweak);
            fsm.AddEvent(EditorLogic.fetch.on_newShip, st_scale_select, st_scale_tweak);
            fsm.AddEvent(EditorLogic.fetch.on_shipLoaded, st_scale_select, st_scale_tweak);*/
        }
        private void onConstructionModeChanged(ConstructionMode mode)
        {
           if (mode == EditorLogic.fetch.constructionMode) return;

            if (mode == reparentConstructionMode)
            {
                EditorLogic.fetch.coordSpaceBtn.gameObject.SetActive(value: false);
                EditorLogic.fetch.radialSymmetryBtn.gameObject.SetActive(value: false);

                EditorLogic.fetch.fsm.RunEvent(on_goToModeReparent);

                EditorLogic.fetch.constructionMode = mode;
                EditorLogic.fetch.constructionMode = mode;
            }
            else
            {
                EditorLogic.fetch.onConstructionModeChanged(mode);
            }
        }

        void OnDestroy()
        {
            reparentButton.onValueChanged.RemoveListener(onReparentButtonInput);
            GameEvents.onEditorConstructionModeChange.Remove(onConstructionModeChanged);
        }


        void Update()
        {
            if (hotKey.GetKeyDown())
            {
                SetMode(reparentConstructionMode, true);
            }
        }

        private void onReparentButtonInput(bool b)
        {
            if (b && reparentButton.interactable)
            {
                SetMode(reparentConstructionMode, false);
            }
        }

        public void SetMode(ConstructionMode mode, bool updateUI)
        {
            editorToolsUI.SetMode(mode, updateUI);

            if (editorToolsUI.constructionMode == reparentConstructionMode && updateUI)
            {
                reparentButton.isOn = true;
            }
        }
    }
}
