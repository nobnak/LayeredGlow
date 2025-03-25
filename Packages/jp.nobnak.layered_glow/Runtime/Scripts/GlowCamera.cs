using Gist2.Deferred;
using Gist2.Extensions.ComponentExt;
using Gist2.Extensions.SizeExt;
using Gist2.Scope;
using LayeredGlowSys.Data;
using PyramidBlur;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace LayeredGlowSys {

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

        protected Camera depthCaptureCam;

        protected Camera attachedCam;
        protected RenderTexture depthTex;
        protected Material mat;
        protected Blur blur;

        protected bool initialized_workspace;
        protected Workspace[] workspaces = new Workspace[0];

        protected CameraData currAttachedCamData;
        protected int2 currMainCamSize;
        protected Validator validator = new();

        #region unity
        void OnEnable() {
            blur = new Blur();
            mat = Resources.Load<Material>(PATH);

            validator.Reset();
            validator.CheckValidity += () => {
                var mainCam = GetMainCamera();
                var valid = (attachedCam != null && currAttachedCamData.Equals(attachedCam))
                    && (mainCam != null && depthCaptureCam != null && math.all(currMainCamSize == mainCam.Size()))
                    && initialized_workspace;
                return valid;
            };
            validator.OnValidate += () => {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("GlowCamera is invalidated");
#endif
                var mainCam = GetMainCamera();
                initialized_workspace = false;
                if (mainCam == null) {
                    Debug.LogWarning("Main camera is not found.");
                    return;
                }

                if (depthCaptureCam == null) {
                    var goDepthCaptureCam = new GameObject("Depth Capture Camera");
                    goDepthCaptureCam.hideFlags = HideFlags.DontSave;
                    goDepthCaptureCam.transform.SetParent(mainCam.transform, false);
                    depthCaptureCam = goDepthCaptureCam.AddComponent<Camera>();
                }
                depthCaptureCam.CopyFrom(mainCam);

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

                var w_src = attachedCam.pixelWidth;
                var h_src = attachedCam.pixelHeight;
                var w = w_src;
                var h = h_src;

                var limitSize = dataset.commons.limitSize;
                var scale = 1f;
                if (4 <= limitSize.x && limitSize.x < w)
                    scale = math.min(scale, limitSize.x / (float)w);
                if (4 <= limitSize.y && limitSize.y < h)
                    scale = math.min(scale, limitSize.y / (float)h);
                if (scale < 1f) {
                    w = (int)math.round(w * scale);
                    h = (int)math.round(h * scale);
                }

                if (NeedResize(depthTex, w, h)) {
                    ResetAllRelatedToMainTex();
                    Resize(ref depthTex, w, h, 24, RenderTextureFormat.Depth);
                    depthTex.name = $"Depth texture (Main camera)";
                }
                depthCaptureCam.targetTexture = depthTex;
                NotifyDepthTextureOnCreate(depthTex);

                ResizeWorkspaces();
                UpdateWorkspaces(depthTex, w, h);

                initialized_workspace = true;

                currAttachedCamData = attachedCam;
                currMainCamSize = mainCam.Size();
            };
        }

        void OnDisable() {
            if (blur != null) {
                blur.Dispose();
                blur = null;
            }
            if (depthCaptureCam != null) {
                depthCaptureCam.targetTexture = null;
                CoreUtils.Destroy(gameObject);
                depthCaptureCam = null;
            }
            ResetWorkspaces();
            DestroyGeneratedTexture();
            validator.Invalidate();
        }

        void OnValidate() {
            validator.Invalidate();
        }
        void Update() {
            validator.Validate();
            //Clear(depthTex);
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination) {
            validator.Validate();
            if (!initialized_workspace) {
                Graphics.Blit(source, destination);
                return;
            }

            Graphics.Blit(source, destination);

            for (var i = 0; i < workspaces.Length; i++) {
                var d = dataset.datas[i];
                var ws = workspaces[i];
                if (!d.enabled) continue;
                ws.UpdateBlurTex(d, blur, mat);
                Graphics.Blit(ws.blurred, destination, mat, (int)ShaderPass.Additive);
            }

            var debugLayerIsVisible = dataset.commons.overlayMode != OverlayMode.None;
            if (debugLayerIsVisible) {
                var debugOutputTex = RenderTexture.GetTemporary(source.descriptor);
                using (new ScopedRenderTexture(debugOutputTex))
                    GL.Clear(true, true, dataset.commons.clearColor);
                for (var i = 0; i < workspaces.Length; i++) {
                    var ws = workspaces[i];
                    var d = dataset.datas[i];
                    if (!d.enabled) continue;
                    var debugInputTex = default(Texture);
                    switch (dataset.commons.overlayMode) {
                        case OverlayMode.Glow:
                        debugInputTex = ws.glowTex;
                        break;
                        case OverlayMode.Threshold:
                        debugInputTex = ws.thresholdTex;
                        break;
                        case OverlayMode.Blurred:
                        debugInputTex = ws.blurred;
                        break;
                    }
                    if (debugInputTex != null)
                        Graphics.Blit(debugInputTex, debugOutputTex, mat, (int)ShaderPass.Additive);
                }

                GL.PushMatrix();
                GL.LoadPixelMatrix();
                using (new ScopedRenderTexture(destination)) {
                    var gap = 10f;
                    var height = dataset.commons.overlayHeight * source.height;
                    var aspect = (float)debugOutputTex.width / debugOutputTex.height;
                    Graphics.DrawTexture(new Rect(gap, gap + height, height * aspect, -height),
                            debugOutputTex, mat, (int)ShaderPass.Overlay);
                }
                GL.PopMatrix();

                RenderTexture.ReleaseTemporary(debugOutputTex);
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
        private static void Clear(RenderTexture depthTex) {
            var currActiveRenderTexture = RenderTexture.active;
            RenderTexture.active = depthTex;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = currActiveRenderTexture;
        }
        protected Camera GetMainCamera() {
            if (link.mainCam == null)
                link.mainCam = Camera.main;
            return link.mainCam;
        }
        private void NotifyDepthTextureOnCreate(RenderTexture targetTex) {
            events.DepthTextureOnCreate?.Invoke(targetTex);
        }
        private void DestroyGeneratedTexture() {
            ResetAllRelatedToMainTex();
            if (depthCaptureCam != null) depthCaptureCam.targetTexture = null;
            CoreUtils.Destroy(depthTex);
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
        private void UpdateWorkspaces(RenderTexture depthTex, int w, int h) {
            for (var i = 0; i < workspaces.Length; i++) {
                var d = dataset.datas[i];
                var ws = workspaces[i];
                var glowCam = ws.glowCam;
                if (NeedResize(ws.GlowTex, w, h)) {
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
                glowCam.targetTexture = ws.GlowTex;
                glowCam.SetTargetBuffers(ws.GlowTex.colorBuffer, depthTex.depthBuffer);
            }
        }
        void ResetAllRelatedToMainTex() {
            NotifyDepthTextureOnCreate(null);

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
            RenderTextureFormat format = RenderTextureFormat.ARGBHalf,
            int antiAliasing = -1
        ) {

            CoreUtils.Destroy(tex);

            tex = new RenderTexture(width, height, depth, format);
            tex.hideFlags = HideFlags.DontSave;
            if (antiAliasing >= 0)
                tex.antiAliasing = math.max(1, antiAliasing);
            return tex;
        }
        public static RenderTexture Resize(ref RenderTexture tex, RenderTexture source) {
            return Resize(ref tex, source.width, source.height, source.depth,
                antiAliasing: source.antiAliasing, format: source.format);
        }
        #endregion

        #region classes
        public const string PATH = "GlowCamera-GlowEffect";
        public const string TAG_UNTAGGED = "Untagged";
        public static readonly int P_INTENSITY = Shader.PropertyToID("_Intensity");
        public static readonly int P_THREAHOLD = Shader.PropertyToID("_Threshold");
        public static readonly int ID_TEMP_DEPTH_TEX = Shader.PropertyToID("_TempDepthTex");

        public enum OverlayMode { None = 0, Glow, Threshold, Blurred }
        public enum ShaderPass { Threshold = 0, Additive, Overlay }
        public enum KeywordThreshold { ___ = 0, LUM_AVERAGE, LUM_VALUE, LUM_SATURATE }
        public enum KeywordAlpha { ___ = 0, ALPHA_THROTTLE }

        [System.Serializable]
        public class Events {
            [System.Serializable]
            public class RenderTextureEvent : UnityEvent<RenderTexture> { }

            public RenderTextureEvent DepthTextureOnCreate = new();
        }
        [System.Serializable]
        public class References {
            public Camera mainCam;
        }
        [System.Serializable]
        public class Preset {
        }
        [System.Serializable]
        public class Commons {
            public Color clearColor = Color.clear;
            public OverlayMode overlayMode = default;
            public float overlayHeight = 0.4f;
            public int2 limitSize = new(-1, -1);
        }
        [System.Serializable]
        public class Data {
            public bool enabled = true;
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
                    CoreUtils.Destroy(glowCam.gameObject);
                }
                CoreUtils.Destroy(glowTex);
                CoreUtils.Destroy(thresholdTex);
                CoreUtils.Destroy(blurred);

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
