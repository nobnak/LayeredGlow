using nobnak.Gist;
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

        protected Validator validator = new Validator();

        #region unity
        private void OnEnable() {
            time = 0f;
            restPosition = transform.position;

            validator.Reset();
            validator.Validation += () => {
                Application.targetFrameRate = framerate;
            };
        }
        private void OnValidate() {
            validator.Invalidate();
        }
        private void Update() {
            validator.Validate();
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