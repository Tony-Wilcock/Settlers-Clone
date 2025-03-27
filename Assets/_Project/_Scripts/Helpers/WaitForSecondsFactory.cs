using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public static class WaitForSecondsFactory
    {
        private static readonly Dictionary<float, WaitForSeconds> _pool = new Dictionary<float, WaitForSeconds>();

        public static WaitForSeconds Get(float seconds)
        {
            if (!_pool.TryGetValue(seconds, out var waitForSeconds))
            {
                waitForSeconds = new WaitForSeconds(seconds);
                _pool.Add(seconds, waitForSeconds);
            }
            return waitForSeconds;
        }

        public static IEnumerator WaitCoroutine(float seconds)
        {
            yield return Get(seconds);
        }
    }
}
