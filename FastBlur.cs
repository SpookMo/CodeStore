using UnityEngine;
using System;
using UnityEngine.Rendering.PostProcessing;

[ExecuteAlways]
[Serializable]
[PostProcess(typeof(FastBlurRender), PostProcessEvent.BeforeStack, "Custom/FastBlur")]
public sealed class FastBlur : PostProcessEffectSettings
{
    public enum BlurType
    {
        StandardGauss = 0,
        SgxGauss = 1,
    }

    [Range(0,3)]
    public IntParameter downsample = new IntParameter { value = 1 };

    [Range(0.0f, 10.0f)]
    public FloatParameter blurSize = new FloatParameter { value = 3.0f };
	
	[Range(1, 4)]
    public IntParameter blurIterations = new IntParameter { value = 2 };

	public ParameterOverride<BlurType> blurType = new ParameterOverride<BlurType> { value = BlurType.StandardGauss };

    public ParameterOverride<Shader> blurShader = new ParameterOverride<Shader> { value = null };

    private Material blurMaterial;

    public Material BlurMaterial
    {
        get
        {
            if (blurMaterial == null)
            {
                blurMaterial = new Material(blurShader);
                blurMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            return blurMaterial;
        }
    }
    public void ClearMaterial()
    {
        if (blurMaterial != null)
            DestroyImmediate(blurMaterial);

    }

    public override bool IsEnabledAndSupported(PostProcessRenderContext context)
    {
        if(blurShader.value == null || !blurShader.value.isSupported || BlurMaterial == null)
        {
           enabled.value = false;
        }

        return enabled.value;
    }

}

public sealed class FastBlurRender : PostProcessEffectRenderer<FastBlur>
{
    internal Vector4 v4;
    float widthMod;
    int rtW, rtH;
    RenderTexture rt, rt2;

    public override void Render(PostProcessRenderContext context)
    {
        if(!settings.enabled.value)
        {
            context.command.BlitFullscreenTriangle(context.source, context.destination);
            return;
        }

        var sheet = context.propertySheets.Get(settings.BlurMaterial.shader);

        widthMod = 1.0f / (1.0f * (1 << settings.downsample.value));


        v4.Set(settings.blurSize.value * widthMod, -settings.blurSize.value * widthMod, 0f, 0f);
        sheet.properties.SetVector("_Parameter", v4);

        //Here is different from old script,I don't know what'the fuck with rendertexture in unity update...
        rtW = context.width >> (1 + settings.downsample.value);
        rtH = context.height >> (1 + settings.downsample.value);

        // downsample
        rt = RenderTexture.GetTemporary(rtW, rtH, 0, context.sourceFormat);
        
        rt.filterMode = FilterMode.Bilinear;

        context.command.BlitFullscreenTriangle(context.source, rt, sheet, 0);
        
        var passOffs = settings.blurType.value == FastBlur.BlurType.StandardGauss ? 0 : 2;
        
        for (var i = 0; i < settings.blurIterations.value; i++) {
            var iterationOffs = i * 1.0f;

            v4.Set(settings.blurSize.value * widthMod + iterationOffs, -settings.blurSize.value * widthMod - iterationOffs, 0f, 0f);
            sheet.properties.SetVector("_Parameter", v4);

            // vertical blur
            rt2 = RenderTexture.GetTemporary(rtW, rtH, 0, context.sourceFormat);
            rt2.filterMode = FilterMode.Bilinear;
            context.command.BlitFullscreenTriangle(rt, rt2, sheet, 1 + passOffs);

            RenderTexture.ReleaseTemporary(rt);
            rt = rt2;
            
            
            // horizontal blur
            rt2 = RenderTexture.GetTemporary(rtW, rtH, 0, context.sourceFormat);
            rt2.filterMode = FilterMode.Bilinear;
            context.command.BlitFullscreenTriangle(rt, rt2, sheet, 2 + passOffs);
            
            RenderTexture.ReleaseTemporary(rt);
            rt = rt2;
        }
        
        context.command.BlitFullscreenTriangle(rt, context.destination);

        RenderTexture.ReleaseTemporary(rt);

    }

    public override void Release()
    {
        settings.ClearMaterial();
        base.Release();
    }

}
