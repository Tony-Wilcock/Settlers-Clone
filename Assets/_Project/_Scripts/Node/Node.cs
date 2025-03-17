using UnityEngine;

namespace PunkyFruitBat
{
    public class Node : MonoBehaviour
    {
        public int vertexIndex;
        public Vector3 position;
        public bool hasFlag = false;
        public bool hasPath = false;
        public bool hasObstacle = false;
        public bool hasBuilding = false;
        public bool isCenterNode = false;
        public bool isEdgeNode = false;
    }
}
