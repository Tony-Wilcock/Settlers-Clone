using UnityEngine;

public class DynamicTexturePainter : MonoBehaviour
{
    public Chunk targetObject; // The object to paint on
    public Texture2D paintTexture; // The texture to paint
    public float paintRadius = 0.5f; // Radius of the paint brush

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // Left mouse click
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (targetObject.chunkObject == hit.transform)
                {
                    // Get the UV coordinates of the hit point
                    Vector2 uv = hit.textureCoord;

                    // Modify the texture (example: blit a circle)
                    RenderTexture tempRenderTexture = new RenderTexture(paintTexture.width, paintTexture.height, 24);
                    Graphics.Blit(paintTexture, tempRenderTexture);

                    // Set the texture to the material
                    targetObject.chunkObject.GetComponent<Renderer>().material.mainTexture = paintTexture;
                }
            }
        }
    }
}
