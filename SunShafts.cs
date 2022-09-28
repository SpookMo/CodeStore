using UnityEngine;
using System;
using UnityEngine.Rendering.PostProcessing;

[ExecuteAlways]
[Serializable]
[PostProcess(typeof(SunShaftsRender),PostProcessEvent.AfterStack,"Custom/SunShafts")]
public sealed class SunShafts : PostProcessEffectSettings
{
    
    
    public enum SunShaftsResolution
    {
        Low = 0,
        Normal = 1,
        High = 2,
    }

    public enum ShaftsScreenBlendMode
    {
        Screen = 0,
        Add = 1,
    }

    [Tooltip("resolution type")]
    public ParameterOverride<SunShaftsResolution> resolution = new ParameterOverride<SunShaftsResolution> { value = SunShaftsResolution.Normal };
    [Tooltip("effect blend mode")]
	public ParameterOverride<ShaftsScreenBlendMode> screenBlendMode = new ParameterOverride<ShaftsScreenBlendMode> { value = ShaftsScreenBlendMode.Screen };
    [Tooltip("sun 's world pos")]
    public Vector3Parameter sunPos = new Vector3Parameter { value = new Vector3(0f, 0f, 0f) };
    [Range(1,4)]
	public IntParameter radialBlurIterations = new IntParameter { value = 2 };

    public ColorParameter sunColor = new ColorParameter { value = Color.white };
	public FloatParameter sunShaftBlurRadius = new FloatParameter { value = 2.5f };
	public FloatParameter sunShaftIntensity = new FloatParameter { value = 1.15f };
	public FloatParameter useSkyBoxAlpha = new FloatParameter { value = 0.75f };
	
	public FloatParameter maxRadius = new FloatParameter { value = 0.75f };
	
	public BoolParameter useDepthTexture = new BoolParameter { value = true };

    public ParameterOverride<Shader> sunShaftsShader = new ParameterOverride<Shader> { value = null };
	private Material sunShaftsMaterial;

    public ParameterOverride<Shader> simpleClearShader = new ParameterOverride<Shader> { value = null };
	private Material simpleClearMaterial;

    public Camera m_camera;

    public override bool IsEnabledAndSupported(PostProcessRenderContext context)
    {

        if (useDepthTexture && !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Depth))
            useDepthTexture.value = false;

        if (m_camera == null)
            m_camera = UICamera.mainCamera;

        if (useDepthTexture)
        {
            m_camera.depthTextureMode |= DepthTextureMode.Depth;
        }

        if (sunShaftsShader.value == null && simpleClearShader.value == null)
        {
            enabled.value = false;
            return enabled.value;
        }

        if(!(sunShaftsShader.value.isSupported && simpleClearShader.value.isSupported))
        {
            enabled.value = false;
            return enabled.value;
        }

        return true;
   
    }
    
	public Material SunShafts_Mat {
		get {
			if( sunShaftsMaterial == null ) {
				sunShaftsMaterial = new Material(sunShaftsShader);
				sunShaftsMaterial.hideFlags = HideFlags.HideAndDontSave;
			}
            return sunShaftsMaterial;
		}
	}

    public Material SimpleClear_Mat
    {
        get
        {
            if (simpleClearMaterial == null)
            {
                simpleClearMaterial = new Material(simpleClearShader);
                simpleClearMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            return simpleClearMaterial;
        }
    }


    public void ClearMaterial() {
		if( sunShaftsMaterial != null )
			DestroyImmediate( sunShaftsMaterial );
		if( simpleClearMaterial != null )
			DestroyImmediate( simpleClearMaterial );
        Debug.Log("ClearMaterial");
	}
}

[UnityEngine.Scripting.Preserve]
public sealed class SunShaftsRender : PostProcessEffectRenderer<SunShafts>
{
    internal Vector4 v4;
    internal Vector3 v3;

    RenderTexture lrColorB, lrDepthBuffer, tmpDepthBuffer;
    int rtW, rtH, idx;
    int divider;
    PropertySheet sheet;

    public override void Render(PostProcessRenderContext context)
    {
        if (!settings.enabled.value)
        {
            context.command.BlitFullscreenTriangle(context.source, context.destination); 
            return;
        }

        sheet = context.propertySheets.Get(settings.SunShafts_Mat.shader);
        
        divider = 4;
        if (settings.resolution.value == SunShafts.SunShaftsResolution.Normal)
            divider = 2;
        else if (settings.resolution.value == SunShafts.SunShaftsResolution.High)
            divider = 1;


        if (settings.m_camera != null)
            v3 = settings.m_camera.WorldToViewportPoint(settings.sunPos.value);
        else v3.Set(0.5f, 0.5f, 0f);

        rtW = context.width / divider;
        rtH = context.height / divider;

        lrDepthBuffer = RenderTexture.GetTemporary(rtW, rtH, 0);

        //mask out everything except the skybox
        // we have 2 methods, one of which requires depth buffer support, the other one is just comparing images

        v4.Set(1.0f, 1.0f, 0f, 0f);
        sheet.properties.SetVector("_BlurRadius4", v4 * settings.sunShaftBlurRadius.value);
        v4.Set(v3.x, v3.y, v3.z, settings.maxRadius.value);
        sheet.properties.SetVector("_SunPosition", v4);
        sheet.properties.SetFloat("_NoSkyBoxMask", 1.0f - settings.useSkyBoxAlpha.value);

        if (!settings.useDepthTexture)
        {
            tmpDepthBuffer = RenderTexture.GetTemporary(context.width, context.height, 0);
            RenderTexture.active = tmpDepthBuffer;
            GL.ClearWithSkybox(false, settings.m_camera);
            
            sheet.properties.SetTexture("_Skybox", tmpDepthBuffer);
            context.command.BlitFullscreenTriangle(context.source, lrDepthBuffer, sheet, 3);

            RenderTexture.ReleaseTemporary(tmpDepthBuffer);
        }
        else
        {
            context.command.BlitFullscreenTriangle(context.source, lrDepthBuffer, sheet, 2); 
        }

        // paint a small black small border to get rid of clamping problems
        DrawBorder(lrDepthBuffer, settings.SimpleClear_Mat);
        
        // radial blur:

        var ofs = settings.sunShaftBlurRadius.value * (1.0f / 768.0f);

        v4.Set(ofs, ofs, 0f, 0f);
        sheet.properties.SetVector("_BlurRadius4", v4);
        v4.Set(v3.x, v3.y, v3.z, settings.maxRadius.value);
        sheet.properties.SetVector("_SunPosition", v4);

        for (idx = 0; idx < settings.radialBlurIterations.value; idx++ ) {
            // each iteration takes 2 * 6 samples
            // we update _BlurRadius each time to cheaply get a very smooth look

            lrColorB = RenderTexture.GetTemporary(rtW, rtH, 0);
            context.command.BlitFullscreenTriangle(lrDepthBuffer, lrColorB, sheet, 1); 
            RenderTexture.ReleaseTemporary(lrDepthBuffer);
            ofs = settings.sunShaftBlurRadius.value * (((idx * 2.0f + 1.0f) * 6.0f)) / 768.0f;
            v4.Set(ofs, ofs, 0f, 0f);
            sheet.properties.SetVector("_BlurRadius4", v4);

            lrDepthBuffer = RenderTexture.GetTemporary(rtW, rtH, 0);
            context.command.BlitFullscreenTriangle(lrColorB, lrDepthBuffer, sheet, 1);
            RenderTexture.ReleaseTemporary(lrColorB);
            ofs = settings.sunShaftBlurRadius.value * (((idx * 2.0f + 2.0f) * 6.0f)) / 768.0f;
            v4.Set(ofs, ofs, 0f, 0f);
            sheet.properties.SetVector("_BlurRadius4", v4);
        }

        // put together: 

        //no need to deal with pos.z...
        v4 = settings.sunColor.value * settings.sunShaftIntensity.value;
        sheet.properties.SetVector("_SunColor", v4);

        /*
        if (v3.z >= 0.0)
        {
            v4.Set(settings.sunColor.value.r, settings.sunColor.value.g, settings.sunColor.value.b, settings.sunColor.value.a);
            v4 *= settings.sunShaftIntensity.value;
            sheet.properties.SetVector("_SunColor", v4);
        }
        else
        {
            v4.Set(0f, 0f, 0f, 0f);
            sheet.properties.SetVector("_SunColor", v4); // no backprojection !
        }*/

        sheet.properties.SetTexture("_ColorBuffer", lrDepthBuffer);
        context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, (settings.screenBlendMode.value == SunShafts.ShaftsScreenBlendMode.Screen) ? 0 : 4);

        RenderTexture.ReleaseTemporary(lrDepthBuffer);
    }

    public override void Release()
    {
        settings.ClearMaterial();
        base.Release();
    }

    void DrawBorder(RenderTexture dest, Material material)
    {
        var x1 = 0f;
        var x2 = 0f;
        var y1 = 0f;
        var y2 = 0f;

        RenderTexture.active = dest;
        var invertY = true; // source.texelSize.y < 0.0f;
        // Set up the simple Matrix
        GL.PushMatrix();
        GL.LoadOrtho();

        for (var i = 0; i < material.passCount; i++)
        {
            material.SetPass(i);

            var y1_ = 0f;
            var y2_ = 0f;

            if (invertY)
            {
                y1_ = 1.0f;
                y2_ = 0f;
            }
            else
            {
                y1_ = 0f;
                y2_ = 1.0f;
            }

            // left	        
            x1 = 0f;
            x2 = 0f + 1.0f / (dest.width * 1.0f);
            y1 = 0f;
            y2 = 0f;
            GL.Begin(GL.QUADS);

            GL.TexCoord2(0f, y1_); GL.Vertex3(x1, y1, 0.1f);
            GL.TexCoord2(1.0f, y1_); GL.Vertex3(x2, y1, 0.1f);
            GL.TexCoord2(1.0f, y2_); GL.Vertex3(x2, y2, 0.1f);
            GL.TexCoord2(0f, y2_); GL.Vertex3(x1, y2, 0.1f);

            // right
            x1 = 1.0f - 1.0f / (dest.width * 1.0f);
            x2 = 1.0f;
            y1 = 0f;
            y2 = 1.0f;

            GL.TexCoord2(0f, y1_); GL.Vertex3(x1, y1, 0.1f);
            GL.TexCoord2(1.0f, y1_); GL.Vertex3(x2, y1, 0.1f);
            GL.TexCoord2(1.0f, y2_); GL.Vertex3(x2, y2, 0.1f);
            GL.TexCoord2(0f, y2_); GL.Vertex3(x1, y2, 0.1f);

            // top
            x1 = 0f;
            x2 = 1.0f;
            y1 = 0f;
            y2 = 0f + 1.0f / (dest.height * 1.0f);

            GL.TexCoord2(0f, y1_); GL.Vertex3(x1, y1, 0.1f);
            GL.TexCoord2(1.0f, y1_); GL.Vertex3(x2, y1, 0.1f);
            GL.TexCoord2(1.0f, y2_); GL.Vertex3(x2, y2, 0.1f);
            GL.TexCoord2(0f, y2_); GL.Vertex3(x1, y2, 0.1f);

            // bottom
            x1 = 0f;
            x2 = 1.0f;
            y1 = 1.0f - 1.0f / (dest.height * 1.0f);
            y2 = 1.0f;

            GL.TexCoord2(0f, y1_); GL.Vertex3(x1, y1, 0.1f);
            GL.TexCoord2(1.0f, y1_); GL.Vertex3(x2, y1, 0.1f);
            GL.TexCoord2(1.0f, y2_); GL.Vertex3(x2, y2, 0.1f);
            GL.TexCoord2(0f, y2_); GL.Vertex3(x1, y2, 0.1f);

            GL.End();
        }

        GL.PopMatrix();
    }
    
}
