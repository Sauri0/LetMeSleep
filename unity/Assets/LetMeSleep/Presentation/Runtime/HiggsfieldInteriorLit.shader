Shader "LetMeSleep/Higgsfield/InteriorLit"
{
    // v0.3.0 r3 (maps-r3 #7): URP/Lit for shells whose single material covers both sides of a wall or roof. Faces
    // inside one of the map's interior volumes (published by HiggsfieldMapLighting as _LMS_Interior*) whose normal
    // points back into the room use the interior palette: walls _InteriorColor with a _TrimColor baseboard up to
    // _TrimHeight above the room floor, ceilings/roof undersides _CeilingColor. Every other face keeps _BaseColor
    // (e.g. the facade seen from the yard). Lighting, shadows, SSAO, fog, light layers and the per-renderer indoor
    // ambient probe are exactly URP/Lit's (LitInput + LitForwardPass). The interior palette lives outside
    // UnityPerMaterial, so these few shells are drawn without the SRP Batcher.
    // v0.3.0 r4 (director #7): _ExteriorFill (linear) adds albedo x fill on exterior faces only, the warm bounce of a lit
    // village on plaster that the (shadowless, open-air only) sky fill must not reach; interior faces are unaffected.
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Exterior color", Color) = (1, 1, 1, 1)
        _InteriorColor("Interior wall color (sRGB)", Color) = (0.91, 0.863, 0.773, 1)
        _CeilingColor("Interior ceiling color (sRGB)", Color) = (0.353, 0.227, 0.141, 1)
        _TrimColor("Baseboard color (sRGB)", Color) = (0.545, 0.353, 0.169, 1)
        _TrimHeight("Baseboard height above the room floor (m, 0 = none)", Range(0, 0.6)) = 0.14
        _InteriorFaceMargin("Face distance to interior (m)", Range(0, 1)) = 0.35
        _ExteriorFill("Exterior warm fill (linear, x albedo)", Color) = (0, 0, 0, 0)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0
        _Metallic("Metallic", Range(0.0, 1.0)) = 0
        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
        _EmissionColor("Emission", Color) = (0, 0, 0)
        _Surface("__surface", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        _ReceiveShadows("Receive Shadows", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" "IgnoreProjector"="True" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull[_Cull]
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex LitPassVertex
            #pragma fragment InteriorLitFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            // World position per pixel is needed for the interior test even without shadows.
            #define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            half4 _InteriorColor;
            half4 _CeilingColor;
            half4 _TrimColor;
            half _TrimHeight;
            half _InteriorFaceMargin;
            half4 _ExteriorFill;
            float4 _LMS_InteriorMin[8];
            float4 _LMS_InteriorMax[8];
            float _LMS_InteriorCount;

            half3 InteriorAlbedo(float3 positionWS, half3 normalWS, half3 exterior, out bool outside)
            {
                outside = false;
                int count = (int)_LMS_InteriorCount;
                [unroll] for (int i = 0; i < 8; i++)
                {
                    if (i >= count) break;
                    float3 lo = _LMS_InteriorMin[i].xyz, hi = _LMS_InteriorMax[i].xyz;
                    if (any(positionWS < lo - _InteriorFaceMargin) || any(positionWS > hi + _InteriorFaceMargin)) continue;
                    float3 toCenter = (lo + hi) * 0.5 - positionWS;
                    // Only faces looking back into the room; the outer skin of the same wall keeps the exterior color.
                    if (dot(normalWS, toCenter) <= 0.0) continue;
                    if (normalWS.y < -0.5) return _CeilingColor.rgb;
                    if (normalWS.y > 0.5) return exterior; // Floors, sills and shelves keep their own color (not outside).
                    float aboveFloor = positionWS.y - lo.y;
                    if (_TrimHeight > 0.0 && aboveFloor > -0.05 && aboveFloor < _TrimHeight) return _TrimColor.rgb;
                    return _InteriorColor.rgb;
                }
                outside = true;
                return exterior;
            }

            void InteriorLitFragment(Varyings input, out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceData surfaceData;
                InitializeStandardLitSurfaceData(input.uv, surfaceData);
                bool outside;
                surfaceData.albedo = InteriorAlbedo(input.positionWS, normalize(input.normalWS.xyz), surfaceData.albedo, outside);
                if (outside) surfaceData.emission += surfaceData.albedo * _ExteriorFill.rgb;
                InputData inputData;
                InitializeInputData(input, surfaceData.normalTS, inputData);
                InitializeBakedGIData(input, inputData);
                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1.0;
                outColor = color;
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull[_Cull]
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull[_Cull]
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
