using UnityEngine;

namespace PunkyFruitBat
{
    public class Node : MonoBehaviour
    {
        [field: SerializeField] public int VertexIndex { get; private set; } = -1;
        [field: SerializeField] public Vector3 Position { get; private set; } = Vector3.zero;
        [field: SerializeField] public bool HasFlag { get; private set; } = false;
        [field: SerializeField] public bool HasPath { get; private set; } = false;
        [field: SerializeField] public bool HasBuilding { get; private set; } = false;
        [field: SerializeField] public bool HasObstacle { get; private set; } = false;
        [field: SerializeField] public bool IsCentreNode { get; private set; } = false;
        [field: SerializeField] public bool IsEdgeNode { get; private set; } = false;

        [SerializeField] private Flag flagOnNode;
        [SerializeField] private Path pathOnNode;
        [SerializeField] private Building buildingOnNode;

        public void SetVertexIndex(int vertexIndex)
        {
            this.VertexIndex = vertexIndex;
        }

        public void SetPosition(Vector3 position)
        {
            this.Position = position;
        }

        public void SetFlagOnNode(Flag flag)
        {
            flagOnNode = flag;
            HasFlag = true;
        }

        public void SetPathOnNode(Path path)
        {
            pathOnNode = path;
            HasPath = true;
        }

        public void SetBuildingOnNode(Building building)
        {
            buildingOnNode = building;
            HasBuilding = true;
        }

        public void SetObstacleOnNode(bool hasObstacle)
        {
            this.HasObstacle = hasObstacle;
        }

        public void SetCenterNode(bool isCentreNode)
        {
            this.IsCentreNode = isCentreNode;
        }

        public void SetEdgeNode(bool isEdgeNode)
        {
            this.IsEdgeNode = isEdgeNode;
        }

        public void RemoveFlagOnNode()
        {
            flagOnNode = null;
            HasFlag = false;
        }

        public void RemovePathOnNode()
        {
            pathOnNode = null;
            HasPath = false;
        }

        public void RemoveBuildingOnNode()
        {
            buildingOnNode = null;
            HasBuilding = false;
        }

        public int GetVertexIndex()
        {
            return VertexIndex;
        }

        public Vector3 GetPosition()
        {
            return Position;
        }

        public Flag GetFlagOnNode()
        {
            return HasFlag ? flagOnNode : null;
        }

        public Path GetPathOnNode()
        {
            return HasPath ? pathOnNode : null;
        }

        public Building GetBuildingOnNode()
        {
            return HasBuilding ? buildingOnNode : null;
        }
    }
}
