using CommNet.Network;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UniLinq;
using UnityEngine;
using static KSP.UI.Screens.RDNode;
using static PartModule;

namespace Stapler
{
	//[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class StaplerInflight : MonoBehaviour
	{
		// added dictionary to remember the parents of parts that are destroyed
		public Dictionary<uint, uint> deadPartsParents = new Dictionary<uint, uint>();

		private static readonly string[] PartTypesTriggeringUnwantedJointBreakEvents = new string[]
		{
			"decoupler",
			"separator",
			"docking",
			"grappling",
			"landingleg",
			"clamp",
			"gear",
			"wheel",
			"mast",
			"heatshield"
		};

		private static readonly string[] _PartTypesTriggeringUnwantedJointBreakEvents = new string[PartTypesTriggeringUnwantedJointBreakEvents.Length];

		public void Awake()
		{
			if (!HighLogic.LoadedSceneIsFlight) return;

			Debug.Log($"[Stapler]: Adding Connection Managers");
			Vessel launchedCraft = FlightGlobals.fetch.activeVessel;

			List<Part> Parts = launchedCraft.Parts;


			foreach (var Part in Parts)
			{
				//Part.gameObject.AddComponent<StaplePart>();
			}
		}

		//1553 void OnPartJointBreak(PartJoint j, float breakForce)
		public void Start()
		{
			Debug.Log($"[Stapler]: Events");
			GameEvents.onPhysicsEaseStop.Add(OnPhysicsEaseStop);
			GameEvents.onPartJointBreak.Add(OnPartJointBreak);
			GameEvents.onPartWillDie.Add(OnPartWillDie);
			GameEvents.onLevelWasLoadedGUIReady.Add(OnLevelLoaded);
			PartTypesTriggeringUnwantedJointBreakEvents.CopyTo(_PartTypesTriggeringUnwantedJointBreakEvents, 0);
		}

        private void OnPhysicsEaseStop(Vessel data)
		{
			Debug.Log($"[Stapler]: Physics Easing");
		}

        // this function was added as during this event the joints are still intact and we can remember the parent of the part that is going to die
        public void OnPartWillDie(Part data)
		{
			Debug.Log($"[Stapler]: Gonna Die Mate");
			if (!(data.localRoot == data))
			{
				if (!deadPartsParents.ContainsKey(data.flightID))
				{
					deadPartsParents.Add(data.flightID, data.parent.flightID);
				}

			}
			else
			{
				if (!deadPartsParents.ContainsKey(data.flightID))
				{
					deadPartsParents.Add(data.flightID, data.flightID);
				}
			}
		}

		// this function was added to clear the list of dead parts when a scene is loaded
		public void OnLevelLoaded(GameScenes data)
		{
			deadPartsParents.Clear();
		}

		public void OnPartJointBreak(PartJoint partJoint, float breakForce)
		{
			if (HighLogic.LoadedScene == GameScenes.EDITOR)
			{
				return;
			}
			Debug.Log($"[Stapler]: Gonna Snap Mate");
			if (partJoint.Target == null)
			{
				return;
			}
			if (partJoint.Target.PhysicsSignificance == 1)
			{
				return;
			}


			/*if (breakForce == 0)
			{
				// this checks if the joint connects a parent and a child (other cases are autostruts that we do not want)
				if (!(deadPartsParents.Contains(new KeyValuePair<uint, uint>(partJoint.Host.flightID, partJoint.Target.flightID)) || deadPartsParents.ContainsKey(partJoint.Target.flightID)))
				{
					return;
				}
			}*/

			// added this check because if a part dies that has a still intact child there will be 2 partjoint breaks one where the target that is destroyed and one where the host is destroyed
			// also expanded attach flames with this new parameter (basically we avoid attaching flames to a destroyed object that could lead to exceptions
			bool attachToHost = false;

			if (deadPartsParents.ContainsKey(partJoint.Target.flightID))
			{
				attachToHost = true;
			}

		}
		private static bool IsPartHostTypeAJointBreakerTrigger(string hostPartName)
		{
			return true;
			//return _PartTypesTriggeringUnwantedJointBreakEvents.Any(hostPartName.Contains);
		}
	}

	/*[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class StaplePart : Part
	{
		public override void onPartAwake()
		{
			Debug.Log($"[Stapler]: Staplepart Added");
			base.onPartAwake();		
		}

		public override void onPartExplode()
		{
			Debug.Log("I am stapler here to say; ouch");
			base.onPartExplode();
        }

        public override void onPartDestroy()
		{
			Debug.Log($"[Stapler]: destroyed");
			base.onPartDestroy();
        }

		public void OnDestroy()
		{
			Debug.Log($"[Stapler]: OnDestroy");
		}
    }*/
}
