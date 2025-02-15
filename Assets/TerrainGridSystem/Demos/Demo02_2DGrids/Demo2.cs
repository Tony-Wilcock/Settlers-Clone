using UnityEngine;
using System.Text;
using TGS;

namespace TGSDemos {


    public class Demo2 : MonoBehaviour {

		TerrainGridSystem tgs;
		GUIStyle labelStyle;
		string message = "";
        readonly StringBuilder sb = new StringBuilder();

        void Start () {
			tgs = TerrainGridSystem.instance;

			// setup GUI styles
			labelStyle = new GUIStyle ();
			labelStyle.alignment = TextAnchor.UpperLeft;
			labelStyle.normal.textColor = Color.black;

			// Events
			// OnCellMouseDown occurs when user presses the mouse button on a cell
			tgs.OnCellMouseDown += OnCellMouseDown;
			// OnCellMouseUp occurs when user releases the mouse button on a cell even after a drag
			tgs.OnCellMouseUp += OnCellMouseUp;
			// OnCellClick occurs when user presses and releases the mouse button as in a normal click
			tgs.OnCellClick += OnCellClick;

			// Example code: draw disputing frontier between territories 0 and 3 in yellow
			//			tgs.TerritoryDrawFrontier(0, 3, null, Color.yellow);

		}

        void OnCellMouseDown (TerrainGridSystem grid, int cellIndex, int buttonIndex) {
			AddMessage("Mouse DOWN on cell #" + cellIndex);
		}

		void OnCellMouseUp(TerrainGridSystem grid, int cellIndex, int buttonIndex) {
			AddMessage("Mouse UP on cell #" + cellIndex);
		}

		void OnCellClick (TerrainGridSystem grid, int cellIndex, int buttonIndex) {
			if (buttonIndex == 0) {
				AddMessage("Mouse CLICK on cell #" + cellIndex + ", Merging!");
				MergeCell (tgs.cellHighlighted);
			} else if (buttonIndex == 1) {
				AddMessage("Right clicked on cell #" + cellIndex);
			}												
		}

		void OnGUI () {
			GUI.Label (new Rect (0, 5, Screen.width, 30), "Try changing the grid properties in Inspector. You can click a cell to merge it.", labelStyle);
			GUI.Label(new Rect(0, 35, Screen.width, 60), message, labelStyle);
		}

		void AddMessage(string text) {
			if (sb.Length > 200) sb.Length = 0;
			sb.AppendLine(text);
			message = sb.ToString();
			Debug.Log(text);
        }

		/// <summary>
		/// Merge cell example. This function will make cell1 marge with a random cell from its neighbours.
		/// </summary>
		void MergeCell (Cell cell1) {
			int neighbourCount = cell1.neighbours.Count;
			if (neighbourCount == 0)
				return;
			Cell cell2 = cell1.neighbours [Random.Range (0, neighbourCount)];
			tgs.CellMerge (cell1, cell2);
			tgs.Redraw ();
		}

    }
}
