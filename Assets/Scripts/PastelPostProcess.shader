Shader "Hidden/PastelPostProcess"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags 
        { 
            "RenderPipeline" = "UniversalPipeline" 
            "RenderType" = "Opaque"
        }
        LOD 100
        ZTest Always 
        ZWrite Off 
        Cull Off

        Pass
        {
            Name "PastelDream"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Uniforms passed from the C# controller
            float _PastelStrength;
            float4 _PastelTint;
            float _Saturation;
            float _ShadowLift;
            float _Warmth;
            float _Dreaminess;
            float _BlurOffset;
            float _VignetteStrength;
            float _VignetteRange;
            float4 _VignetteColor;

            // Helper function to sample the input texture using default blit definitions
            float4 SampleScene(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float4 original = SampleScene(uv);

                // --- 1. Dreamy Orton Blur (Soft Glow) ---
                // A 9-tap blur scaled by _BlurOffset.
                float2 texelSize = _BlitTexture_TexelSize.xy * _BlurOffset;
                float4 blur = float4(0, 0, 0, 0);

                float2 offsets[9] = {
                    float2(-1.0, -1.0), float2(0.0, -1.0), float2(1.0, -1.0),
                    float2(-1.0,  0.0), float2(0.0,  0.0), float2(1.0,  0.0),
                    float2(-1.0,  1.0), float2(0.0,  1.0), float2(1.0,  1.0)
                };

                float weights[9] = {
                    0.09, 0.12, 0.09,
                    0.12, 0.16, 0.12,
                    0.09, 0.12, 0.09
                };

                for (int i = 0; i < 9; i++)
                {
                    blur += SampleScene(uv + offsets[i] * texelSize) * weights[i];
                }

                // Blend the original image with the blurred glow
                float3 glow = original.rgb + blur.rgb * _Dreaminess;

                // --- 2. Saturation ---
                // Calculate luminance and desaturate to create a soft, clean pastel base
                float luma = dot(glow, float3(0.2126, 0.7152, 0.0722));
                float3 desat = lerp(float3(luma, luma, luma), glow, _Saturation);

                // --- 3. Shadow Lift ---
                // Lifts shadows to soften deep blacks, giving a low-contrast matte appearance
                float3 lifted = lerp(desat, desat + _ShadowLift * 0.15 * (1.0 - luma), 1.0 - luma);

                // --- 4. Warmth / Temperature Shift ---
                // Shift colors towards soft warm tones (peachy orange) and soft cool tones (lavender)
                float3 warmTone = float3(1.04, 0.97, 0.91);
                float3 coolTone = float3(0.94, 0.97, 1.03);
                float3 temperature = lerp(coolTone, warmTone, _Warmth);
                float3 colored = lifted * temperature;

                // --- 5. Pastel Tint & Milkiness Wash ---
                // Blend with a customizable soft color overlay (e.g. warm pink)
                float3 tinted = lerp(colored, colored * _PastelTint.rgb, _PastelStrength);
                // Add soft ambient milkiness to shadows
                float3 finalColor = lerp(tinted, tinted + _PastelTint.rgb * 0.08 * (1.0 - luma), _PastelStrength);

                // --- 6. Cozy Colored Vignette ---
                float2 uvDist = uv - float2(0.5, 0.5);
                float dist = length(uvDist);
                float vignette = smoothstep(0.8, 0.8 - _VignetteRange * 0.5, dist);
                finalColor = lerp(_VignetteColor.rgb, finalColor, lerp(1.0, vignette, _VignetteStrength));

                return float4(finalColor, original.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Copy"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragCopy

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 FragCopy(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
