using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LayeredGlowSys {

	public class TestGlowCamera : MonoBehaviour {

		[SerializeField]
		protected GlowCamera glow;
		[SerializeField]
		protected float peakIntensity = 1f;
		[SerializeField]
		protected float speed = 0.01f;
		[SerializeField]
		protected float hysteresis = 1f;

		#region unity
		private void Update() {
			var data = glow.CurrData;
			var time = Time.realtimeSinceStartup * speed;
			var t = hysteresis * (Mathf.PerlinNoise(time, 0) - 0.5f) + 0.5f;

			for (var i = 0; i < data.datas.Length; i++) {
				var d = data.datas[i];
				d.intensity = t * peakIntensity;
				t = 1f - t;
			}
		}
		#endregion
	}
}
