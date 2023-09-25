using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LayeredGlowSys.Data {

    public struct CameraData : System.IEquatable<Camera> {

        public Matrix4x4 worldToCameraMatrix;
        public Matrix4x4 projectionMatrix;

        public int cullingMask;

        public int pixelWidth;
        public int pixelHeight;

        public RenderTexture targetTexture;

        public CameraData(Camera target) {
            this.worldToCameraMatrix = target.worldToCameraMatrix;
            this.projectionMatrix = target.projectionMatrix;
            this.cullingMask = target.cullingMask;
            this.pixelWidth = target.pixelWidth;
            this.pixelHeight = target.pixelHeight;
            this.targetTexture = target.targetTexture;
        }

        #region IEquatable
        public bool Equals(Camera other) {
            if (other == null) return false;
            return this.worldToCameraMatrix == other.worldToCameraMatrix
                && this.projectionMatrix == other.projectionMatrix
                && this.cullingMask == other.cullingMask
                && this.pixelWidth == other.pixelWidth
                && this.pixelHeight == other.pixelHeight
                && this.targetTexture == other.targetTexture;
        }
        #endregion

        public static implicit operator CameraData(Camera target) {
            return new CameraData(target);
        }
    }
}
