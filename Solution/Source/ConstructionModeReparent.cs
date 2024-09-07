
using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using KSP.UI;
using KSP.UI.TooltipTypes;
using Vectrosity;

namespace Stapler
{
	// this is a component that gets added to the instance of EditorToolsUI and interacts with it in order to provide an additional "scale" tool mode in the editor
	internal class ConstructionModeReparent : MonoBehaviour
	{
		Toggle reparentButton;
		KeyBinding keyBinding;
		EditorToolsUI editorToolsUI;

		Color colorOrange = new Color(1.0f, 0.6f, 0.2f);
		Color colorRed = new Color(1.0f, 0.2f, 0.0f);
		Color colorBlue = new Color(0.3f, 0.3f, 1.0f);

		private float lineWidth = 2f;

		List<VectorLine> parentageLines;
		List<VectorLine> potentialLines;

		KFSMEvent on_enterReparentMode;
		KFSMEvent on_exitReparentMode;
		KFSMEvent on_childSelected;
		KFSMEvent on_scaleReset;

		KFSMState state_reparent_select;
		KFSMState state_reparent_tweak;

		const ConstructionMode scaleConstructionMode = (ConstructionMode)5;

		void Start()
		{
			editorToolsUI = GetComponent<EditorToolsUI>();

			keyBinding = new KeyBinding(StaplerEditorLogic.Instance.ScaleModeKey, ControlTypes.EDITOR_GIZMO_TOOLS | ControlTypes.KEYBOARDINPUT);

			CreateToolButton();

			GameEvents.onEditorConstructionModeChange.Remove(EditorLogic.fetch.onConstructionModeChanged);
			GameEvents.onEditorConstructionModeChange.Add(onConstructionModeChanged);

			PatchEditorFSM();
		}

		void CreateToolButton()
		{
			// Clone the root button and get a starting position
			reparentButton = GameObject.Instantiate(editorToolsUI.rootButton);
			RectTransform buttonTransform = reparentButton.transform as RectTransform;
			Vector2 buttonPosition = buttonTransform.anchoredPosition;
			// Move right by an incriment equal to the gap between the other tools.
			float offset = (editorToolsUI.rootButton.transform as RectTransform).anchoredPosition.x - (editorToolsUI.rotateButton.transform as RectTransform).anchoredPosition.x;
			buttonPosition.x += (Tools.isTweakscaleInstalled()) ? offset * 2f : offset;
			// Apply this new position
			buttonTransform.SetParent(editorToolsUI.rootButton.transform.parent, false);
			buttonTransform.anchoredPosition = buttonPosition;

			reparentButton.gameObject.name = "reparentButton";
			reparentButton.GetComponent<TooltipController_Text>().SetText("Tool: Reparent");
			// Overwrite icon textures
			Texture2D offIconTexture = GameDatabase.Instance.GetTexture("Stapler/Icons/reparent_off", false);
			Texture2D onIconTexture = GameDatabase.Instance.GetTexture("Stapler/Icons/reparent_on", false);
			var oldSprite = reparentButton.image.sprite;
			reparentButton.image.sprite = Sprite.Create(offIconTexture, oldSprite.rect, oldSprite.pivot);
			(reparentButton.graphic as Image).sprite = Sprite.Create(onIconTexture, oldSprite.rect, oldSprite.pivot);

			// Add a listener for button selection
			reparentButton.onValueChanged.AddListener(onreparentButtonInput);
		}

		Part selectedPart
		{
			get => EditorLogic.fetch.selectedPart;
			set { EditorLogic.fetch.selectedPart = value; }
		}
		Part selectedParent {
			get => EditorLogic.fetch.selectedPart.parent;
		}

		static KFSMCallback Combine(params KFSMCallback[] callbacks)
		{
			return (KFSMCallback)Delegate.Combine(callbacks);
		}

		void PatchEditorFSM()
		{
			KerbalFSM fsm = EditorLogic.fetch.fsm;
			int layerMask = EditorLogic.fetch.layerMask | 4 | 0x200000;

			// add states
			state_reparent_select = new KFSMState("state_reparent_select")
			{
				OnUpdate = Combine(
					EditorLogic.fetch.UndoRedoInputUpdate,
					EditorLogic.fetch.snapInputUpdate,
					EditorLogic.fetch.partSearchUpdate)
			};
			fsm.AddState(state_reparent_select);

			state_reparent_tweak = new KFSMState("state_reparent_tweak")
			{
				OnEnter = delegate
				{
					
					EditorLogic.fetch.audioSource.PlayOneShot(EditorLogic.fetch.tweakGrabClip);
				},
				OnUpdate = Combine(
					//partscaleInputUpdate,
					EditorLogic.fetch.UndoRedoInputUpdate,
					EditorLogic.fetch.snapInputUpdate,
					EditorLogic.fetch.deleteInputUpdate,
					EditorLogic.fetch.partSearchUpdate),
				OnLeave = (KFSMState to) =>
				{
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
			fsm.AddState(state_reparent_tweak);


			on_enterReparentMode = new KFSMEvent("on_enterReparentMode")
			{
				updateMode = KFSMUpdateMode.MANUAL_TRIGGER,
				OnEvent = delegate 
				{
					Debug.Log($"[Stapler]: Entering reparent mode");
					if (EditorLogic.fetch.selectedPart == null)
					{
						ScreenMessages.PostScreenMessage("Select a part to Reparent", EditorLogic.fetch.modeMsg);
						on_enterReparentMode.GoToStateOnEvent = state_reparent_select;
					}
					else if (!EditorLogic.fetch.ship.Contains(EditorLogic.fetch.selectedPart))
					{
						on_enterReparentMode.GoToStateOnEvent = EditorLogic.fetch.st_place;
						EditorLogic.fetch.on_partPicked.OnEvent();
					}
					else
					{
						on_enterReparentMode.GoToStateOnEvent = state_reparent_tweak;
					}
				}
			};
			fsm.AddEvent(on_enterReparentMode, EditorLogic.fetch.st_idle, EditorLogic.fetch.st_offset_select, EditorLogic.fetch.st_offset_tweak, EditorLogic.fetch.st_rotate_select, EditorLogic.fetch.st_rotate_tweak, EditorLogic.fetch.st_root_unselected, EditorLogic.fetch.st_root_select);


			/*on_partSelected = new KFSMEvent("on_partSelected") {

			};
			fsm.AddEvent(on_partSelected, on_childSelected);*/

			/// <summary>
			/// Called when no longer in reparent mode
			/// </summary>
			on_exitReparentMode = new KFSMEvent("on_exitReparentMode") 
			{
				updateMode = KFSMUpdateMode.MANUAL_TRIGGER,
				OnEvent = delegate {
					Debug.Log($"[Stapler]: Exiting reparent mode");
					if (selectedPart != null)
					{
						SetPartHightlight(selectedPart, Color.clear, false);
						selectedPart = null;
					}
					if (selectedParent != null)
					{
						SetPartHightlight(selectedParent, Color.clear, false);
					}
				}
			};
			fsm.AddEvent(on_exitReparentMode);

			on_childSelected = new KFSMEvent("on_childSelected")
			{
				updateMode = KFSMUpdateMode.UPDATE,
				OnCheckCondition = delegate
				{
					if (Input.GetMouseButtonUp(0) && !EventSystem.current.IsPointerOverGameObject())
					{
						selectedPart = EditorLogic.fetch.pickPart(layerMask, Input.GetKey(KeyCode.LeftShift), pickRootIfFrozen: false);
						SetPartHightlight(selectedPart, Color.green, true);

						List<Part> roots = GetPartLineage(selectedPart);

						SetPartHightlight(roots[1], colorOrange, true);

						/*for (int i = 1; i < roots.Count; i++)
						{
							SetPartHightlight(roots[i], colorOrange, true);
							
						}*/
						//DrawPartConnections(roots, colorOrange);

						if (selectedPart != null)
						{
							if (!EditorLogic.fetch.ship.Contains(selectedPart))
							{
								on_childSelected.GoToStateOnEvent = EditorLogic.fetch.st_place;
								EditorLogic.fetch.on_partPicked.OnEvent();
								return false;
							}

							if (!selectedPart.attachRules.allowSrfAttach)
							{
								ScreenMessages.PostScreenMessage("No Surface Attachment", 1, ScreenMessageStyle.LOWER_CENTER);
								selectedPart = null;
								return false;
							}

							if (!selectedPart.attachRules.srfAttach)
							{
								ScreenMessages.PostScreenMessage("Node Only", 1, ScreenMessageStyle.LOWER_CENTER);
								selectedPart = null;
								return false;
							}

							return true;
						}
					}

					return false;
				}
			};
			fsm.AddEvent(on_childSelected, state_reparent_select);

			

			on_scaleReset = new KFSMEvent("on_scaleReset")
			{
				GoToStateOnEvent = state_reparent_tweak,
				updateMode = KFSMUpdateMode.MANUAL_TRIGGER
			};
			fsm.AddEvent(on_scaleReset, state_reparent_tweak);

			EditorLogic.fetch.on_undoRedo.OnEvent += delegate
			{
				if (fsm.currentState == state_reparent_tweak)
				{
					//EditorLogic.fetch.on_undoRedo.GoToStateOnEvent = state_reparent_select;
				}
			};

			EditorLogic.fetch.st_place.OnUpdate -= EditorLogic.fetch.partRotationInputUpdate;

			// add existing events to our new states
			fsm.AddEvent(EditorLogic.fetch.on_partDeleted, state_reparent_tweak);
			fsm.AddEvent(EditorLogic.fetch.on_goToModeRotate, state_reparent_select, state_reparent_tweak);
			fsm.AddEvent(EditorLogic.fetch.on_goToModePlace, state_reparent_select, state_reparent_tweak);
			fsm.AddEvent(EditorLogic.fetch.on_goToModeOffset, state_reparent_select, state_reparent_tweak);
			fsm.AddEvent(EditorLogic.fetch.on_goToModeRoot, state_reparent_select, state_reparent_tweak);
			fsm.AddEvent(EditorLogic.fetch.on_undoRedo, state_reparent_tweak);
			fsm.AddEvent(EditorLogic.fetch.on_podDeleted, state_reparent_select, state_reparent_tweak);
			fsm.AddEvent(EditorLogic.fetch.on_partCreated, state_reparent_select, state_reparent_tweak);
			fsm.AddEvent(EditorLogic.fetch.on_partOverInventoryPAW, state_reparent_select, state_reparent_tweak);
			fsm.AddEvent(EditorLogic.fetch.on_newShip, state_reparent_select, state_reparent_tweak);
			fsm.AddEvent(EditorLogic.fetch.on_shipLoaded, state_reparent_select, state_reparent_tweak);
		}


		private void onScaleGizmoUpdated(Vector3 arg1)
		{
			if (EditorLogic.fetch.ship.Contains(selectedPart))
			{
				EditorLogic.fetch.SetBackup();
			}

			GameEvents.onEditorPartEvent.Fire(ConstructionEventType.PartOffset, selectedPart);
		}

		private void onConstructionModeChanged(ConstructionMode mode)
		{
			if (mode == EditorLogic.fetch.constructionMode) return;

			if (mode == scaleConstructionMode)
			{
				EditorLogic.fetch.coordSpaceBtn.gameObject.SetActive(value: false);
				EditorLogic.fetch.radialSymmetryBtn.gameObject.SetActive(value: false);

				EditorLogic.fetch.fsm.RunEvent(on_enterReparentMode);

				EditorLogic.fetch.constructionMode = mode;
			}
			else
			{
				EditorLogic.fetch.fsm.RunEvent(on_exitReparentMode);
				EditorLogic.fetch.onConstructionModeChanged(mode);
			}
		}

		void OnDestroy()
		{
			reparentButton.onValueChanged.RemoveListener(onreparentButtonInput);
			GameEvents.onEditorConstructionModeChange.Remove(onConstructionModeChanged);
		}

		void Update()
		{
			if (keyBinding.GetKeyDown())
			{
				SetMode(scaleConstructionMode, true);
			}
		}

		private void onreparentButtonInput(bool b)
		{
			if (b && reparentButton.interactable)
			{
				SetMode(scaleConstructionMode, false);
			}
		}

		public void SetMode(ConstructionMode mode, bool updateUI)
		{
			editorToolsUI.SetMode(mode, updateUI);

			if (editorToolsUI.constructionMode == scaleConstructionMode && updateUI)
			{
				reparentButton.isOn = true;
			}
		}
		void SetPartHightlight(Part part, Color color, bool highlight)
		{
			try
			{//Part.HighlightType.OnMouseOver
				part.SetHighlightColor(color);
				part.SetHighlightType((highlight) ? Part.HighlightType.AlwaysOn : Part.HighlightType.Disabled);
			} 
			catch(Exception e) 
			{ 
				Debug.Log($"[Stapler]: {e}"); 
			}
		}

		/// <summary>
		/// Returns an ordered list of parts that are sequential parents of each other until the root part
		/// </summary>
		/// <param name="part"> The starting point of the search, can have children of its own</param>
		/// <returns></returns>
		List<Part> GetPartLineage(Part part)
		{
			List<Part> lineage = new List<Part>();
			Part selectedPart = part;
			
			while (true) 
			{ 
				if(selectedPart != null)
				{
					lineage.Add(selectedPart);
					selectedPart = selectedPart.parent;
				} 
				else
				{
					break;
				}
			}
			return lineage;
		}

		void DrawPartConnections(List<Part> parts, Color lineColor)
		{
			if (parts == null || parts.Count < 2)
			{
				Debug.LogError($"[Stapler]: At least two points are required to create a line.");
				return;
			}

			Debug.LogError($"[Stapler]: Drawing limes");
			Camera mainCamera = Camera.main;

			try
			{
				List<Vector2> points = new List<Vector2>();

				// Instantiate lines for each pair of start and end points
				for (int i = 0; i < parts.Count; i++)
				{
					Vector3 screenPosition = mainCamera.WorldToScreenPoint(parts[i].transform.position);
					points.Add(new Vector2(screenPosition.x, screenPosition.y));
				}
				VectorLine vectorLine = new VectorLine("Line", points, lineWidth);

				vectorLine.lineType = LineType.Continuous;
				vectorLine.rectTransform.SetParent(UIMasterController.Instance.mainCanvas.transform, worldPositionStays: false);
				vectorLine.color = lineColor;
				vectorLine.active = true;
				vectorLine.Draw();

				StartCoroutine(DeleteLines(vectorLine));
			}
			catch (Exception e) 
			{ 
				Debug.LogError($"[Stapler]: {e}"); 
			}
		}

		IEnumerator DeleteLines(VectorLine vectorLine)
		{
			//yield return new WaitForEndOfFrame();

			yield return new WaitForSeconds(5);

			vectorLine.active = false;
			// Destroy all line GameObjects
		}

		/*void OnMouseOverPart(bool over)
		{
			foreach (var part in selectedParts)
			{
				part.mpb.SetColor(PropertyIDs._RimColor, over ? Color.clear : Color.white);
				part.GetPartRenderers().ToList().ForEach(r => r.SetPropertyBlock(part.mpb));
			}
		}*/


	}
}
