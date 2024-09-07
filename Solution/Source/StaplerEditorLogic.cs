using Expansions.Serenity;
using KSP.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Stapler
{
	[KSPAddon(KSPAddon.Startup.EditorAny, false)]
	internal class StaplerEditorLogic : SingletonBehavior<StaplerEditorLogic>
	{
		public KeyCode ScaleModeKey { get; private set; }

		void Start()
		{
			// todo: find index in editor widget row and use that as keybind.
			ScaleModeKey = (Tools.isTweakscaleInstalled()) ? KeyCode.Alpha6 : KeyCode.Alpha5;

			EditorLogic.fetch.toolsUI.gameObject.AddComponent<ConstructionModeReparent>();
		}

		void Update()
		{
			if (EditorLogic.fetch.AnyTextFieldHasFocus() || DeltaVApp.AnyTextFieldHasFocus() || RoboticControllerManager.AnyWindowTextFieldHasFocus())
			{
				return;
			}
		}
	}
}
