using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

#if USES_URP
using UnityEngine.Rendering.Universal;
#endif

namespace TGS {

    [ExecuteAlways]
    public class TGSURPCameraSetup : MonoBehaviour {

#if USES_URP
        readonly Dictionary<ScriptableRendererFeature, bool> renderFeatures = new Dictionary<ScriptableRendererFeature, bool>();

        public void PreShot() {
            UniversalAdditionalCameraData camData = GetComponent<UniversalAdditionalCameraData>();
            if (camData != null) {
                camData.antialiasing = AntialiasingMode.None;
                camData.dithering = false;
                camData.renderPostProcessing = false;
                camData.renderShadows = false;
                camData.stopNaN = false;
                camData.volumeLayerMask = 0;
            }
            GetRenderFeatures();
            ToggleOffRenderFeatures();
        }

        public void PostShot() {
            RestoreRenderFeatures();
        }

        void GetRenderFeatures() {
            var asset = UniversalRenderPipeline.asset;
            if (asset == null) return;

            var type = asset.GetType();
            var fieldInfo = type.GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fieldInfo == null) return;
            var renderDatas = (ScriptableRendererData[])fieldInfo.GetValue(asset);
            if (renderDatas == null) return;

            foreach (var renderData in renderDatas) {
                foreach (var rendererFeature in renderData.rendererFeatures) {
                    if (rendererFeature != null) {
                        renderFeatures[rendererFeature] = rendererFeature.isActive;
                    }
                }
            }
        }

        void ToggleOffRenderFeatures() {
            foreach (var kvp in renderFeatures) {
                if (kvp.Key != null) {
                    kvp.Key.SetActive(false);
                }
            }
        }

        void RestoreRenderFeatures() {
            foreach (var kvp in renderFeatures) {
                if (kvp.Key != null) {
                    kvp.Key.SetActive(kvp.Value);
                }
            }
        }
#else
    public void PreShot() {}
    public void PostShot() {}
#endif

    }
}
