using System;
using UnityEngine;

namespace PunkyFruitBat
{
    public partial class EventHandlingManager
    {
        public event Action OnPorterTaskComplete;

        public void OnPorterTaskCompleted()
        {
            OnPorterTaskComplete?.Invoke();
        }
    }
}
