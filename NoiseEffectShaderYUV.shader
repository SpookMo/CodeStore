// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Hidden/PostProcessing/Noise Shader YUV" 
{
  HLSLINCLUDE

    #pragma target 3.0
    #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"

	struct v2f { 
		float4 pos	: POSITION;
		float2 uv	: TEXCOORD0;
		float2 uvg	: TEXCOORD1; // grain
		float2 uvs	: TEXCOORD2; // scratch
	}; 

	TEXTURE2D_SAMPLER2D(_MainTex,sampler_MainTex);
	TEXTURE2D_SAMPLER2D(_GrainTex,sampler_GrainTex);
	TEXTURE2D_SAMPLER2D(_ScratchTex,sampler_ScratchTex);

	uniform float4 _GrainOffsetScale;
	uniform float4 _ScratchOffsetScale;
	uniform float4 _Intensity; // x=grain, y=scratch

	v2f vert (AttributesDefault v)
	{
		v2f o;
		o.pos = float4(v.vertex.xy,0.0,1.0);
		o.uv = TransformTriangleVertexToUV(v.vertex.xy);
		o.uvg = TransformTriangleVertexToUV(v.vertex.xy) * _GrainOffsetScale.zw + _GrainOffsetScale.xy;
		o.uvs = TransformTriangleVertexToUV(v.vertex.xy) * _ScratchOffsetScale.zw + _ScratchOffsetScale.xy;

		#if UNITY_UV_STARTS_AT_TOP
		o.uv = o.uv * float2(1.0, -1.0) + float2(0.0, 1.0);
		#endif

		return o;

	}

	float4 frag (v2f i) : COLOR
	{
	   float4 col = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
	
		// convert to YUV
		float3 yuv;
		yuv.x = dot( col.rgb, half3(0.299,0.587,0.114) );
		yuv.y = (col.b - yuv.x) * 0.492;
		yuv.z = (col.r - yuv.x) * 0.877;
	
		// sample noise texture and do a signed add
		float3 grain = SAMPLE_TEXTURE2D(_GrainTex,sampler_GrainTex,i.uvg).rgb * 2 - 1;
		yuv.rgb += grain * _Intensity.x;

		// convert back to rgb
		col.r = yuv.z * 1.140 + yuv.x;
		col.g = yuv.z * (-0.581) + yuv.y * (-0.395) + yuv.x;
		col.b = yuv.y * 2.032 + yuv.x;

		// sample scratch texture and add
		float3 scratch = SAMPLE_TEXTURE2D(_ScratchTex,sampler_ScratchTex,i.uvs).rgb * 2 - 1;
		col.rgb += scratch * _Intensity.y;

		return col;
	}

  ENDHLSL


	SubShader {
		Pass {
			ZTest Always Cull Off ZWrite Off
			Fog { Mode off }
				
			HLSLPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				//#pragma fragmentoption ARB_precision_hint_fastest
			ENDHLSL
			}
	}
}