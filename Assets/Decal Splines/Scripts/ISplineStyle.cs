using UnityEditor;
using UnityEngine;

namespace DecalSplines
{
#if UNITY_EDITOR || DECALSPLINES_ALLOW_RUNTIME
    public class ISplineStyle: ScriptableObject
    {
        [SerializeField] private string styleName;
        [SerializeField] private float width;
        [SerializeField] private float fixedLength;
        [SerializeField][Range(0, 10)] private float curviness;
        [SerializeField] private bool freeHandles;


        public string StyleName { get { return styleName; } }
        public float Width { get { return width; } }
        public float FixedLength { get { return fixedLength; } }
        public float Curviness { get { return curviness; } }
        public bool FreeHandles { get { return freeHandles; } }


        public bool IsFixedLenght()
        {
            return (fixedLength > 0);
        }

#if UNITY_EDITOR
        public static ISplineStyle FindStyleAsset(string fileName)
        {
            if (Application.isPlaying)
                Debug.Log("FindStyleAsset can only be used in the Editor, it won't work in game. Use a direct refference of a style asset instead.");
            string searchFilter = $"\"{fileName}\" , t: {typeof(ISplineStyle).Name}";
            string[] guids = AssetDatabase.FindAssets(searchFilter);

            ISplineStyle result = null;
            if (guids.Length > 0)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                if (assetPath.Length > 0)
                {
                    result = (ISplineStyle)AssetDatabase.LoadAssetAtPath(assetPath, typeof(ISplineStyle));
                }
            }
            return result;
        }
#endif
    }
#endif
}
