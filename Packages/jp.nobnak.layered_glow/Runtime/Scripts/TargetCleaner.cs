using Gist2.Scope;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LayeredGlowSys {

    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class TargetCleaner : MonoBehaviour {

        protected Camera cam;
        protected RenderTexture colorTex;

        #region interface
        public void SetTexture(RenderTexture color) {
            this.colorTex = color;
        }
        #endregion

        #region unity
        private void OnEnable() {
            cam = GetComponent<Camera>();
        }
        private void OnPreRender() {
            using (new ScopedRenderTexture(colorTex)) {
                GL.Clear(false, true, Color.clear);
            }
        }
        #endregion
    }
}
