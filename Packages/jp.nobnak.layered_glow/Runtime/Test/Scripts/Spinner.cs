using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Random = Unity.Mathematics.Random;

namespace LayeredGlowSys.Test {

    public class Spinner : MonoBehaviour {

        public Tuner tuner = new();

        protected Random rand;

        void OnEnable() {
            rand = new Random((uint)GetInstanceID());
        }
        protected void Update() {
            var dt = Time.deltaTime;
            var t = Time.time * tuner.frequency;
            var v = new float3(
                noise.snoise(new float4(100, 0, 0, t)),
                noise.snoise(new float4(0, 100, 0, t)),
                noise.snoise(new float4(0, 0, 100, t)));
            var rot = quaternion.Euler(v * (tuner.speed * dt * PI2));
            transform.rotation *= rot;
        }

        #region declarations
        public static readonly float PI2 = math.PI * 2f;
        [System.Serializable]
        public class Tuner {
            public float speed = 1f;
            public float frequency = 1f;
        }
        #endregion
    }
}