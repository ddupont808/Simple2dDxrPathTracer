// Edge-Avoiding À-TrousWavelet Transform for denoising
// modified version of the implementation from https://www.shadertoy.com/view/ldKBzG
// (mainly just changed it to use depth based edge detection rather than normals to work better in 2d)

Shader "Hidden/Denoise"
{
	Properties
	{

	}
	SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			float hash1(float seed) {
				return frac(sin(seed) * 43758.5453123);
			}

			sampler2D _CameraDepthTexture;

			float depth(float2 uv) {
				float rawDepth = tex2D(_CameraDepthTexture, uv).r;
				float ortho = (_ProjectionParams.z - _ProjectionParams.y) * (1 - rawDepth) + _ProjectionParams.y;
				return ortho;
			}

			sampler2D _CurrentFrame;
			sampler2D _LastDenoisedFrame;

			float2 _DenoiseRange;

			float4 frag(v2f i) : SV_Target
			{
				float2 offset[25];
				offset[0] = float2(-2,-2);
				offset[1] = float2(-1,-2);
				offset[2] = float2(0,-2);
				offset[3] = float2(1,-2);
				offset[4] = float2(2,-2);

				offset[5] = float2(-2,-1);
				offset[6] = float2(-1,-1);
				offset[7] = float2(0,-1);
				offset[8] = float2(1,-1);
				offset[9] = float2(2,-1);

				offset[10] = float2(-2,0);
				offset[11] = float2(-1,0);
				offset[12] = float2(0,0);
				offset[13] = float2(1,0);
				offset[14] = float2(2,0);

				offset[15] = float2(-2,1);
				offset[16] = float2(-1,1);
				offset[17] = float2(0,1);
				offset[18] = float2(1,1);
				offset[19] = float2(2,1);

				offset[20] = float2(-2,2);
				offset[21] = float2(-1,2);
				offset[22] = float2(0,2);
				offset[23] = float2(1,2);
				offset[24] = float2(2,2);


				float kernel[25];
				kernel[0] = 1.0f / 256.0;
				kernel[1] = 1.0f / 64.0;
				kernel[2] = 3.0f / 128.0;
				kernel[3] = 1.0f / 64.0;
				kernel[4] = 1.0f / 256.0;

				kernel[5] = 1.0f / 64.0;
				kernel[6] = 1.0f / 16.0;
				kernel[7] = 3.0f / 32.0;
				kernel[8] = 1.0f / 16.0;
				kernel[9] = 1.0f / 64.0;

				kernel[10] = 3.0f / 128.0;
				kernel[11] = 3.0f / 32.0;
				kernel[12] = 9.0f / 64.0;
				kernel[13] = 3.0f / 32.0;
				kernel[14] = 3.0f / 128.0;

				kernel[15] = 1.0f / 64.0;
				kernel[16] = 1.0f / 16.0;
				kernel[17] = 3.0f / 32.0;
				kernel[18] = 1.0f / 16.0;
				kernel[19] = 1.0f / 64.0;

				kernel[20] = 1.0f / 256.0;
				kernel[21] = 1.0f / 64.0;
				kernel[22] = 3.0f / 128.0;
				kernel[23] = 1.0f / 64.0;
				kernel[24] = 1.0f / 256.0;


				float3 sum = float3(0, 0, 0);
				float3 sum_f = float3(0, 0, 0);
				float c_phi = 1.0;
				float r_phi = 1.0;
				float d_phi = 0.5;
				float p_phi = 0.25;

				float3 cval = tex2D(_CurrentFrame, i.uv).xyz;
				float dval = depth(i.uv);
				float3 rval = tex2D(_LastDenoisedFrame, i.uv).xyz;

				float ang = 2.0 * 3.1415926535 * hash1(251.12860182 * i.uv.x + 729.9126812 * i.uv.y + 5.1839513 * _Time);
				float2x2 m = float2x2(cos(ang), sin(ang), -sin(ang), cos(ang));

				float cum_w = 0.0;
				float cum_fw = 0.0;

				float denoiseStrength = (_DenoiseRange.x + (_DenoiseRange.y - _DenoiseRange.x) * hash1(641.128752 * i.uv.x + 312.321374 * i.uv.y + 1.92357812 * _Time));

				uint ind;
				for (ind = 0; ind < 25; ind ++) {
					float2 uv = i.uv + mul(m, offset[ind] * denoiseStrength) / _ScreenParams.xy;

					float3 ctmp = tex2D(_CurrentFrame, uv).xyz;
					float3 t = cval - ctmp;
					float dist2 = dot(t, t);
					float c_w = min(exp(-(dist2) / c_phi), 1.0);

					float dtmp = depth(uv);
					dist2 = (dval - dtmp) * (dval - dtmp);
					float d_w = min(exp(-(dist2) / d_phi), 1.0);

					float3 rtmp = tex2D(_LastDenoisedFrame, uv).xyz;
					t = rval - rtmp;
					dist2 = dot(t, t);
					float r_w = min(exp(-(dist2) / r_phi), 1.0);

					// new denoised frame
					float weight0 = c_w * d_w;
					sum += ctmp * weight0 * kernel[ind];
					cum_w += weight0 * kernel[ind];

					// denoise the previous denoised frame again
					float weight1 = r_w * d_w;
					sum_f += rtmp * weight1 * kernel[ind];
					cum_fw += weight1 * kernel[ind];
				}

				// mix in more of the just-denoised frame if it differs significantly from the
				// frame from feedback
				float3 ptmp = tex2D(_CurrentFrame, i.uv).xyz;
				float3 t = sum / cum_w - ptmp;
				float dist2 = dot(t, t);
				float p_w = min(exp(-(dist2) / p_phi), 1.0);

				return clamp(float4(lerp(sum / cum_w, sum_f / cum_fw, p_w), 0.0), 0.0, 1.0);
			}
			ENDCG
		}
	}
}
