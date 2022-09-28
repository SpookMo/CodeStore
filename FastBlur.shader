Shader "Hidden/PostProcessing/FastBlur" {

	HLSLINCLUDE

	 #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"

	 TEXTURE2D_SAMPLER2D(_MainTex,sampler_MainTex);
	 TEXTURE2D_SAMPLER2D(_Bloom,sampler_Bloom);
	
	 //abandon
	 //uniform half4 _MainTex_TexelSize;
	 //now same as (_ScreenParams.zw - half2(1.0,1.0))
	 static half2 texelSize = _ScreenParams.zw - half2(1.0,1.0);
	 uniform half4 _Parameter;

	 struct v2f_tap
	 {
		float4 pos : SV_POSITION;
		half2 uv20 : TEXCOORD0;
		half2 uv21 : TEXCOORD1;
		half2 uv22 : TEXCOORD2;
		half2 uv23 : TEXCOORD3;
	 };			

	 v2f_tap vert4Tap (AttributesDefault v )
	 {
		v2f_tap o;
		o.pos = float4(v.vertex.xy,0.0,1.0);
		o.uv20 = TransformTriangleVertexToUV(v.vertex.xy) + texelSize;				
		o.uv21 = TransformTriangleVertexToUV(v.vertex.xy) + texelSize * half2(-0.5h,-0.5h);	
		o.uv22 = TransformTriangleVertexToUV(v.vertex.xy) + texelSize * half2(0.5h,-0.5h);	
		o.uv23 = TransformTriangleVertexToUV(v.vertex.xy) + texelSize * half2(-0.5h,0.5h);


		 #if UNITY_UV_STARTS_AT_TOP
		  o.uv20 = o.uv20 * float2(1.0, -1.0) + float2(0.0, 1.0);
		  o.uv21 = o.uv21 * float2(1.0, -1.0) + float2(0.0, 1.0);
		  o.uv22 = o.uv22 * float2(1.0, -1.0) + float2(0.0, 1.0);
		  o.uv23 = o.uv23 * float2(1.0, -1.0) + float2(0.0, 1.0);
		#endif

		return o; 
	 }					
		
	 float4 fragDownsample ( v2f_tap i ) : COLOR
	 {				
		float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv20);
		color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv21);
		color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv22);
		color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv23);
		return color * 0.25;
	 }
	
		// weight curves

	 static const half curve[7] = { 0.0205, 0.0855, 0.232, 0.324, 0.232, 0.0855, 0.0205 };  // gauss'ish blur weights

	 static const half4 curve4[7] = { half4(0.0205,0.0205,0.0205,0), half4(0.0855,0.0855,0.0855,0), half4(0.232,0.232,0.232,0),
			half4(0.324,0.324,0.324,1), half4(0.232,0.232,0.232,0), half4(0.0855,0.0855,0.0855,0), half4(0.0205,0.0205,0.0205,0) };

	 struct v2f_withBlurCoords8 
	 {
		float4 pos : SV_POSITION;
		half2 uv : TEXCOORD0;
		half2 offs : TEXCOORD1;
	 };	
		
	 struct v2f_withBlurCoordsSGX 
	 {
		float4 pos : SV_POSITION;
		half2 uv : TEXCOORD0;
		half4 offs[3] : TEXCOORD1;
	 };

	 v2f_withBlurCoords8 vertBlurHorizontal (AttributesDefault v)
	 {
		v2f_withBlurCoords8 o;
		o.pos = float4(v.vertex.xy,0.0,1.0);
			
		o.uv = TransformTriangleVertexToUV(v.vertex.xy);
		o.offs = texelSize * half2(1.0, 0.0) * _Parameter.x;

		//#if UNITY_UV_STARTS_AT_TOP
		// o.uv = o.uv * half2(1.0, -1.0) + half2(0.0, 1.0);
		//#endif

		return o; 
	 }
		
	 v2f_withBlurCoords8 vertBlurVertical (AttributesDefault v)
	 {
		v2f_withBlurCoords8 o;
		o.pos = float4(v.vertex.xy,0.0,1.0);
			
		o.uv = TransformTriangleVertexToUV(v.vertex.xy);
		o.offs = texelSize * half2(0.0, 1.0) * _Parameter.x;

		
		//#if UNITY_UV_STARTS_AT_TOP
		 // o.uv = o.uv * half2(1.0, -1.0) + half2(0.0, 1.0);
		//#endif
			 
		return o; 
	 }	

	 half4 fragBlur8 ( v2f_withBlurCoords8 i ) : COLOR
	 {
		half2 uv = i.uv.xy; 
		half2 netFilterWidth = i.offs;  
		half2 coords = uv - netFilterWidth * 3.0; 
		
		half4 color = 0;
		
  		for( int l = 0; l < 7; l++ )  
  		{   
			half4 tap = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, coords);
			color += tap * curve4[l];
			coords += netFilterWidth;
  		}

		return color;
	 }


	 v2f_withBlurCoordsSGX vertBlurHorizontalSGX (AttributesDefault v)
	 {
		v2f_withBlurCoordsSGX o;
		o.pos = float4(v.vertex.xy,0.0,1.0);
			
		o.uv = TransformTriangleVertexToUV(v.vertex.xy);
		half2 netFilterWidth = texelSize * half2(1.0, 0.0) * _Parameter.x; 
		half4 coords = -netFilterWidth.xyxy * 3.0;
			
		o.offs[0] = o.uv.xyxy + coords * half4(1.0h,1.0h,-1.0h,-1.0h);
		coords += netFilterWidth.xyxy;
		o.offs[1] = o.uv.xyxy + coords * half4(1.0h,1.0h,-1.0h,-1.0h);
		coords += netFilterWidth.xyxy;
		o.offs[2] = o.uv.xyxy + coords * half4(1.0h,1.0h,-1.0h,-1.0h);

		return o; 
	 }		
		
	 v2f_withBlurCoordsSGX vertBlurVerticalSGX (AttributesDefault v)
	 {
		v2f_withBlurCoordsSGX o;
		o.pos = float4(v.vertex.xy,0.0,1.0);
			
		o.uv = TransformTriangleVertexToUV(v.vertex.xy);
		half2 netFilterWidth = texelSize * half2(0.0, 1.0) * _Parameter.x;
		half4 coords = -netFilterWidth.xyxy * 3.0;
			
		o.offs[0] = o.uv.xyxy + coords * half4(1.0h,1.0h,-1.0h,-1.0h);
		coords += netFilterWidth.xyxy;
		o.offs[1] = o.uv.xyxy + coords * half4(1.0h,1.0h,-1.0h,-1.0h);
		coords += netFilterWidth.xyxy;
		o.offs[2] = o.uv.xyxy + coords * half4(1.0h,1.0h,-1.0h,-1.0h);

		return o; 
	 }	

	 half4 fragBlurSGX ( v2f_withBlurCoordsSGX i ) : COLOR
	 {

		half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * curve4[3];
			
  		for( int l = 0; l < 3; l++ )  
  		{   
			half4 tapA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.offs[l].xy);
			half4 tapB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.offs[l].zw);
			color += (tapA + tapB) * curve4[l];
  		}

		return color;

	  }	
					
	ENDHLSL
	
	SubShader {
	  ZTest Off Cull Off ZWrite Off Blend Off
	  Fog { Mode off }  

		// 0
		Pass { 
	
				HLSLPROGRAM
		
					#pragma vertex vert4Tap
					#pragma fragment fragDownsample
		
				ENDHLSL
		 
			}

		// 1
		Pass {
				//ZTest Always
				Cull Off
		
				HLSLPROGRAM
		
					#pragma vertex vertBlurVertical
					#pragma fragment fragBlur8
		
				ENDHLSL
			}	
		
		// 2
		Pass {		
				//ZTest Always
				Cull Off
				
				HLSLPROGRAM
		
					#pragma vertex vertBlurHorizontal
					#pragma fragment fragBlur8
		
				ENDHLSL
			}	

		// alternate blur
		// 3
		Pass {
				ZTest Always
				Cull Off
		
				HLSLPROGRAM
		
					#pragma vertex vertBlurVerticalSGX
					#pragma fragment fragBlurSGX
		
				ENDHLSL
			}	
		
		// 4
		Pass {		
				ZTest Always
				Cull Off
				
				HLSLPROGRAM
		
					#pragma vertex vertBlurHorizontalSGX
					#pragma fragment fragBlurSGX
		
				ENDHLSL
			}	
	}	
}
