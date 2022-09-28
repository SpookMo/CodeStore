Shader "Hidden/PostProcessing/SunShaftsComposite" {
	
	HLSLINCLUDE
				
		#pragma target 3.0
		#include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"
		#include "Packages/com.unity.postprocessing/PostProcessing/Shaders/Colors.hlsl"
	
		struct v2f {
			float4 pos : POSITION;
			float2 uv : TEXCOORD0;	
		};
		
		struct v2f_radial {
			float4 pos : POSITION;
			float2 uv : TEXCOORD0;
			float2 blurVector : TEXCOORD1;
		};
		
	    TEXTURE2D_SAMPLER2D(_MainTex,sampler_MainTex);
		TEXTURE2D_SAMPLER2D(_ColorBuffer,sampler_ColorBuffer);
		TEXTURE2D_SAMPLER2D(_Skybox,sampler_Skybox);
	    TEXTURE2D_SAMPLER2D(_CameraDepthTexture,sampler_CameraDepthTexture);
	
		uniform half _NoSkyBoxMask;
		
		uniform half4 _SunColor;
		uniform half4 _BlurRadius4;
		uniform half4 _SunPosition;

		#define SAMPLES_FLOAT 6.0f
		#define SAMPLES_INT 6
			
		v2f vert(AttributesDefault v ) {
			v2f o;
			o.pos = float4(v.vertex.xy,0.0,1.0);
			o.uv = TransformTriangleVertexToUV(v.vertex.xy);
				
		    #if UNITY_UV_STARTS_AT_TOP
		     o.uv = o.uv * float2(1.0, -1.0) + float2(0.0, 1.0);
		    #endif
		
			return o;
		}
		
		half4 fragScreen(v2f i) : COLOR { 
			half4 colorA = SAMPLE_TEXTURE2D (_MainTex,sampler_MainTex, i.uv);
			half4 colorB = SAMPLE_TEXTURE2D (_ColorBuffer,sampler_ColorBuffer, i.uv);
			half4 depthMask = saturate (colorB * _SunColor);	
			return 1.0f - (1.0f - colorA) * (1.0f - depthMask);	
		}

		half4 fragAdd(v2f i) : COLOR { 
			half4 colorA = SAMPLE_TEXTURE2D (_MainTex,sampler_MainTex, i.uv);
			half4 colorB = SAMPLE_TEXTURE2D (_ColorBuffer,sampler_ColorBuffer, i.uv);
			half4 depthMask = saturate (colorB * _SunColor);	
			return colorA + depthMask;	
		}
	
		v2f_radial vert_radial(AttributesDefault v ) {
			v2f_radial o;
			o.pos = float4(v.vertex.xy,0.0,1.0);
		
			o.uv = TransformTriangleVertexToUV(v.vertex.xy);

			#if UNITY_UV_STARTS_AT_TOP
		     o.uv = o.uv * float2(1.0, -1.0) + float2(0.0, 1.0);
		    #endif

			//here is not same as v.vertex.xy,cause o.uv represents...
			o.blurVector = (_SunPosition.xy - o.uv) * _BlurRadius4.xy;	
		
			return o; 
		}
	
		half4 frag_radial(v2f_radial i) : COLOR 
		{	
			half4 color = half4(0,0,0,0);
			for(int j = 0; j < SAMPLES_INT; j++)   
			{	
				half4 tmpColor = SAMPLE_TEXTURE2D (_MainTex,sampler_MainTex, i.uv);
				color += tmpColor;
				i.uv.xy += i.blurVector.xy; 	
			}
			return color / SAMPLES_FLOAT;
		}	
	
		half TransformColor (half4 skyboxValue) {
			return max (skyboxValue.a, _NoSkyBoxMask * dot (skyboxValue.rgb, float3 (0.59,0.3,0.11))); 		
		}
	
		half4 frag_depth (v2f i) : COLOR {
		    float depthSample = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,sampler_CameraDepthTexture, i.uv);
		
			half4 tex = SAMPLE_TEXTURE2D (_MainTex,sampler_MainTex, i.uv);
		
			depthSample = Linear01Depth (depthSample);
		 
			// consider maximum radius
			half2 vec = _SunPosition.xy - i.uv.xy;	
			half dist = saturate (_SunPosition.w - length (vec.xy));		
		
			half4 outColor = 0;
		
			// consider shafts blockers
			if (depthSample > 0.99)
				outColor = TransformColor (tex) * dist;
			
			return outColor;
		}
	
		half4 frag_nodepth (v2f i) : COLOR {
		    float4 sky = SAMPLE_TEXTURE2D (_Skybox, sampler_Skybox, i.uv);
		
			float4 tex = SAMPLE_TEXTURE2D (_MainTex,sampler_MainTex, i.uv);
		
			// consider maximum radius
			half2 vec = _SunPosition.xy - i.uv.xy;	
			half dist = saturate (_SunPosition.w - length (vec.xy));	

			half4 outColor = 0;		
		
			if (Luminance ( abs(sky.rgb - tex.rgb)) < 0.2)
				outColor = TransformColor (sky) * dist;
		
			return outColor;
		}	

	ENDHLSL
	
	Subshader {
	
	Tags{
	      "Queue" = "Transparent"
		  "RenderType" = "Transparent"
		  "IgnoreProjector" = "true"
	
	}
     //0
	 Pass {
		  ZTest Always Cull Off ZWrite Off
		  Fog { Mode off }      

			  HLSLPROGRAM
      
				  #pragma vertex vert
				  #pragma fragment fragScreen
      
			  ENDHLSL
		  }
     //1
	 Pass {
		  ZTest Always Cull Off ZWrite Off
		  Fog { Mode off }      

			  HLSLPROGRAM
      
				  #pragma vertex vert_radial
				  #pragma fragment frag_radial
      
			  ENDHLSL
		 }
     //2
	  Pass {
		  ZTest Always Cull Off ZWrite Off
		  Fog { Mode off }      

			  HLSLPROGRAM
         
				  #pragma vertex vert
				  #pragma fragment frag_depth
      
			  ENDHLSL
			}
      //3
	  Pass {
		  ZTest Always Cull Off ZWrite Off
		  Fog { Mode off }      

			  HLSLPROGRAM
          
				  #pragma vertex vert
				  #pragma fragment frag_nodepth
      
			  ENDHLSL
		  } 
      //4
	  Pass {
		  ZTest Always Cull Off ZWrite Off
		  Fog { Mode off }      

			  HLSLPROGRAM

			  #pragma vertex vert
			  #pragma fragment fragAdd
      
			  ENDHLSL
	      } 
	
	} // subshader
}
