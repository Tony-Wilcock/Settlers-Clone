using UnityEngine;
using TGS;

namespace TGSDemos {

    public class Demo4b : MonoBehaviour {

		TerrainGridSystem tgs;
		GUIStyle labelStyle;
		public Texture2D texture;

		void Start () {
			// setup GUI styles
			labelStyle = new GUIStyle ();
			labelStyle.alignment = TextAnchor.MiddleCenter;
			labelStyle.normal.textColor = Color.black;

			// Get a reference to Terrain Grid System's API
			tgs = TerrainGridSystem.instance;

			// assign a canvas (background) texture
			Texture2D tex = Resources.Load<Texture2D>("Textures/worldMap");
            tgs.canvasTexture = tex;

			// listen to click event and implement territory coloring
			tgs.OnTerritoryClick += (TerrainGridSystem grid, int territoryIndex, int regionIndex, int buttonIndex) => {
				// Color clicked territory in white
				tgs.TerritoryToggleRegionSurface (territoryIndex, visible: true, Color.white, refreshGeometry: false, tex);
			};

		}

		void OnGUI () {
			GUI.Label (new Rect (0, 5, Screen.width, 30), "Click on any position to reveal part of the canvas texture.", labelStyle);
		}

	}
}
