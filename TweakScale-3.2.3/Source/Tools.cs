using JetBrains.Annotations;
using KSP.UI;
using Smooth.Algebraics;
using System;
using System.Collections;
using System.Collections.Generic;
//using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Vectrosity;

namespace Stapler
{
	/// <summary>
	/// Various handy functions.
	/// </summary>
	public static class Tools
	{
		/// <summary>
		/// Gets the exponentValue in <paramref name="values"/> that's closest to <paramref name="x"/>.
		/// </summary>
		/// <param name="x">The exponentValue to find.</param>
		/// <param name="values">The values to look through.</param>
		/// <returns>The exponentValue in <paramref name="values"/> that's closest to <paramref name="x"/>.</returns>

		// MOST of the time, if parent isn't null, potentialParent will be equal to parent.
		// But after re-rooting, parent can be non-null and potentialParent is null
		public static Part ParentSafe(this Part part)
		{
			return part.potentialParent != null ? part.potentialParent : part.parent;
		}

		// add events
		static Part GetAttachedPart(AttachNode node)
		{
			if (node.attachedPart != null) return node.attachedPart;
			var editorAttachment = EditorLogic.fetch?.attachment;
			if (editorAttachment != null && editorAttachment.callerPartNode == node)
			{
				return editorAttachment.potentialParent;
			}
			return null;
		}

		/// <summary>
		/// Checks 
		/// </summary>
		/// <returns>A boolean true if tweakscale is active in the same game.</returns>
		public static bool isTweakscaleInstalled()
		{
			//TODO: Use to adjust button position and hotkey 
			return false;
		}

		
	}
}
