Shader "GlowCamera/GlowEffect" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
		_Intensity ("Intensity", Float) = 0
		_Threshold ("Threshold", Vector) = (0, 1, 0, 0)
    }
    SubShader {
        Cull Off ZWrite Off ZTest Always

			CGINCLUDE
			#pragma multi_compile ___ LUM_AVERAGE LUM_VALUE
            #include "UnityCG.cginc"
			#include "Assets/Packages/Gist/CGIncludes/ColorSpace.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
			float _Intensity;
			float4 _Threshold;

			float Lum(float4 c) {
				#if defined(LUM_AVERAGE)
				return Lightness(c.xyz);
				#elif defined(LUM_VALUE)
				return max(c.x, max(c.y, c.z));
				#else
				return Luminance(c.xyz);
				#endif
			}

			float4 fragThreshold(v2f i) : SV_Target {
				float4 cmain = tex2D(_MainTex, i.uv);
				float l = Lum(cmain);
				float4 cthresh = step(_Threshold.x, l) * cmain;
				return cthresh;
			}
            float4 fragAdditive (v2f i) : SV_Target {
                float4 col = tex2D(_MainTex, i.uv);
                return col * _Intensity;
            }
			float4 fragOverlay(v2f i) : SV_Target {
				float4 cmain = tex2D(_MainTex, i.uv);
				return cmain;
			}
			ENDCG

		// Threshold
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragThreshold
            ENDCG
        }
		// Additive
        Pass {
			Blend OneMinusDstColor One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragAdditive
            ENDCG
        }
		// Overlay
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragOverlay
            ENDCG
        }
    }
}
