using UnityEngine;
using System.Collections.Generic;

using TGS;

namespace TGSDemos {

    public class Demo24 : MonoBehaviour {
	
		TerrainGridSystem tgs;
		bool isSelectingStart;
		int cellStartIndex;
		GUIStyle labelStyle;

		// example enums for grouping cells
		const int CELLS_ALL = -1;
		const int CELL_DEFAULT = 1;
		const int CELL_PLAYER = 2;
		const int CELL_ENEMY = 4;
		const int CELL_OBSTACLE = 8;
		const int CELLS_ALL_NAVIGATABLE = ~(CELL_OBSTACLE | CELL_PLAYER | CELL_ENEMY);

		// Use this for initialization
		void Start () {
			tgs = TerrainGridSystem.instance;

			// setup GUI resizer - only for the demo
			GUIResizer.Init (800, 500); 
			labelStyle = new GUIStyle ();
			labelStyle.alignment = TextAnchor.MiddleLeft;
			labelStyle.normal.textColor = Color.white;

			isSelectingStart = true;

			// Draw some cells marked as can't by used by pathfinding
			for (int i = 0; i < 25; i++) {
				int row = Random.Range(2, tgs.rowCount - 3);
				int col = Random.Range(2, tgs.columnCount - 3);
				int cellIndex = tgs.CellGetIndex(row, col);
				tgs.CellSetCanCross(cellIndex, false);
				tgs.CellSetColor(cellIndex, Color.gray);
			}

			Random.InitState (2);

			// Draw some blocked areas
			for (int i = 0; i < 25; i++) {
				int row = Random.Range (2, tgs.rowCount - 3);
				int col = Random.Range (2, tgs.columnCount - 3);
				for (int j = -2; j <= 2; j++) {
					for (int k = -2; k <= 2; k++) {
						int cellIndex = tgs.CellGetIndex (row + j, col + k);
						tgs.CellSetGroup (cellIndex, CELL_OBSTACLE);
						tgs.CellToggleRegionSurface (cellIndex, true, Color.white);
					}
				}
			}

			// Example: sets crossing costs for hexagon sides and draw a line
			int barrierCost = 10000;
			Color barrierColor = Color.blue;
			float barrierWidth = 5f;
			for (int k = 0; k < 10; k++) {
				int cellIndex = tgs.CellGetIndex (10, k + 20);
				if (!tgs.cells [cellIndex].canCross)
					continue;
				if (tgs.cells [cellIndex].column % 2 == 0) {
					
					// Assign a crossing cost to barrier for path-finding purposes
					tgs.CellSetSideCrossCost (cellIndex, CELL_SIDE.Top, barrierCost);
					tgs.CellSetSideCrossCost (cellIndex, CELL_SIDE.TopLeft, barrierCost);
					tgs.CellSetSideCrossCost (cellIndex, CELL_SIDE.TopRight, barrierCost);

					// Make the barrier block LOS (Line-Of-Sight)
					tgs.CellSetSideBlocksLOS (cellIndex, CELL_SIDE.Top, true);
					tgs.CellSetSideBlocksLOS (cellIndex, CELL_SIDE.TopLeft, true);
					tgs.CellSetSideBlocksLOS (cellIndex, CELL_SIDE.TopRight, true);

					// Draw the barrier
					tgs.DrawLine (cellIndex, CELL_SIDE.Top, barrierColor, barrierWidth);
					tgs.DrawLine (cellIndex, CELL_SIDE.TopLeft, barrierColor, barrierWidth);
					tgs.DrawLine (cellIndex, CELL_SIDE.TopRight, barrierColor, barrierWidth);

				} else {
					// Assign a crossing cost to barrier for path-finding purposes
					tgs.CellSetSideCrossCost (cellIndex, CELL_SIDE.Top, barrierCost);
					// Make the barrier block LOS (Line-Of-Sight)
					tgs.CellSetSideBlocksLOS (cellIndex, CELL_SIDE.Top, true);
					// Draw the barrier
					tgs.DrawLine (cellIndex, CELL_SIDE.Top, barrierColor, barrierWidth);
				}
			}

			// Hook into cell click event to toggle start selection or draw the cells within the view cone
			tgs.OnCellClick += (grid, cellIndex, buttonIndex) => GetCellsInCone(cellIndex);
			tgs.OnCellEnter += (grid, cellIndex) => ShowLineOfSight (cellIndex);
		}

		void GetCellsInCone(int clickedCellIndex)
		{
			Debug.Log("Clicked on cell# " + clickedCellIndex);
			DestroyLOSMarkers();

			if (isSelectingStart)
			{
				// Selects start cell
				cellStartIndex = clickedCellIndex;
				tgs.CellToggleRegionSurface(cellStartIndex, true, Color.yellow);				
			}
			else
			{				
				// Clicked on the end cell, then show the cone
				// First clear color of start cell
				tgs.CellToggleRegionSurface(cellStartIndex, false, Color.white);				

				Vector2 startPos = tgs.cells[cellStartIndex].center;
				Vector2 endPos = tgs.cells[clickedCellIndex].center;
				Vector2 direction = endPos - startPos;

				float maxDistance = Vector2.Distance(startPos, endPos);
				direction /= maxDistance;

				// Get Cells within Cone				
				List<int> cellsInCone = new List<int>();
				// Set start cell, direction , distance and angle here
				tgs.CellGetWithinCone(cellStartIndex, direction, maxDistance, 60.0f, cellsInCone);

				// Color the cone
				if (cellsInCone != null)
				{					
					for (int k = 0; k < cellsInCone.Count; k++)
					{
						// If cell are not obstacles and can cross, flash magenta
						if (tgs.CellGetGroup(cellsInCone[k]) != CELL_OBSTACLE && tgs.CellGetCanCross(cellsInCone[k]))
						{
							tgs.CellFadeOut(cellsInCone[k], Color.magenta, 2f);
						}
					}
				}
			}

			isSelectingStart = !isSelectingStart;
		}

		void OnGUI () {
			// Do autoresizing of GUI layer
			GUIResizer.AutoResize ();
			
			if (isSelectingStart) {
				GUI.Label (new Rect (10, 10, 160, 30), "Select the starting cell", labelStyle);
			} else {
				GUI.Label (new Rect (10, 10, 160, 30), "Select the ending cell", labelStyle);
			}			
		}

		// LOS (Line of sight) example
		List<int> cellIndices;
		List<Vector3> worldPositions;
		List<GameObject> LOSMarkers;

		void ShowLineOfSight (int targetCellIndex) {
			if (isSelectingStart)
				return;
												
			// Destroys markers of a previous LOS
			DestroyLOSMarkers ();

			// Compute LOS and get list of cell indices and world positions
			bool isLOS = tgs.CellGetLineOfSight (cellStartIndex, targetCellIndex, ref cellIndices, ref worldPositions, CELLS_ALL_NAVIGATABLE);

			// Add small dots (spheres) along the LOS
			worldPositions.ForEach ((Vector3 obj) => {
				GameObject sphere =	GameObject.CreatePrimitive (PrimitiveType.Sphere);
				LOSMarkers.Add (sphere);
				sphere.transform.position = obj;
				sphere.transform.localScale = Vector3.one * 5f;
				sphere.GetComponent<Renderer> ().material.color = isLOS ? Color.green : Color.red;
			});
		}

		void DestroyLOSMarkers () {
			if (LOSMarkers == null) {
				LOSMarkers = new List<GameObject> ();
			} else {
				LOSMarkers.ForEach ((GameObject obj) => DestroyImmediate (obj));
			}
		}
	
	}
}