using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public abstract class Building : MonoBehaviour
    {
        [field: SerializeField] protected BuildingType buildingType;
        [field: SerializeField] protected BuildingSize buildingSize;
        [field: SerializeField] protected Transform buildingGFXTransform;
        [field: SerializeField] protected int centerIndex;
        [field: SerializeField] protected int entranceIndex;
        [field: SerializeField] protected int[] reservedNodes;

        public BuildingType BuildingType => buildingType;
        public BuildingSize BuildingSize => buildingSize;
        public Transform BuildingGFXTransform => buildingGFXTransform;
        public int CenterIndex => centerIndex;
        public int EntranceIndex => entranceIndex;
        public int[] ReservedNodes => reservedNodes;

        protected HexGridManager manager;
        protected BuildingManager buildingManager;

        public void InitialiseBuild(HexGridManager manager, BuildingManager buildingManager, BuildingType buildingType, int centerIndex)
        {
            this.manager = manager;
            this.buildingManager = buildingManager;
            this.buildingType = buildingType;
            this.buildingSize = buildingManager.GetBuildingSize(buildingType);
            this.buildingGFXTransform = transform.GetChild(0);
            this.centerIndex = centerIndex;
            this.entranceIndex = manager.NodeManager.GetNeighbourInDirection(centerIndex, Direction.Southeast);
            this.reservedNodes = buildingManager.GetReservedNodes(centerIndex, buildingSize);

            Build(manager, buildingType, centerIndex);
        }

        private void Build(HexGridManager manager, BuildingType buildingType, int centerIndex)
        {
            gameObject.SetActive(true);
            Debug.Log("Building " + buildingType + " at " + centerIndex);
            manager.FlagManager.PlaceFlag(entranceIndex, true);
            DrawPathVisual(entranceIndex, centerIndex);
            manager.UIManager.HideAllPanels();

            foreach (int nodeIndex in reservedNodes)
            {
                Node node = manager.NodeManager.GetNode(nodeIndex);
                node.hasBuilding = true;
            }
        }

        private void DrawPathVisual(int start, int end)
        {
            Node startNode = HexGridManager.Instance.NodeManager.GetNode(start);
            Node endNode = HexGridManager.Instance.NodeManager.GetNode(end);
            // Get the Y angle between the startNode and the endNode with 0 being the forward direction
            float angle = Vector3.SignedAngle(Vector3.forward, endNode.transform.position - startNode.transform.position, Vector3.up);

            Vector3 position = startNode.transform.position;

            // Get a path visual from the pool and spawn at the start position with the angle between the start and end nodes
            GameObject visual = HexGridManager.Instance.PathManager.GetPathVisualsFromPool();
            visual.transform.SetPositionAndRotation(position, Quaternion.Euler(0, angle, 0));
            visual.transform.SetParent(transform);
        }
    }
}
