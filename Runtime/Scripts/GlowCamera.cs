using Gist2.Deferred;
using Gist2.Extensions.ComponentExt;
using Gist2.Scope;
using LayeredGlowSys.Data;
using PyramidBlur;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace LayeredGlowSys {

    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class GlowCamera : MonoBehaviour {

        [SerializeField]
        protected Events events = new();
        [SerializeField]
        public Preset preset = new();
        [SerializeField]
        protected DataSet dataset = new();
        [SerializeField]
        protected References link = new();

        protected Camera attachedCam;
        protected RenderTexture mainTex_generated, mainTex_external;
        protected Material mat;
        protected Blur blur;

        protected bool initialized_workspace;
        protected Workspace[] workspaces = new Workspace[0];

        protected CameraData currAttachedCamData;
        protected CameraData currMainCamData;
        protected Validator validator = new();

        #region unity
        void OnEnable() {
            blur = new Blur();
            mat = Resources.Load<Material>(PATH);

            validator.Reset();
            validator.CheckValidity += () => {
                var mainCam = GetMainCamera();
                var valid = (attachedCam != null && currAttachedCamData.Equals(attachedCam))
                    && (mainCam != null && currMainCamData.Equals(mainCam));
                return valid;
            };
            validator.OnValidate += () => {
                Debug.Log($"{this.GetType().Name} : Validation");
                var mainCam = GetMainCamera();
                initialized_workspace = false;

                if (attachedCam == null) {
                    attachedCam = GetComponent<Camera>();
                }
                var attachedTex = attachedCam.targetTexture;
                attachedCam.CopyFrom(mainCam);
                attachedCam.targetTexture = attachedTex;

                attachedCam.tag = TAG_UNTAGGED;
                attachedCam.cullingMask = 0;
                attachedCam.clearFlags = CameraClearFlags.Nothing;
                attachedCam.enabled = true;
                attachedCam.useOcclusionCulling = false;
                attachedCam.depth = mainCam.depth + 2;

                var w = attachedCam.pixelWidth;
                var h = attachedCam.pixelHeight;

                currAttachedCamData = attachedCam;
                currMainCamData = mainCam;

                switch (preset.mainTexType) {
                    case MainTexType.CaptureMainCamera: {
                        if (NeedResize(mainTex_generated, w, h)) {
                            ResetAllRelatedToMainTex();
                            SetTargetTextureToMainCamera(Resize(ref mainTex_generated, w, h, 24));
                        }
                        break;
                    }
                    default:
                    case MainTexType.ExternalTexture: {
                        ReleaseMainTexture();
                        break;
                    }
                }

                var mainTex = GetMainTex();
                if (mainTex == null) return;
                ResizeWorkspaces();
                UpdateWorkspaces(mainTex);

                initialized_workspace = true;
            };
        }

        void OnDisable() {
            if (blur != null) {
                blur.Dispose();
                blur = null;
            }
            ResetWorkspaces();
            ReleaseMainTexture();
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
            var mainTex = GetMainTex();
            if (link.mainCam == null || mainTex == null || !initialized_workspace) {
                Graphics.Blit(source, destination);
                return;
            }

            Graphics.Blit(mainTex, destination);

            for (var i = 0; i < workspaces.Length; i++) {
                var d = dataset.datas[i];
                var ws = workspaces[i];
                ws.UpdateBlurTex(d, blur, mat);
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
                    using (new ScopedRenderTexture(destination)) {
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
        public void ListenMainTex(RenderTexture tex) {
            if (mainTex_external != tex) {
                validator.Invalidate();
                mainTex_external = tex;
            }
        }
        #endregion

        #region member
        protected RenderTexture GetMainTex() {
            switch (preset.mainTexType) {
                case MainTexType.CaptureMainCamera: {
                    return mainTex_generated;
                }
                default:
                case MainTexType.ExternalTexture: {
                    return mainTex_external;
                }
            }
        }
        protected Camera GetMainCamera() {
            if (link.mainCam == null)
                link.mainCam = Camera.main;
            return link.mainCam;
        }
        private void ClearAllGlowTextures() {
            for (var i = 0; i < workspaces.Length; i++) {
                var ws = workspaces[i];

                using (new ScopedRenderTexture(ws.glowTex)) {
                    GL.Clear(false, true, Color.clear);
                }
            }
        }
        private void SetTargetTextureToMainCamera(RenderTexture targetTex) {
            events.MainTextureOnCreate.Invoke(targetTex);
            if (link.mainCam != null)
                link.mainCam.targetTexture = targetTex;
        }
        private void ReleaseMainTexture() {
            ResetAllRelatedToMainTex();
            mainTex_generated.Destroy();
        }
        private void ResizeWorkspaces() {
            for (var i = dataset.datas.Length; i < workspaces.Length; i++) {
                workspaces[i].Dispose();
            }
            var oldWorkspaceSize = workspaces.Length;
            System.Array.Resize(ref workspaces, dataset.datas.Length);
            for (var i = oldWorkspaceSize; i < workspaces.Length; i++) {
                var go = new GameObject("Glow camera");
                go.hideFlags = HideFlags.DontSave;
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
        void ResetAllRelatedToMainTex() {
            SetTargetTextureToMainCamera(null);
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
        public static bool NeedResize(Texture tex, int width, int height) {
            return tex == null || tex.width != width || tex.height != height;
        }

        public static bool NeedResize(Texture tex, Texture refTex) {
            return NeedResize(tex, refTex.width, refTex.height);
        }
        public static RenderTexture Resize(ref RenderTexture tex, int width, int height,
            int depth = 24,
            RenderTextureFormat format = RenderTextureFormat.ARGBHalf) {

            tex.Destroy();

            var q = QualitySettings.antiAliasing;
            tex = new RenderTexture(width, height, depth, format);
            tex.antiAliasing = q <= 1 ? 1 : q;
            return tex;
        }
        public static RenderTexture Resize(ref RenderTexture tex, RenderTexture source) {
            return Resize(ref tex, source.width, source.height, source.depth, source.format);
        }
        #endregion

        #region classes
        public const string PATH = "GlowCamera-GlowEffect";
        public const string TAG_UNTAGGED = "Untagged";
        public static readonly int P_INTENSITY = Shader.PropertyToID("_Intensity");
        public static readonly int P_THREAHOLD = Shader.PropertyToID("_Threshold");

        public enum OverlayMode { None = 0, Glow, Threshold, Blurred }
        public enum ShaderPass { Threshold = 0, Additive, Overlay }
        public enum KeywordThreshold { ___ = 0, LUM_AVERAGE, LUM_VALUE }
        public enum KeywordAlpha { ___ = 0, ALPHA_THROTTLE }
        public enum MainTexType { CaptureMainCamera = 0, ExternalTexture }

        [System.Serializable]
        public class Events {
            [System.Serializable]
            public class RenderTextureEvent : UnityEvent<RenderTexture> { }

            public RenderTextureEvent MainTextureOnCreate = new();
        }
        [System.Serializable]
        public class References {
            public Camera mainCam;
        }
        [System.Serializable]
        public class Preset {
            public MainTexType mainTexType = default;
        }
        [System.Serializable]
        public class Commons {
            public Color clearColor = Color.clear;
            public LayerMask debugLayer;
            public OverlayMode overlayMode = default;
            public int overlayIndex = -1;
            public float overlayHeight = 0.4f;
        }
        [System.Serializable]
        public class Data {
            public Blur.Settings blur = new();
            public int layerIndex;
            public float intensity = 3f;
            [Range(0f, 1f)]
            public float threshold = 0.6f;
            public KeywordThreshold thresholdMode = default(KeywordThreshold);
            public KeywordAlpha alphaUsage = default(KeywordAlpha);
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
                    glowCam.gameObject.Destroy();
                }
                glowTex.Destroy();
                thresholdTex.Destroy();
                blurred.Destroy();

            }
            #endregion

            public void UpdateBlurTex(Data data, Blur blur, Material mat) {
                if (NeedResize(thresholdTex, glowTex))
                    thresholdTex = Resize(ref thresholdTex, glowTex);
                if (NeedResize(blurred, glowTex))
                    blurred = Resize(ref blurred, glowTex);

                var vthresh = new Vector4(data.threshold, 1f / Mathf.Clamp(1f - data.threshold, 0.1f, 1f), 0f, 0f);
                mat.SetFloat(P_INTENSITY, data.intensity);
                mat.SetVector(P_THREAHOLD, vthresh);
                mat.shaderKeywords = null;
                if (data.thresholdMode != KeywordThreshold.___)
                    mat.EnableKeyword(data.thresholdMode.ToString());
                if (data.alphaUsage != default)
                    mat.EnableKeyword(data.alphaUsage.ToString());
                Graphics.Blit(glowTex, thresholdTex, mat, (int)ShaderPass.Threshold);

                blur.Render(thresholdTex, blurred, data.blur);
            }
            #endregion
        }
        #endregion
    }
}
