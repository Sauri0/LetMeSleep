Shader "LetMeSleep/Higgsfield/Halo"
{
    // v0.3.0 camera-facing glow around lanterns, lamps and sconces (UI-06 / ENV-03 halos). Additive, screen
    // aligned, pulled toward the camera so the lantern body never clips it, faded by scene fog. Size comes
    // from the object scale; color (sRGB, alpha = opacity) and intensity from a MaterialPropertyBlock.
    // Occlusion: the camera depth at the halo center decides visibility (like a lens-flare test), so a lamp
    // behind a wall never shows its glow through that wall even though the quad is pulled toward the camera.
    Properties
    {
        _HaloColor("Halo color (alpha = opacity)", Color) = (1, 0.702, 0.278, 0.35)
        _HaloIntensity("Intensity", Float) = 1
        _HaloPull("Pull toward camera (m)", Float) = 0.3
        _HaloFalloff("Falloff exponent", Range(0.5, 4)) = 1.8
        _HaloCore("Core boost", Range(0, 2)) = 0.6
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Halo"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _HaloColor;
                half _HaloIntensity;
                float _HaloPull;
                half _HaloFalloff;
                half _HaloCore;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 local:TEXCOORD0; float fog:TEXCOORD1; float visible:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 center = TransformObjectToWorld(float3(0, 0, 0));
                float scale = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                float3 toCamera = GetCameraPositionWS() - center;
                float distance = max(length(toCamera), 1e-3);
                float3 forward = toCamera / distance;
                // Screen-aligned quad: camera right/up rows of the view matrix.
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                // Pull toward the camera so the quad clears the lantern body, glazing and wall, and shrink it by the
                // same perspective factor so its apparent (angular) size stays the configured one.
                float pull = min(max(_HaloPull, 1.2 * scale), distance * 0.6);
                float keep = (distance - pull) / distance;
                // Depth occlusion of the halo center (the lamp itself may sit up to ~0.6 size in front of it).
                float4 centerCS = TransformWorldToHClip(center);
                float visible = 1.0;
                if (centerCS.w > 1e-3)
                {
                    float2 uv = centerCS.xy / centerCS.w * 0.5 + 0.5;
                    #if UNITY_UV_STARTS_AT_TOP
                        if (_ProjectionParams.x < 0) uv.y = 1.0 - uv.y;
                    #endif
                    if (all(uv > 0.0) && all(uv < 1.0))
                    {
                        float sceneEye = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                        float tolerance = max(0.35, 0.6 * scale);
                        visible = saturate((sceneEye - (centerCS.w - tolerance)) / (0.25 * tolerance + 0.05));
                    }
                }
                scale *= visible;
                float3 world = center + forward * pull + (right * input.positionOS.x + up * input.positionOS.y) * (scale * keep);
                output.positionCS = TransformWorldToHClip(world);
                output.local = input.positionOS.xy * 2.0;
                output.fog = ComputeFogFactor(output.positionCS.z);
                output.visible = visible;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float r = saturate(length(input.local));
                half glow = pow(1.0 - r, _HaloFalloff);
                glow += _HaloCore * pow(1.0 - r, _HaloFalloff * 4.0);
                half3 color = _HaloColor.rgb * (_HaloColor.a * _HaloIntensity * glow * input.visible);
                color = MixFogColor(color, half3(0, 0, 0), input.fog);
                return half4(color, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
