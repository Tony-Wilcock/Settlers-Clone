using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public class Path
    {
        [field: SerializeField] public int Id { get; private set; }
        [field: SerializeField] public Flag Flag1 { get; private set; }
        [field: SerializeField] public Flag Flag2 { get; private set; }
        [field: SerializeField] public List<int> Nodes { get; private set; } = new List<int>();
        [field: SerializeField] public int CenterNode { get; private set; }
        [field: SerializeField] public bool HasCarrier { get; private set; } = false;
        [field: SerializeField] public Carrier Carrier { get; private set; }
        [field: SerializeField] public List<GameObject> PathVisuals { get; private set; } = new List<GameObject>();

        //private void Awake()
        //{
        //    gameObject.SetActive(false);
        //}

        public Path(Flag flag1, Flag flag2, List<int> nodes, int id)
        {
            Flag1 = flag1;
            Flag2 = flag2;
            Nodes.AddRange(nodes);
            Id = id;

            CalculateCenterNode();
            DrawPath();
        }

        private void CalculateCenterNode()
        {
            // Get the index of the center node
            int midIndex = Nodes.Count / 2;
            CenterNode = Nodes[midIndex];
        }

        private void DrawPath()
        {
            for (int i = 0; i < Nodes.Count - 1; i++)
            {
                int startNode = Nodes[i];
                int endNode = Nodes[i + 1];
                DrawPathVisuals(startNode, endNode);
            }
        }

        private void DrawPathVisuals(int start, int end)
        {
            Node startNode = HexGridManager.Instance.NodeManager.GetNode(start);
            Node endNode = HexGridManager.Instance.NodeManager.GetNode(end);
            // Get the Y angle between the startNode and the endNode with 0 being the forward direction
            float angle = Vector3.SignedAngle(Vector3.forward, endNode.transform.position - startNode.transform.position, Vector3.up);

            Vector3 position = startNode.transform.position;

            // Get a path visual from the pool and spawn at the start position with the angle between the start and end nodes
            GameObject visual = HexGridManager.Instance.PathManager.GetPathVisualsFromPool();
            visual.transform.SetPositionAndRotation(position, Quaternion.Euler(0, angle, 0));
            PathVisuals.Add(visual);
        }

        private void ClearPathVisuals()
        {
            foreach (GameObject pathVisual in PathVisuals)
            {
                HexGridManager.Instance.PathManager.ReturnPathVisualsToPool(pathVisual);
            }
            PathVisuals.Clear();
        }

        public void OnPathRemoved()
        {
            ClearPathVisuals();

            Id = -1;
            CenterNode = -1;
            Nodes.Clear();
            Flag1 = null;
            Flag2 = null;
        }

        public void SetCarrier(Carrier carrier)
        {
            HasCarrier = true;
            Carrier = carrier;
        }

        public void RemoveCarrier()
        {
            HasCarrier = false;
            Carrier = null;
        }

        public List<int> GetNodes => Nodes;
    }
}
