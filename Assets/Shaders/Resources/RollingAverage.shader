Shader "Hidden/RollingAccumulation"
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

			sampler2D _CurrentFrame;
			sampler2D _Accumulation;

			float _Smoothing;
			float _Samples;

			float4 frag(v2f i) : SV_Target
			{
				float4 currentFrame = tex2D(_CurrentFrame, i.uv);
				float4 accumulation = tex2D(_Accumulation, i.uv);

				// rolling avg of rendered frames
				float4 color = currentFrame * (_Smoothing / (1 + _Samples)) + accumulation * (1 - (_Smoothing / (1 + _Samples)));

				return color;
			}
			ENDCG
		}
	}
}
