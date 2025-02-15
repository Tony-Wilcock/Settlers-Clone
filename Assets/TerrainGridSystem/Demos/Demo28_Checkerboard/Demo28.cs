using UnityEngine;
using System.Collections.Generic;

using TGS;

namespace TGSDemos {

    public class Demo28 : MonoBehaviour {
        public Texture2D whiteCell, blackCell;
        public Sprite pieceSprite;

        TerrainGridSystem grid;
        GUIStyle labelStyle;
        GameObject piece;

        void Start() {
            // setup GUI styles
            labelStyle = new GUIStyle();
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.normal.textColor = Color.black;

            // Get a reference to Grids 2D System's API
            grid = TerrainGridSystem.instance;

            // create checker board
            CreateBoard();

            // create piece sprite and move it to center position
            CreatePiece();

            // listen to click events
            grid.OnCellClick += Grid_OnCellClick;

        }

        private void Grid_OnCellClick(TerrainGridSystem tgs, int cellIndex, int buttonIndex) {
            MovePiece(cellIndex);
        }

        void Update() {
            if (Input.GetKeyDown(KeyCode.Space)) {
                grid.MovePauseToggle(piece);
            }
        }

        /// <summary>
        /// Creates a classic black and white checkers board
        /// </summary>
        void CreateBoard() {
            grid.gridTopology = GridTopology.Box;
            grid.rowCount = 8;
            grid.columnCount = 8;

            bool even = false;
            for (int row = 0; row < grid.rowCount; row++) {
                even = !even;
                for (int col = 0; col < grid.columnCount; col++) {
                    even = !even;
                    Cell cell = grid.CellGetAtPosition(col, row);
                    int cellIndex = grid.CellGetIndex(cell);
                    if (even) {
                        grid.CellToggleRegionSurface(cellIndex, true, whiteCell);
                    } else {
                        grid.CellToggleRegionSurface(cellIndex, true, blackCell);
                    }
                }
            }
        }

        /// <summary>
        /// Creates the game object for the piece -> loads the texture, creates an sprite, assigns the texture and position the game object in world space over the center cell.
        /// </summary>
        void CreatePiece() {
            // Creates the piece
            piece = new GameObject("Piece");
            SpriteRenderer spriteRenderer = piece.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = pieceSprite;

            // Parents the piece to the grid and sets scale
            piece.transform.SetParent(grid.transform, false);
            piece.transform.localScale = Vector3.one * 0.25f;

            // Get central cell of checker board
            Cell centerCell = grid.CellGetAtPosition(4, 4);
            int centerCellIndex = grid.CellGetIndex(centerCell);
            grid.MoveTo(piece, centerCellIndex);
        }

        private void OnCellMove(GameObject piece, Vector3 destination, int pathIndex, List<int> path) {
            int currentCellIndex = grid.CellGetAtWorldPosition(piece.transform.position).index;
            int nextCellIndex = path[pathIndex];
            Debug.Log("Piece moved at step " + pathIndex + " of path. Destination pos: " + destination + ". Current: " + grid.cells[currentCellIndex].coordinates + ". Next: " + grid.cells[nextCellIndex].coordinates);
        }


        /// <summary>
        /// Moves the piece to the center position of the cell specified by cell index.
        /// </summary>
        /// <param name="cellIndex">Cell index.</param>
        void MovePiece(int destinationCellIndex) {
            // Gets current cell under piece
            Cell currentCell = grid.CellGetAtWorldPosition(piece.transform.position);
            int currentCellIndex = grid.CellGetIndex(currentCell);

            // Obtain a path from current cell to destination
            List<int> positions = grid.FindPath(currentCellIndex, destinationCellIndex);

            // Move along those positions
            GridMove moveToCommand = grid.MoveTo(piece, positions, velocity: 2f);
            moveToCommand.OnCellMove += OnCellMove;
            moveToCommand.OnMoveEnd += (gameObject) => { Debug.Log("Piece moved to " + grid.cells[destinationCellIndex].coordinates); };
        }


    }
}
