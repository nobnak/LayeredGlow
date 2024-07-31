using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
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
		[SerializeField]
		protected float2 range = new float2(0.5f, 1f);

		#region unity
		private void Update() {
			var data = glow.CurrData;
			var time = Time.realtimeSinceStartup * speed;

			for (var i = 0; i < data.datas.Length; i++) {
				var d = data.datas[i];
				var sn = 0.5f * (noise.snoise(new float2(time, i * 100)) + 1);
                var t = math.lerp(range.x, range.y, math.saturate(hysteresis * (sn - 0.5f) + 0.5f));
                d.intensity = t * peakIntensity;
			}
		}
		#endregion
	}
}
