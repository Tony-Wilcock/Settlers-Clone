using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public class StorehousePorter : Character
    {
        public List<Resource> ResourcesToCollect { get; private set; } = new();

        override protected void Awake()
        {
            base.Awake();
        }

        public void AddResourceToQueue(Resource resource)
        {
            ResourcesToCollect.Add(resource);
        }

        public void RemoveResourceFromQueue(Resource resource)
        {
            ResourcesToCollect.Remove(resource);
        }

        public void CollectResources()
        {
            foreach (Resource resource in ResourcesToCollect)
            {
                resource.UpdateResource();
            }
        }
    }
}
