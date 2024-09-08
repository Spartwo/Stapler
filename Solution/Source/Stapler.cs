using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.EventSystems;

using Highlighting;
using Expansions.Serenity;

namespace Stapler
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    class Stapler : MonoBehaviour
    {
        // Setup.

        public StaplerButton button;
        private KeyBinding keyBinding;
        public static bool hasTweakScale;

        // Extremely dumb. STAP as a number.
        public const ConstructionMode reparentMode = (ConstructionMode)1920116;

        // Editor FSM.

        private EditorLogic editor => EditorLogic.fetch;

        private KerbalFSM FSM => editor.fsm;

        private KFSMEvent on_goToModeParent;
        private KFSMEvent on_pickChild;
        private KFSMEvent on_pickParent;
        private KFSMEvent on_pickNothing;

        private KFSMState st_parent_selectChild;
        private KFSMState st_parent_selectParent;

        private Part selectedPart;

        private readonly List<PartSelector> partSelectors = new List<PartSelector>();

        protected void Start()
        {
            // Check for TweakScale.
            hasTweakScale = CheckTweakScale();

            // Set the hotkey.
            KeyCode hotkey = hasTweakScale ? KeyCode.Alpha6 : KeyCode.Alpha5;
            keyBinding = new KeyBinding(hotkey, ControlTypes.EDITOR_GIZMO_TOOLS | ControlTypes.KEYBOARDINPUT);

            // Add the reparent tool to the tools UI.
            button = StaplerButton.Create(this);

            // Register event.
            GameEvents.onEditorConstructionModeChange.Add(OnConstructionModeChanged);

            // Extend the stock editor logic FSM.
            ExtendFSM();
        }

        protected void OnDestroy()
        {
            GameEvents.onEditorConstructionModeChange.Remove(OnConstructionModeChanged);
        }

        protected void Update()
        {
            // Number hotkey to activate the tool.

            if (!EditorLogic.fetch.NameOrDescriptionFocused() && !DeltaVApp.AnyTextFieldHasFocus() && !RoboticControllerManager.AnyWindowTextFieldHasFocus())
                if (keyBinding.GetKeyDown())
                    SetMode(true);
        }

        public static bool CheckTweakScale()
        {
            var minVersion = new Version(3, 2);

            foreach (var assy in AssemblyLoader.loadedAssemblies)
            {
                if (assy.name.Equals("Scale"))
                {
                    Version version = assy.assembly.GetName().Version;
                    if (version > minVersion)
                        return true;
                }
            }

            return false;
        }

        static KFSMCallback Combine(params KFSMCallback[] callbacks)
        {
            return (KFSMCallback)Delegate.Combine(callbacks);
        }

        public void SetMode(bool updateUI)
        {
            EditorLogic.fetch.toolsUI.SetMode(reparentMode, updateUI);
            button.toggle.isOn = true;
        }

        private void ExtendFSM()
        {
            // Add new states/events to the FSM to accommodate the re-parenting tool.
            // Based on TweakScale's FSM extension.


            // 1. Event to transition to the re-parenting mode.

            on_goToModeParent = new KFSMEvent("on_goToModeParent");
            on_goToModeParent.updateMode = KFSMUpdateMode.MANUAL_TRIGGER;

            on_goToModeParent.OnEvent = delegate
            {
                on_goToModeParent.GoToStateOnEvent = st_parent_selectChild;
            };

            FSM.AddEventExcluding(on_goToModeParent, st_parent_selectChild, st_parent_selectParent, editor.st_podSelect);


            // 2. State from which to SELECT the CHILD part to re-parent.

            st_parent_selectChild = new KFSMState("st_parent_selectChild");

            st_parent_selectChild.OnEnter = delegate
            {
                ScreenMessages.PostScreenMessage("Select a part to Re-parent", editor.modeMsg);
            };

            st_parent_selectChild.OnUpdate = delegate
            {
            };

            st_parent_selectChild.OnLeave = delegate (KFSMState to)
            {
            };

            FSM.AddState(st_parent_selectChild);
            AddStockEvents(st_parent_selectChild);


            // 3. Conditional event upon CLICKING a CHILD PART, which transitions to st_parent_selectParent.

            on_pickChild = new KFSMEvent("on_pickChild")
            {
                updateMode = KFSMUpdateMode.UPDATE,

                OnCheckCondition = delegate
                {
                    if (Input.GetMouseButtonUp(0) && !Mouse.Left.WasDragging(25f) && !EventSystem.current.IsPointerOverGameObject())
                    {
                        selectedPart = editor.pickPart(editor.layerMask | 4 | 0x200000, false, false);

                        if (selectedPart == null || selectedPart == EditorLogic.RootPart)
                        {
                            selectedPart = null;
                            return false;
                        }

                        return true;
                    }

                    return false;
                }
            };

            on_pickChild.OnEvent = delegate
            {
                if (selectedPart != EditorLogic.RootPart)
                {
                    on_pickChild.GoToStateOnEvent = st_parent_selectParent;
                }
                else
                {
                    on_pickChild.GoToStateOnEvent = st_parent_selectChild;
                    ScreenMessages.PostScreenMessage("Cannot re-parent the root part", 5f, ScreenMessageStyle.UPPER_CENTER);
                    //audioSource.PlayOneShot(cannotPlaceClip);
                    selectedPart = null;
                }
            };

            FSM.AddEvent(on_pickChild, st_parent_selectChild);


            // 4. State from which to SELECT the new PARENT part.

            st_parent_selectParent = new KFSMState("st_parent_selectParent");

            st_parent_selectParent.OnEnter = delegate
            {
                ScreenMessages.PostScreenMessage("Select a part to be the new parent", editor.modeMsg);

                // Make a list of all parts MINUS all invalid parts.

                var allInvalid = new List<Part>
                {
                    selectedPart,
                    selectedPart.parent
                };

                allInvalid.AddRange(EditorLogicBase.FindPartsInChildren(selectedPart));

                foreach (var part in selectedPart.symmetryCounterparts)
                {
                    allInvalid.Add(part);
                    allInvalid.AddRange(EditorLogicBase.FindPartsInChildren(part));
                }

                var allValid = editor.getSortedShipList().Except(allInvalid.Distinct()).ToList();

                // Duplicate re-root selection behaviour for valid parts.
                // Part selectors are added to each valid part, they handle highlighting and selection, and provide a callback.

                foreach (var part in allValid)
                {
                    part.SetHighlight(active: false, recursive: false);
                    part.SetHighlightType(Part.HighlightType.Disabled);
                }

                foreach (var part in allValid)
                    partSelectors.Add(PartSelector.Create(part, OnParentSelect, Highlighter.colorPartRootToolHighlight, Highlighter.colorPartRootToolHover, Highlighter.colorPartRootToolHighlightEdge, Highlighter.colorPartRootToolHoverEdge));
            };

            st_parent_selectParent.OnUpdate = Combine(
                EditorLogic.fetch.UndoRedoInputUpdate,
                EditorLogic.fetch.snapInputUpdate,
                EditorLogic.fetch.partSearchUpdate);

            st_parent_selectParent.OnLeave = delegate (KFSMState to)
            {
                // Clean up part selectors.

                foreach (var selector in partSelectors)
                {
                    selector.Dismiss();
                }

                partSelectors.Clear();
            };

            FSM.AddState(st_parent_selectParent);
            AddStockEvents(st_parent_selectParent);


            // 4.5 Event to CANCEL the selection of a new parent part, which transitions back to st_parent_selectChild.

            on_pickNothing = new KFSMEvent("on_pickNothing");
            on_pickNothing.GoToStateOnEvent = st_parent_selectChild;
            on_pickNothing.updateMode = KFSMUpdateMode.LATEUPDATE;
            on_pickNothing.OnCheckCondition = delegate
            {
                // Cancel the selection if the user clicks on nothing.

                if (Mouse.Left.GetButtonUp() && !Mouse.Left.WasDragging() && !editor.pickPart(editor.layerMask | 4 | 0x200000, pickRoot: false, pickRootIfFrozen: false))
                {
                    selectedPart.gameObject.SetLayerRecursive(0, filterTranslucent: true, 2097152);
                    selectedPart.SetHighlightDefault();
                    selectedPart = null;

                    return true;
                }

                return false;
            };

            FSM.AddEvent(on_pickNothing, st_parent_selectParent);


            // 5. Event for when the PARENT is SELECTED, which transitions back to st_parent_selectChild.
            // The operation itself is in OnParentSelect.

            on_pickParent = new KFSMEvent("on_pickParent");
            on_pickParent.GoToStateOnEvent = st_parent_selectChild;
            on_pickParent.updateMode = KFSMUpdateMode.MANUAL_TRIGGER;
            on_pickParent.OnEvent = delegate
            {
                editor.audioSource.PlayOneShot(editor.reRootClip);
                GameEvents.onEditorPartEvent.Fire(ConstructionEventType.Unknown, selectedPart);
            };

            FSM.AddEvent(on_pickParent, st_parent_selectParent);
        }

        public static float SqrDistance(Part part, Part other)
        {
            return Vector3.SqrMagnitude(part.transform.position - other.transform.position);
        }

        private void OnParentSelect(Part newParent)
        {
            if (newParent == null || !editor.ship.Contains(newParent) || newParent == selectedPart || selectedPart.parent == null)
            {
                Debug.LogError("Stapler: Invalid parent part selected.");
                return;
            }

            // Re-parent the selected part and its symmetry counterparts.

            var parts = new List<Part>();
            parts.Add(selectedPart);
            parts.AddRange(selectedPart.symmetryCounterparts);

            foreach (var part in parts)
            {
                part.parent.removeChild(part);
                EditorLogicBase.clearAttachNodes(part, part.parent);

                var localParent = newParent;
                if (newParent.symmetryCounterparts.Count > 0)
                {
                    // If the new parent has symmetry counterparts, pick the closest one to us.
                    // This is just a guess. I'll wait to see where it goes wrong.

                    var newParentAndCounterParts = newParent.symmetryCounterparts.Concat(new[] { newParent });
                    localParent = newParentAndCounterParts.OrderBy(p => SqrDistance(p, part)).First();
                }

                part.setParent(localParent);
                part.transform.SetParent(localParent.transform, true);
                part.attachMode = AttachModes.SRF_ATTACH;
                part.onAttach(localParent);
            }

            // Play a sound and run the event that changes the state back to the start.

            editor.audioSource.PlayOneShot(editor.partGrabClip);
            editor.pickPart(LayerUtil.DefaultEquivalent | 4 | 0x200000, pickRoot: true, pickRootIfFrozen: true);
            FSM.RunEvent(on_pickParent);
        }

        private void AddStockEvents(KFSMState state)
        {
            // Add all stock events to the state.
            // Without this, the user is trapped when they enter our new state. 

            FSM.AddEvent(EditorLogic.fetch.on_partDeleted, state);
            FSM.AddEvent(EditorLogic.fetch.on_goToModeRotate, state);
            FSM.AddEvent(EditorLogic.fetch.on_goToModePlace, state);
            FSM.AddEvent(EditorLogic.fetch.on_goToModeOffset, state);
            FSM.AddEvent(EditorLogic.fetch.on_goToModeRoot, state);
            FSM.AddEvent(EditorLogic.fetch.on_undoRedo, state);
            FSM.AddEvent(EditorLogic.fetch.on_podDeleted, state);
            FSM.AddEvent(EditorLogic.fetch.on_partCreated, state);
            FSM.AddEvent(EditorLogic.fetch.on_partOverInventoryPAW, state);
            FSM.AddEvent(EditorLogic.fetch.on_newShip, state);
            FSM.AddEvent(EditorLogic.fetch.on_shipLoaded, state);
        }

        private void OnConstructionModeChanged(ConstructionMode mode)
        {
            // Event is called by the game whenever the construction mode changes.
            // We need to run our own event to transition to the new mode.

            // todo: Check state before deciding to transition or not.

            if (mode != reparentMode || FSM.CurrentState == st_parent_selectChild)
                return;

            EditorLogic.fetch.fsm.RunEvent(on_goToModeParent);
        }
    }
}