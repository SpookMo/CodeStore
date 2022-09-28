using UnityEngine;
using System;
using UnityEngine.Rendering.PostProcessing;
using System.Linq;

#if UNITY_2018_3_OR_NEWER
[ExecuteAlways]
#else
[ExecuteInEditMode]
#endif
[Serializable]
[PostProcess(typeof(NoiseEffectRender),PostProcessEvent.AfterStack,"Custom/NoiseAndGrain")]
public sealed class NoiseEffect : PostProcessEffectSettings
{
   
    [Tooltip("Monochrome noise just adds grain. Non-monochrome noise more resembles VCR as it adds noise in YUV color space,thus introducing magenta/green colors")]
    public BoolParameter monochrome = new BoolParameter { value = true };

    [Tooltip("Noise grain takes random intensity from Min to Max")]
    [Range(0f,5.0f)]
    public FloatParameter grainIntensityMin = new FloatParameter { value = 0.1f };
    [Range(0f, 5.0f)]
    public FloatParameter grainIntensityMax = new FloatParameter { value = 0.2f };

    [Tooltip("The size of the noise grains (1 = one pixel)")]
    [Range(0.1f, 50.0f)]
    public FloatParameter grainSize = new FloatParameter { value = 2.0f };

    [Tooltip("Scratches take random intensity from Min to Max")]
    [Range(0f, 5.0f)]
    public FloatParameter scratchIntensityMin = new FloatParameter { value = 0.05f };
    [Range(0f, 5.0f)]
    public FloatParameter scratchIntensityMax = new FloatParameter { value = 0.25f };

    [Tooltip("Scratches jump to another locations at this times per second")]
    [Range(1.0f, 30.0f)]
    public FloatParameter scratchFPS = new FloatParameter { value = 10.0f };

    [Tooltip("While scratches are in the same location, they jitter a bit")]
    [Range(0f, 1.0f)]
    public FloatParameter scratchJitter = new FloatParameter { value = 0.01f };

    public TextureParameter grainTexture = new TextureParameter { value = null };
    public TextureParameter scratchTexture = new TextureParameter { value = null };
    public ParameterOverride<Shader> shaderRGB = new ParameterOverride<Shader> { value = null };
    public ParameterOverride<Shader> shaderYUV = new ParameterOverride<Shader> { value = null };

	private Material m_MaterialRGB;
	private Material m_MaterialYUV;	
	public float scratchTimeLeft;
    private bool rgbFallback;


    public override bool IsEnabledAndSupported(PostProcessRenderContext context)
    {     
		if( shaderRGB.value == null || shaderYUV.value == null )
		{
            enabled.value = false;
		}
		else
		{
            if (!shaderRGB.value.isSupported) // disable effect if RGB shader is not supported
            {
                rgbFallback = false;
            }
            else if (!shaderYUV.value.isSupported) // fallback to RGB if YUV is not supported
                rgbFallback = true;
		}
        
        scratchTimeLeft = 0f;

        return enabled.value;
    }
	
	public Material material {
		get {
			if( m_MaterialRGB == null ) {
				m_MaterialRGB = new Material( shaderRGB );
				m_MaterialRGB.hideFlags = HideFlags.HideAndDontSave;
			}
			if( m_MaterialYUV == null && !rgbFallback ) {
				m_MaterialYUV = new Material( shaderYUV );
				m_MaterialYUV.hideFlags = HideFlags.HideAndDontSave;
			}
			return (!rgbFallback && !monochrome) ? m_MaterialYUV : m_MaterialRGB;
		}
	}
    
    public void ClearMaterial() {
		if( m_MaterialRGB )
			DestroyImmediate( m_MaterialRGB );
		if( m_MaterialYUV )
			DestroyImmediate( m_MaterialYUV );

        Debug.Log("Noise Effect ClearMaterial");
    }
}

[UnityEngine.Scripting.Preserve]
public sealed class NoiseEffectRender : PostProcessEffectRenderer<NoiseEffect>
{
    internal Vector4 v4;
    float grainScale, grainX, grainY, scratchX, scratchY;
    public override void Render(PostProcessRenderContext context)
    {
        var mat = settings.material;

        if (settings.scratchTimeLeft <= 0.0f)
        {
            settings.scratchTimeLeft = UnityEngine.Random.value * 2 / settings.scratchFPS.value; // we have sanitized it earlier, won't be zero
            scratchX = UnityEngine.Random.Range(settings.scratchIntensityMin, settings.scratchIntensityMax);
            scratchY = UnityEngine.Random.Range(settings.scratchIntensityMin, settings.scratchIntensityMax);
        }
        settings.scratchTimeLeft -= Time.deltaTime;

        var sheet = context.propertySheets.Get(mat.shader);
        sheet.properties.SetTexture("_GrainTex", settings.grainTexture.value);
        sheet.properties.SetTexture("_ScratchTex", settings.scratchTexture.value);
        grainScale = 1.0f / settings.grainSize.value;
        grainX = UnityEngine.Random.Range(settings.grainIntensityMin, settings.grainIntensityMax);
        grainY = UnityEngine.Random.Range(settings.grainIntensityMin, settings.grainIntensityMax);
        v4.Set(grainX, grainY, Screen.width / settings.grainTexture.value.width * grainScale, Screen.height / settings.grainTexture.value.height * grainScale);
        sheet.properties.SetVector("_GrainOffsetScale", v4);
        v4.Set(scratchX + UnityEngine.Random.value * settings.scratchJitter, scratchY + UnityEngine.Random.value * settings.scratchJitter, Screen.width / settings.scratchTexture.value.width, Screen.height / settings.scratchTexture.value.height);
        sheet.properties.SetVector("_ScratchOffsetScale", v4);
        v4.Set(grainX, grainY, 0f, 0f);
        sheet.properties.SetVector("_Intensity", v4);
        context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);
    }

    public override void Release()
    {
        settings.ClearMaterial();

        base.Release();
    }

}
