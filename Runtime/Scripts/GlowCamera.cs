using nobnak.Gist;
using nobnak.Gist.Cameras;
using nobnak.Gist.Compute.Blurring;
using nobnak.Gist.Events;
using nobnak.Gist.ObjectExt;
using nobnak.Gist.Scoped;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LayeredGlowSys {

    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class GlowCamera : MonoBehaviour {

        public enum OverlayMode { None = 0, Glow, Threshold, Blurred }
        public enum ShaderPass { Threshold = 0, Additive, Overlay }
        public enum KeywordLum { ___ = 0, LUM_AVERAGE, LUM_VALUE }
        public const string PATH = "GlowCamera-GlowEffect";
        public const string TAG_UNTAGGED = "Untagged";
        public static readonly int P_INTENSITY = Shader.PropertyToID("_Intensity");
        public static readonly int P_THREAHOLD = Shader.PropertyToID("_Threshold");
        public TextureEvent GlowTexOnCreate;

        [SerializeField]
        protected DataSet dataset = new DataSet();
        [SerializeField]
        protected References link = new References();

        protected Camera attachedCam;
        protected RenderTexture mainTex;
        protected Material mat;
        protected Blur blur;
        protected Workspace[] workspaces = new Workspace[0];

        protected CameraData cameraData;
        protected Validator validator = new Validator();

        #region unity
        void OnEnable() {
            blur = new Blur();
            mat = Resources.Load<Material>(PATH);

            validator.Reset();
            validator.SetCheckers(() => {
                var valid = attachedCam != null && cameraData.Equals(attachedCam);
                if (!valid)
                    cameraData = attachedCam;
                return valid;
            });
            validator.Validation += () => {
                Debug.Log($"{this.GetType().Name} : Validation");
				if (link.mainCam == null) {
					link.mainCam = Camera.main;
				}

                if (attachedCam == null) {
                    attachedCam = GetComponent<Camera>();
                }
                var attachedTargetTexture = attachedCam.targetTexture;
                attachedCam.CopyFrom(link.mainCam);
                attachedCam.tag = TAG_UNTAGGED;
                attachedCam.targetTexture = attachedTargetTexture;
                attachedCam.cullingMask = 0;
                attachedCam.clearFlags = CameraClearFlags.Nothing;
                attachedCam.enabled = true;
                attachedCam.useOcclusionCulling = false;
                attachedCam.depth = link.mainCam.depth + 2;
                cameraData = attachedCam;

                var w = attachedCam.pixelWidth;
                var h = attachedCam.pixelHeight;

                if (NeedResize(mainTex, w, h)) {
                    ResetAllTargetTextures();
                    link.mainCam.targetTexture = Resize(ref mainTex, w, h, 24);
                }

                ResizeWorkspaces();

                UpdateWorkspaces(mainTex);

            };
        }

        void OnDisable() {
            if (blur != null) {
                blur.Dispose();
                blur = null;
            }
            ResetWorkspaces();
            ReleaseTextures();
            validator.Invalidate();
        }

        void OnValidate() {
            validator.Invalidate();
        }
        void Update() {
            validator.Validate();
        }
        void OnRenderImage(RenderTexture source, RenderTexture destination) {
            validator.Validate();
            if (link.mainCam == null) {
                Graphics.Blit(source, destination);
                return;
            }
            Graphics.Blit(mainTex, destination);

            for (var i = 0; i < workspaces.Length; i++) {
                var d = dataset.datas[i];
                var ws = workspaces[i];
                ws.UpdateBlurTex(d, blur, source, mat);
                Graphics.Blit(ws.blurred, destination, mat, (int)ShaderPass.Additive);
            }

            var debugLayerIsVisible = Camera.allCameras
                .Where(v => v.isActiveAndEnabled)
                .Any(v => (v.cullingMask & dataset.commons.debugLayer.value) != 0)
                && (dataset.commons.overlayIndex >= 0 && dataset.commons.overlayIndex < workspaces.Length);
            if (debugLayerIsVisible) {
                var targetIndex = dataset.commons.overlayIndex;
                var ws = workspaces[targetIndex];
                var d = dataset.datas[targetIndex];
                var tex = default(Texture);
                switch (dataset.commons.overlayMode) {
                    case OverlayMode.Glow:
                        tex = ws.glowTex;
                        break;
                    case OverlayMode.Threshold:
                        tex = ws.thresholdTex;
                        break;
                    case OverlayMode.Blurred:
                        tex = ws.blurred;
                        break;
                }
                if (tex != null) {
                    var gap = 10f;
                    var height = dataset.commons.overlayHeight * source.height;
                    var aspect = (float)ws.glowTex.width / ws.glowTex.height;

                    GL.PushMatrix();
                    GL.LoadPixelMatrix();
                    using (new RenderTextureActivator(destination)) {
                        Graphics.DrawTexture(new Rect(gap, gap + height, height * aspect, -height),
                                tex, mat, (int)ShaderPass.Overlay);
                    }
                    GL.PopMatrix();
                }
            }
        }
#endregion

#region interface
        public DataSet CurrData {
            get {
                validator.Validate();
                return dataset;
            }
            set {
                validator.Invalidate();
                dataset = value;
            }
        }
        #endregion

        #region member
        private void ClearAllGlowTextures() {
            for (var i = 0; i<workspaces.Length; i++) {
                var ws = workspaces[i];

                using (new RenderTextureActivator(ws.glowTex)) {
                    GL.Clear(false, true, Color.clear);
                }
            }
        }
        private void ReleaseTextures() {
            if (link.mainCam != null)
                link.mainCam.targetTexture = null;

            mainTex.DestroySelf();
        }
        private void ResizeWorkspaces() {
            for (var i = dataset.datas.Length; i < workspaces.Length; i++) {
                workspaces[i].Dispose();
            }
            var oldWorkspaceSize = workspaces.Length;
            System.Array.Resize(ref workspaces, dataset.datas.Length);
            for (var i = oldWorkspaceSize; i < workspaces.Length; i++) {
                var go = new GameObject("Glow camera");
                workspaces[i] = new Workspace() {
                    glowCam = go.AddComponent<Camera>(),
                    cleaner = go.AddComponent<TargetCleaner>()
                };
            }
        }
        private void UpdateWorkspaces(RenderTexture mainTex) {
            var w = mainTex.width;
            var h = mainTex.height;
            for (var i = 0; i < workspaces.Length; i++) {
                var d = dataset.datas[i];
                var ws = workspaces[i];
                var glowCam = ws.glowCam;
                if (NeedResize(ws.glowTex, w, h)) {
                    glowCam.targetTexture = null;
                    ws.GlowTex = Resize(ref ws.glowTex, w, h, 0);
                }

                glowCam.CopyFrom(link.mainCam);
                glowCam.tag = TAG_UNTAGGED;
                glowCam.gameObject.hideFlags = HideFlags.DontSave;
                glowCam.transform.SetParent(link.mainCam.transform, false);
                glowCam.transform.localPosition = Vector3.zero;
                glowCam.transform.localRotation = Quaternion.identity;
                glowCam.transform.localScale = Vector3.one;
                glowCam.cullingMask = 1 << d.layerIndex;
                glowCam.depth = link.mainCam.depth + 1;

                glowCam.backgroundColor = Color.clear;
                glowCam.clearFlags = CameraClearFlags.Nothing;
                glowCam.SetTargetBuffers(ws.glowTex.colorBuffer, mainTex.depthBuffer);
            }
        }
        void ResetAllTargetTextures() {
            link.mainCam.targetTexture = null;
            foreach (var ws in workspaces)
                if (ws.glowCam != null)
                    ws.glowCam.targetTexture = null;
        }
        private void ResetWorkspaces() {
            foreach (var ws in workspaces)
                ws.Dispose();
            System.Array.Resize(ref workspaces, 0);
        }
#endregion

#region static
        public static bool NeedResize(RenderTexture tex, int width, int height) {
            return tex == null || tex.width != width || tex.height != height;
        }
        public static RenderTexture Resize(ref RenderTexture tex, int width, int height,
            int depth = 24,
            RenderTextureFormat format = RenderTextureFormat.ARGBHalf) {

            tex.DestroySelf();
            tex = new RenderTexture(width, height, depth, format);
            tex.antiAliasing = 1; // QualitySettings.antiAliasing;
            return tex;
        }
        public static RenderTexture Resize(ref RenderTexture tex, RenderTexture source) {
            return Resize(ref tex, source.width, source.height, source.depth, source.format);
        }
#endregion

#region classes
        [System.Serializable]
        public class References {
            public Camera mainCam;
        }
        [System.Serializable]
        public class Commons {
            public Color clearColor = Color.clear;
            public LayerMask debugLayer;
            public OverlayMode overlayMode = OverlayMode.None;
            public int overlayIndex = -1;
            public float overlayHeight = 0.4f;
        }
        [System.Serializable]
        public class Data {
            public int layerIndex;
			[Range(0, 3)]
            public int iterations = 0;
			[Range(4, 1024)]
            public int blurResolution = 128;
            public float intensity = 3f;
            [Range(0f, 1f)]
            public float threshold = 0.6f;
            public KeywordLum thresholdMode = KeywordLum.___;
        }
        [System.Serializable]
        public class DataSet {
            public Commons commons = new Commons();
            public Data[] datas = new Data[0];
        }
        public class Workspace : System.IDisposable {
            public Camera glowCam;
            public TargetCleaner cleaner;
            public RenderTexture glowTex;
            public RenderTexture thresholdTex;
            public RenderTexture blurred;

            public RenderTexture GlowTex {
                get {
                    return glowTex;
                }
                set {
                    this.glowTex = value;
                    this.cleaner.SetTexture(glowTex);
                }
            }

#region interface

#region IDisposable
            public void Dispose() {
                if (glowCam != null) {
                    glowCam.targetTexture = null;
                    glowCam.DestroyGo();
                }
                glowTex.DestroySelf();
                thresholdTex.DestroySelf();
                blurred.DestroySelf();

            }
#endregion

            public void UpdateBlurTex(Data data, Blur blur, Texture source, Material mat) {
                if (NeedResize(thresholdTex, glowTex.width, glowTex.height)) {
                    thresholdTex = Resize(ref thresholdTex, glowTex);
                }
                var vthresh = new Vector4(data.threshold, 1f / Mathf.Clamp(1f - data.threshold, 0.1f, 1f), 0f, 0f);
                mat.SetFloat(P_INTENSITY, data.intensity);
                mat.SetVector(P_THREAHOLD, vthresh);
                mat.shaderKeywords = null;
                if (data.thresholdMode != KeywordLum.___)
                    mat.EnableKeyword(data.thresholdMode.ToString());
                Graphics.Blit(glowTex, thresholdTex, mat, (int)ShaderPass.Threshold);

                var tmpBlurIter = data.iterations;
				data.blurResolution = Mathf.Max(4, data.blurResolution);
                blur.FindSize(source.height, data.blurResolution, out tmpBlurIter, out int tmpBlurLod);
                blur.Render(thresholdTex, ref blurred, tmpBlurIter, tmpBlurLod);
            }
#endregion
        }
#endregion
    }
}
