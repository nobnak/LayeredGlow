using Gist2.Deferred;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LayeredGlowSys.Test {

    public class Mover : MonoBehaviour {

        [SerializeField]
        protected float speed = 1f;
        [SerializeField]
        [Range(1, 30)]
        protected int framerate = 10;
        [SerializeField]
        protected Vector3 movePosition;

        protected Vector3 restPosition;
        protected float time;

        protected Validator changed = new Validator();

        #region unity
        private void OnEnable() {
            time = 0f;
            restPosition = transform.position;

            changed.Reset();
            changed.OnValidate += () => {
                Application.targetFrameRate = framerate;
            };
        }
        private void OnValidate() {
            changed.Invalidate();
        }
        private void Update() {
            changed.Validate();
            time += Time.deltaTime * speed;

            transform.position = restPosition
                + new Vector3(
                    movePosition.x * Mathf.PerlinNoise(time, 0f),
                    movePosition.y * Mathf.PerlinNoise(time, 100f),
                    movePosition.z * Mathf.PerlinNoise(time, 200f));
        }
        #endregion
    }
}