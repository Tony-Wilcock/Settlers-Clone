using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TGS {

    public class MaterialPool {

        readonly List<Material> pool = new List<Material>();

        public Material Get(Material mat) {
            if (pool.Count > 0) {
                Material pooledObject = pool[0];
                pool.RemoveAt(0);
                return pooledObject;
            } else {
                Material newObject = new Material(mat);
                return newObject;
            }
        }

        public void Return(Material o) {
            pool.Add(o);
        }

        public void Release() {
            foreach (Material mat in pool) {
                Object.DestroyImmediate(mat);
            }
        }
    }
}