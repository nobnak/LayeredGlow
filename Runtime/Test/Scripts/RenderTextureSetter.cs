using Gist2.Deferred;
using Gist2.Wrappers;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

public class RenderTextureSetter : MonoBehaviour {

    public Events events = new();
    public Preset preset = new();

    protected Validator changed = new();
    protected RenderTextureWrapper textureWrapper;

    #region unity
    void Awake() {
        events.onEnable?.Invoke(false);
    }
    void OnEnable() {
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
        Graphics.Blit(textureWrapper, destination);
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
    }
    #endregion
}