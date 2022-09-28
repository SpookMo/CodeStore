Shader "Hidden/PostProcessing/SimpleClear" {

 HLSLINCLUDE
 
    #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"

	TEXTURE2D_SAMPLER2D(_MainTex,sampler_MainTex);
 
	struct v2f {
		float4 pos : POSITION;
	};

	v2f vert(AttributesDefault v )
	{
		v2f o;
		o.pos = float4(v.vertex.xy,0.0,1.0);
		return o;
	}

	half4 frag (v2f i) : COLOR
	{
		return half4(0,0,0,0);
	}

 ENDHLSL	
		

	SubShader {
		Pass {
			ZTest Always Cull Off ZWrite Off
			Fog { Mode off }

		    HLSLPROGRAM
				#pragma vertex vert
				#pragma fragment frag
	        ENDHLSL
			}
	}

}