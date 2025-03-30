using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PunkyFruitBat
{
    public abstract class Resource : MonoBehaviour
    {
        [field: SerializeField] public ResourceType ResourceType { get; protected set; }
        [field: SerializeField] public int StartNodeIndex { get; protected set; } = -1;
        [field: SerializeField] public int DestinationNodeIndex { get; protected set; } = -1;
        [field: SerializeField] public List<Flag> FlagsAlongRoute { get; protected set; }
        [field: SerializeField] public Flag CurrentFlag { get; protected set; } = null;
        [field: SerializeField] public Flag NextFlag { get; protected set; } = null;

        protected virtual void Awake()
        {
            // Initialise the list
            FlagsAlongRoute = new();
        }

        public void SetResource(int currentIndex, int destinationIndex, List<Flag> allFlags)
        {
            StartNodeIndex = currentIndex;
            DestinationNodeIndex = destinationIndex;
            FlagsAlongRoute.Clear();
            FlagsAlongRoute.AddRange(allFlags);
        }

        public void UpdateResource()
        {
            CurrentFlag = FlagsAlongRoute[0];
            if (CurrentFlag == FlagsAlongRoute.Last())
            {
                // Get the building attached to the flag
                Building building = CurrentFlag.GetBuildingAtFlag();
                building.AddResourceToSite(ResourceType, 1);
                ResetResource();
                return;
            }

            NextFlag = FlagsAlongRoute[1];
            FlagsAlongRoute.RemoveAt(0);
        }

        public void ResetResource()
        {
            StartNodeIndex = -1;
            DestinationNodeIndex = -1;
            FlagsAlongRoute.Clear();
            CurrentFlag = null;
            NextFlag = null;
        }
    }
}
