using UnityEngine;
using TGS;

namespace TGSDemos {

    public class TiledTextures : MonoBehaviour {

        public Texture2D tex;

        TerrainGridSystem tgs;

        void Start () {

            tgs = TerrainGridSystem.instance;

            for (int k = 0; k < 150; k++) {
                int index = Random.Range(0, tgs.cells.Count);
                tgs.CellSetTexture(index, tex, isCanvasTexture: true, textureScale: 0.2f);
            }
        }
    }
}