using Gist2.Deferred;
using Gist2.Scope;
using Gist2.Wrappers;
using LayeredGlowSys;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

public class RenderTextureSetter : MonoBehaviour {

    public Events events = new();
    public Preset preset = new();

    protected Validator changed = new();
    protected RenderTextureWrapper textureWrapper;
    protected Material mat;

    #region unity
    void Awake() {
        events.onEnable?.Invoke(false);
    }
    void OnEnable() {
        mat = Resources.Load<Material>(GlowCamera.PATH);
        textureWrapper = new RenderTextureWrapper(size => {
            var tex = new RenderTexture(size.x, size.y, 24);
            tex.hideFlags = HideFlags.DontSave;
            tex.name = gameObject.name;
            return tex;
        });
        textureWrapper.Changed += v => {
            events.onRenderTextureChanged?.Invoke(v);
        };

        changed.OnValidate += () => {
            textureWrapper.Size = new int2(Screen.width, Screen.height);
        };

        events.onEnable?.Invoke(true);
    }
    void OnDisable() {
        if (textureWrapper != null) {
            textureWrapper.Dispose();
            textureWrapper = null;
        }

        events.onEnable?.Invoke(false);
    }
    void OnValidate() {
        changed.Invalidate();
    }
    void Update() {
        changed.Validate();
        textureWrapper.Validate();
    }
    void OnRenderImage(RenderTexture source, RenderTexture destination) {
        Graphics.Blit(source, destination);

        if (textureWrapper != null) {
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            using (new ScopedRenderTexture(destination)) {
                var height = preset.renderSize * source.height;
                var textureSize = textureWrapper.Size;
                var aspect = (float)textureSize.x / textureSize.y;
                var gap = preset.renderOffset * height;
                Graphics.DrawTexture(new Rect(gap, gap + height, height * aspect, -height),
                        textureWrapper, mat, (int)GlowCamera.ShaderPass.Overlay);
            }
            GL.PopMatrix();
        }
    }
    #endregion

    #region declarations
    [System.Serializable]
    public class Events {
        public RenderTextureEvent onRenderTextureChanged = new();
        public BoolEvent onEnable = new();

        [System.Serializable]
        public class RenderTextureEvent : UnityEvent<RenderTexture> { }
        [System.Serializable]
        public class BoolEvent : UnityEvent<bool> { }
    }
    [System.Serializable]
    public class Preset {
        public float renderSize = 0.5f;
        public float renderOffset = 0f;
    }
    #endregion
}