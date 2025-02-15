using System;
using UnityEngine;


namespace TGS {

    public static class Misc {

		public static Vector4 Vector4zero = Vector4.zero;

		public static Vector3 Vector3zero = Vector3.zero;
		public static Vector3 Vector3one = Vector3.one;
		public static Vector3 Vector3up = Vector3.up;

		public static Vector2 Vector2left = Vector2.left;
		public static Vector2 Vector2right = Vector2.right;
		public static Vector2 Vector2one = Vector2.one;
		public static Vector2 Vector2zero = Vector2.zero;
		public static Vector2 Vector2down = Vector2.down;
		public static Vector2 Vector2half = Vector2.one * 0.5f;

		public static Color ColorNull = new Color (0, 0, 0, 0);

		public static Bounds BoundsZero = new Bounds();

        public static int FastConvertToInt(string s) {
            int value = 0;
            int start, sign;
            if (s[0] == '-') {
                start = 1;
                sign = -1;
            } else {
                start = 0;
                sign = 1;
            }
            for (int i = start; i < s.Length; i++) {
                value = value * 10 + (s[i] - '0');
            }
            return value * sign;
        }


        public static T FindObjectOfType<T>(bool includeInactive = false) where T : UnityEngine.Object {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindAnyObjectByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#else
            return UnityEngine.Object.FindObjectOfType<T>(includeInactive);
#endif
        }

        public static UnityEngine.Object[] FindObjectsOfType(Type type, bool includeInactive = false) {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType(type, includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            return UnityEngine.Object.FindObjectsOfType(type, includeInactive);
#endif
        }


        public static T[] FindObjectsOfType<T>(bool includeInactive = false) where T : UnityEngine.Object {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            return UnityEngine.Object.FindObjectsOfType<T>(includeInactive);
#endif
        }
    }

}