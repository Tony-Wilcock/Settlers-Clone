﻿using UnityEngine;
using TGS;

namespace TGSDemos {

    public class DemoOrtho : MonoBehaviour {

		TerrainGridSystem tgs;
		GUIStyle labelStyle;

		void Start () {
			tgs = TerrainGridSystem.instance;

			// setup GUI styles
			labelStyle = new GUIStyle ();
			labelStyle.alignment = TextAnchor.MiddleCenter;
			labelStyle.normal.textColor = Color.white;

			tgs.OnCellHighlight += (TerrainGridSystem grid, int cellIndex, ref bool cancelHighlight) => { 
				cancelHighlight = true; 
				tgs.CellFadeOut (cellIndex, Color.yellow, 2f); 
			};
			tgs.OnCellClick += (TerrainGridSystem grid, int cellIndex, int buttonIndex) => MergeCell (cellIndex);
        }

		void OnGUI () {
			GUI.Label (new Rect (0, 5, Screen.width, 30), "Try changing the grid properties in Inspector. You can click a cell to merge it.", labelStyle);
		}

		/// <summary>
		/// Merge cell example. This function will make cell1 marge with a random cell from its neighbours.
		/// </summary>
		void MergeCell (int cellIndex) {
			Cell cell1 = tgs.cells [cellIndex];
			int neighbourCount = cell1.neighbours.Count;
			if (neighbourCount == 0)
				return;
			Cell cell2 = cell1.neighbours [Random.Range (0, neighbourCount)];
			tgs.CellMerge (cell1, cell2);
			tgs.Redraw ();
		}

	}
}
