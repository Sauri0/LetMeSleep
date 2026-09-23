Shader "LetMeSleep/Higgsfield/Pool"
{
    // v0.3.0 r3 warm pool of light on the ground under a lantern (maps-r3 #3, "charco en el suelo #6A4A2A de unos
    // 3 m de radio bajo cada poste"). A box decal: the unit cube (HiggsfieldAtmosphereVisuals.Box) is drawn from the
    // inside faces, the scene depth at each pixel gives the visible surface, and only surfaces inside the box receive
    // a round, soft warm tint (strongest at the center, fading to the radius and with height above the ground). Grass,
    // dirt and rocks under the post read as one warm pool whatever their own color; nothing outside the box changes.
    // _PoolColor is sRGB (alpha = opacity at the center), set per instance with a MaterialPropertyBlock.
    Properties
    {
        _PoolColor("Pool color (sRGB, alpha = center opacity)", Color) = (0.45, 0.31, 0.18, 0.55)
        _PoolFalloff("Radial falloff", Range(0.3, 4)) = 1.3
    }
    SubShader
    {
        Tags { "Queue"="Transparent-50" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Pool"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Front
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _PoolColor;
                half _PoolFalloff;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                #if UNITY_REVERSED_Z
                    float depth = SampleSceneDepth(uv);
                #else
                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
                #endif
                float3 world = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float3 local = TransformWorldToObject(world);
                clip(0.5 - abs(local));
                float radial = saturate(length(local.xz) * 2.0);
                float fall = pow(1.0 - radial, _PoolFalloff);
                // The box spans from 0.35 below to 0.65 above the ground (local y = -0.5 .. 0.5, ground at -0.15).
                float height = saturate(1.0 - max(local.y + 0.15, 0.0) / 0.5);
                half alpha = _PoolColor.a * fall * height;
                half3 color = MixFog(_PoolColor.rgb, ComputeFogFactor(TransformWorldToHClip(world).z));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
