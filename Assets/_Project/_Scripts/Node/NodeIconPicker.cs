using System;
using UnityEngine;

namespace PunkyFruitBat
{
    public enum NodeIconIndex
    {
        None = 0,
        Flag = 1,
        SmallBuilding = 2,
        MediumBuilding = 3,
        LargeBuilding = 4,
        Resource = 5
    }

    [Serializable]
    public class NodeIconPicker
    {
        private HexGridManager manager;
        private GameObject[] icons;
        private int currentIconIndex = (int)NodeIconIndex.None;

        public void Initialise(HexGridManager manager, IconPrefabs_SO icons_SO)
        {
            this.manager = manager;

            if (icons_SO == null || icons_SO.iconPrefabs == null || icons_SO.iconPrefabs.Length < 1)
            {
                Debug.LogError("IconPrefabs array is null! Please assign prefabs in the Inspector.");
                return; // Exit the constructor if IconPrefabs is null
            }

            icons = new GameObject[icons_SO.iconPrefabs.Length];

            for (int i = 0; i < icons_SO.iconPrefabs.Length; i++)
            {
                if (icons_SO.iconPrefabs[i] != null)
                {
                    icons[i] = UnityEngine.Object.Instantiate(icons_SO.iconPrefabs[i]);
                    icons[i].transform.SetParent(manager.NodeIconsTransform);
                    icons[i].SetActive(false);
                }
                else
                {
                    Debug.LogError($"NodeSelection: IconPrefabs[{i}] is null!");
                }
            }

            manager.NodeManager.OnNearestVertexUpdated += HandleNearestVertexUpdated;
            manager.PathManager.PathBuilder.OnPathCancelled += DeactivateAllIcons;
            manager.FlagManager.OnFlagPlaced += DeactivateAllIcons;
            manager.FlagManager.OnFlagRemoved += DeactivateAllIcons;
        }

        private void HandleNearestVertexUpdated(int index)
        {
            if (manager.UIManager.AreAnyPanelsActive()) return;
            if (index < 0 || index >= manager.globalVertices.Length)
            {
                DeactivateAllIcons();
                return;
            }

            DetermineIconToPlace(index);
            SetActiveIcon(manager.globalVertices[index]);
        }

        private void DetermineIconToPlace(int index)
        {
            currentIconIndex = (int)NodeIconIndex.None;
        }

        private void SetActiveIcon(Vector3 position)
        {
            if (manager.CameraManager.IsCameraMoving || manager.CameraManager.IsCameraRotating || manager.CameraManager.IsCameraZooming || manager.CameraManager.IsDraggingForMovement || manager.CameraManager.IsDraggingForRotation)
            {
                DeactivateAllIcons();
                return;
            }

            DeactivateAllIcons();

            icons[currentIconIndex].SetActive(true);
            icons[currentIconIndex].transform.position = position;
        }

        private bool IsValidIconIndex(int index) => index >= 0 && index < icons.Length;

        private void DeactivateAllIcons()
        {
            for (int i = 0; i < icons.Length; i++)
            {
                icons[i].SetActive(false); // Deactivate all icons
            }
        }

        public void Unsubscribe()
        {
            manager.NodeManager.OnNearestVertexUpdated -= HandleNearestVertexUpdated;
            manager.PathManager.PathBuilder.OnPathCancelled -= DeactivateAllIcons;
            manager.FlagManager.OnFlagPlaced -= DeactivateAllIcons;
            manager.FlagManager.OnFlagRemoved -= DeactivateAllIcons;
        }
    }
}