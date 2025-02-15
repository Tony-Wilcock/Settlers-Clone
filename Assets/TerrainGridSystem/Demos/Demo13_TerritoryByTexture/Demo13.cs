using UnityEngine;
using TGS;

namespace TGSDemos {

    public class Demo13 : MonoBehaviour {

        TerrainGridSystem tgs;

        void Start() {
            tgs = TerrainGridSystem.instance;
            tgs.OnTerritoryClick += Tgs_OnTerritoryClick;

            // Mark the centroid of each territory region (the centroid always lays inside the polygon)
            foreach(Territory territory in tgs.territories) {
                if (!territory.isEmpty) {
                    Vector2 centroid = territory.GetCentroid();
                    GameObject o = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    o.transform.position = tgs.transform.TransformPoint(centroid);
                    o.transform.localScale = Vector3.one * 10f;
                }
            }
        }

        private void Tgs_OnTerritoryClick(TerrainGridSystem tgs, int territoryIndex, int regionIndex, int buttonIndex) {
            Debug.Log("Hiding territory index " + territoryIndex);
            tgs.TerritorySetVisible(territoryIndex, false);
        }

    }

}