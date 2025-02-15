using UnityEngine;
using System.Collections.Generic;
using TGS;

namespace TGSDemos {

    public class Demo10b : MonoBehaviour {

        TerrainGridSystem tgs;
        GUIStyle labelStyle;
        Rigidbody character;

        void Start() {
            tgs = TerrainGridSystem.instance;

            // setup GUI resizer - only for the demo
            GUIResizer.Init(800, 500);

            // setup GUI styles
            labelStyle = new GUIStyle();
            labelStyle.alignment = TextAnchor.MiddleLeft;
            labelStyle.normal.textColor = Color.black;

            character = GameObject.Find("Character").GetComponent<Rigidbody>();
        }

        void OnGUI() {
            // Do autoresizing of GUI layer
            GUIResizer.AutoResize();
            GUI.backgroundColor = new Color(0.8f, 0.8f, 1f, 0.5f);
            GUI.Label(new Rect(10, 5, 160, 30), "Move the ball with WASD and press G to reposition grid around it.", labelStyle);
            GUI.Label(new Rect(10, 25, 160, 30), "Press N to show neighbour cells around the character position.", labelStyle);
            GUI.Label(new Rect(10, 45, 160, 30), "Press C to snap to center of cell.", labelStyle);
            GUI.Label(new Rect(10, 65, 160, 30), "Open the Demo10b.cs script to learn how to assign gridCenter property using code.", labelStyle);
        }

        void Update() {

            // Move ball
            const float strength = 10f;
            if (tgs.input.GetKey("w")) {
                character.AddForce(Vector3.forward * strength);
            }
            if (tgs.input.GetKey("s")) {
                character.AddForce(Vector3.back * strength);
            }
            if (tgs.input.GetKey("a")) {
                character.AddForce(Vector3.left * strength);
            }
            if (tgs.input.GetKey("d")) {
                character.AddForce(Vector3.right * strength);
            }
            if (tgs.input.GetKeyDown("c")) {
                SnapToCellCenter();
            }

            // Reposition grid
            if (tgs.input.GetKeyDown("g")) {
                RepositionGrid();
            }

            // Show neighbour cells
            if (tgs.input.GetKeyDown("n")) {
                ShowNeighbours(character.transform.position);
            }

            // Position camera
            Camera.main.transform.position = character.transform.position + new Vector3(0, 20, -20);
            Camera.main.transform.LookAt(character.transform.position);

        }

        // Updates grid position around newPosition
        void RepositionGrid() {
            tgs.SetGridCenterWorldPosition(character.transform.position, true);
        }

        // Moves character to center of current cell
        void SnapToCellCenter() {
            Vector3 pos = tgs.SnapToCell(character.transform.position);
            // Shift pos a bit upwards
            pos -= tgs.transform.forward;
            character.transform.position = pos;
        }


        // Highlight neighbour cells around character posiiton
        void ShowNeighbours(Vector3 position) {
            Cell characterCell = tgs.CellGetAtWorldPosition(position);
            List<Cell> neighbours = tgs.CellGetNeighbours(characterCell);
            if (neighbours != null) {
                foreach (Cell cell in neighbours) {
                    tgs.CellFadeOut(cell, Color.red, 2.0f);
                }
            }
        }

    }

}