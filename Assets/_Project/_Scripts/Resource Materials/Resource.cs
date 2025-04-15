using UnityEngine;

namespace PunkyFruitBat
{
    // Make Resource NOT abstract if you intend to attach derived types like WoodResource, StoneResource etc.
    // If it's just a generic container, keep it abstract and ensure prefabs have derived scripts.
    // For simplicity now, let's make it non-abstract.
    public class Resource : MonoBehaviour // Changed from abstract for easier use if prefabs use this directly
    {
        [field: SerializeField] public ResourceType ResourceType { get; protected set; }
        [field: SerializeField] public int StartNodeIndex { get; protected set; } = -1; // HQ Center
        [field: SerializeField] public int DestinationNodeIndex { get; protected set; } = -1; // Building Center
        [field: SerializeField] public Flag CurrentFlag { get; set; } = null; // Flag that the resource is currently at

        /// <summary>
        /// Basic initialization for a resource.
        /// </summary>
        public void InitialiseResource(int startNode, int destinationNode, ResourceType type)
        {
            StartNodeIndex = startNode;
            DestinationNodeIndex = destinationNode;
            ResourceType = type;
            gameObject.name = $"{type}_To_{destinationNode}"; // Use destination node in name
        }

        public void ResetResource()
        {
            // Don't clear DestinationNodeIndex if pooling
            if (transform.parent != null) transform.SetParent(null);
        }
    }
}