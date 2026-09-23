Shader "LetMeSleep/Higgsfield/Glint"
{
    // v0.3.0 r3 warm reflection of a lamp on night water (maps-r3 #9, "reflejos verticales #FFB347 bajo cada farol").
    // A flat strip lying on the water surface (HiggsfieldAtmosphereVisuals.GlintQuad, uv.y = distance from the point
    // under the lamp toward the viewer) is turned toward the camera here, so in screen space it always reads as the
    // vertical broken glitter line of the sketches. Additive, stylized bands that drift slowly, faded by scene fog.
    // _GlintColor (sRGB, alpha = opacity) and _GlintSize (x width m, y length m, z seed) come from a MaterialPropertyBlock.
    Properties
    {
        _GlintColor("Glint color (alpha = opacity)", Color) = (1, 0.702, 0.278, 0.5)
        _GlintSize("Width, length (m), seed", Vector) = (0.5, 5, 0, 0)
        _GlintIntensity("HDR intensity", Range(0, 8)) = 2.2
        _BandFrequency("Dashes per metre", Range(0.5, 12)) = 2.4
        _BandSpeed("Band drift", Range(0, 4)) = 0.6
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Glint"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _GlintColor;
                float4 _GlintSize;
                half _GlintIntensity;
                half _BandFrequency;
                half _BandSpeed;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float fog:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            float Hash11(float p) { p = frac(p * 0.1031); p *= p + 33.33; p *= p + p; return frac(p); }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                float3 toCamera = GetCameraPositionWS() - origin;
                toCamera.y = 0.0;
                float3 along = dot(toCamera, toCamera) > 1e-4 ? normalize(toCamera) : float3(0, 0, 1);
                float3 across = float3(along.z, 0.0, -along.x);
                float3 world = origin + across * (input.positionOS.x * _GlintSize.x) + along * (input.positionOS.z * _GlintSize.y);
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float along = input.uv.y;
                float metres = along * _GlintSize.y;
                // Stacked horizontal dashes (the sketches' broken reflection), each with its own width and a small
                // sideways jitter, drifting slowly toward the viewer.
                float bandPosition = metres * _BandFrequency + _Time.y * _BandSpeed;
                float band = floor(bandPosition);
                float widthHash = Hash11(band + _GlintSize.z * 13.1);
                float shiftHash = Hash11(band * 1.7 + _GlintSize.z * 5.3 + 11.0);
                float inBand = frac(bandPosition);
                float halfWidth = lerp(0.2, 0.5, widthHash) * lerp(1.0, 0.55, along);
                float centerX = 0.5 + (shiftHash - 0.5) * 0.22;
                float dx = (input.uv.x - centerX) / max(halfWidth, 1e-3);
                float dy = (inBand - 0.5) / 0.3;
                float dash = 1.0 - smoothstep(0.7, 1.0, dx * dx + dy * dy);
                float fade = pow(saturate(1.0 - along), 1.2) * smoothstep(0.0, 0.05, along);
                float strength = dash * fade * lerp(0.6, 1.0, widthHash);
                half3 color = _GlintColor.rgb * (_GlintColor.a * _GlintIntensity * strength);
                color = MixFogColor(color, half3(0, 0, 0), input.fog);
                return half4(color, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
