using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Rendering;

namespace LayeredGlowSys {

    public class CameraDepthCapture : MonoBehaviour {

        public const string SHADER_Hidden_BlitToDepth = "Hidden/BlitToDepth";
        public static readonly int ID_CameraDepthTexture = Shader.PropertyToID("_CameraDepthTexture");

        public event System.Action DepthTextureOnblit;

        public RenderTexture depthTexture;

        protected Material matBlitToDepth;

        #region unity
        private void OnEnable() {
            matBlitToDepth = new Material(Shader.Find(SHADER_Hidden_BlitToDepth));

            var cam = GetComponent<Camera>();
            if (cam != null)
                cam.depthTextureMode |= DepthTextureMode.Depth;
        }
        private void OnDisable() {
            CoreUtils.Destroy(matBlitToDepth);
        }
        private void OnRenderImage(RenderTexture source, RenderTexture destination) {
            var cameraDepthTex = Shader.GetGlobalTexture(ID_CameraDepthTexture);

            if (cameraDepthTex == null)
                Debug.LogWarning("CameraDepthCapture: Camera depth texture is null");

            if (depthTexture != null) {
                Graphics.Blit(cameraDepthTex, depthTexture, matBlitToDepth);
                DepthTextureOnblit?.Invoke();
            }

            Graphics.Blit(source, destination);
        }
        #endregion

    }
}