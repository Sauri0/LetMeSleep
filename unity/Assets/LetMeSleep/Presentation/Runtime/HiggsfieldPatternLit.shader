Shader "LetMeSleep/Higgsfield/PatternLit"
{
    // v0.3.0 r4 (maps director corrections): URP/Lit (LitInput + LitForwardPass, like InteriorLit) with a small procedural
    // surface pattern chosen by _Pattern, for map swatches that need more than a flat color without new geometry or UVs:
    //   0 plain: optional split by face orientation (_BaseColor on tops, _SideColor on vertical faces: wooden stair treads
    //     and risers) and _NormalFlatten (pulls upward faces toward +Y so bevelled cobbles stop flashing in the moonlight).
    //   1 stone blocks: world-space running-bond blocks (_BlockSize, m) with _MortarColor joints, per-block tone and a
    //     per-block facet tilt of the normal (faceted fireplace stone, ENV-03 02/04).
    //   2 ember rim: _EmberColor emission on the silhouette edges only (fresnel), for hearth logs glowing at their borders.
    //   3 porthole: round window centered in the renderer's world box (_PaneMin/_PaneMax, set by HiggsfieldMapLighting), in the
    //     plane the pixel faces: sky over a clean sea horizon inside, a _RimColor brass ring, _SideColor wall outside it.
    //   4 lantern glass: the band of vertical faces inside the renderer box (_GlassBand: height from, height to, radius) emits
    //     _EmissionColor (lit glass of a table/wall lantern); frame, cap and base keep _BaseColor.
    // _AmbientLift (linear) adds albedo x lift as emission on every pattern: the warm bounce of nearby night lamps on faces
    // that neither the moon nor a lamp reaches (wooden stair risers).
    // Everything else (lighting, shadows, SSAO, fog, light layers, per-renderer ambient probes) is URP/Lit's. The pattern
    // properties live outside UnityPerMaterial, so these few materials are drawn without the SRP Batcher.
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Base / top color", Color) = (1, 1, 1, 1)
        _Pattern("Pattern (0 plain, 1 stone, 2 ember rim, 3 porthole, 4 lantern glass)", Float) = 0
        _SideColor("Vertical face / wall color (sRGB)", Color) = (1, 1, 1, 1)
        _SplitByNormal("Split tops and sides (0/1)", Float) = 0
        _NormalFlatten("Flatten upward normals (0..1)", Range(0, 1)) = 0
        _BlockSize("Stone block width, height (m)", Vector) = (0.3, 0.2, 0, 0)
        _MortarColor("Mortar color (sRGB)", Color) = (0.3, 0.28, 0.27, 1)
        _MortarWidth("Mortar width (m)", Range(0, 0.06)) = 0.018
        _BlockVariation("Per-block tone variation", Range(0, 0.5)) = 0.14
        _BlockTilt("Per-block facet tilt", Range(0, 1)) = 0.35
        _EmberColor("Ember color (linear HDR)", Color) = (1, 0.19, 0.02, 1)
        _EmberPower("Ember rim tightness", Range(0.5, 8)) = 3
        _RimColor("Porthole ring color (sRGB)", Color) = (0.69, 0.54, 0.23, 1)
        _RingWidth("Porthole ring width (fraction of radius)", Range(0.02, 0.5)) = 0.2
        _PortholeScale("Porthole radius (fraction of the box)", Range(0.3, 1)) = 0.92
        _SkyColor("Porthole sky (sRGB)", Color) = (0.549, 0.784, 0.941, 1)
        _SkyTopColor("Porthole sky top (sRGB)", Color) = (0.42, 0.7, 0.92, 1)
        _SeaColor("Porthole sea (sRGB)", Color) = (0.184, 0.561, 0.816, 1)
        _SeaDeepColor("Porthole sea bottom (sRGB)", Color) = (0.15, 0.47, 0.72, 1)
        _Horizon("Porthole horizon height (fraction of radius)", Range(-0.8, 0.8)) = 0.05
        _ViewIntensity("Porthole view brightness", Range(0, 4)) = 1
        _GlassBand("Lantern glass: height from, to (0..1), radius (0..1)", Vector) = (0.3, 0.72, 0.82, 0)
        _AmbientLift("Warm fill (linear, x albedo), all patterns", Color) = (0, 0, 0, 0)
        _PaneMin("Renderer box min (world, set per renderer)", Vector) = (0, 0, 0, 0)
        _PaneMax("Renderer box max (world, set per renderer)", Vector) = (0, 0, 0, 0)
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
            #pragma fragment PatternLitFragment
            #pragma shader_feature_local_fragment _EMISSION
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
            #define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            float _Pattern;
            half4 _SideColor;
            float _SplitByNormal;
            half _NormalFlatten;
            float4 _BlockSize;
            half4 _MortarColor;
            float _MortarWidth;
            half _BlockVariation;
            half _BlockTilt;
            half4 _EmberColor;
            half _EmberPower;
            half4 _RimColor;
            half _RingWidth;
            half _PortholeScale;
            half4 _SkyColor;
            half4 _SkyTopColor;
            half4 _SeaColor;
            half4 _SeaDeepColor;
            half _Horizon;
            half _ViewIntensity;
            float4 _GlassBand;
            half4 _AmbientLift;
            float4 _PaneMin;
            float4 _PaneMax;

            float Hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }

            // Stone blocks projected on the plane that faces the dominant normal axis.
            void StoneBlocks(float3 positionWS, inout half3 normalWS, inout half3 albedo)
            {
                float3 n = abs(normalWS);
                float2 plane = n.y > max(n.x, n.z) ? positionWS.xz : (n.x > n.z ? positionWS.zy : positionWS.xy);
                float2 size = max(_BlockSize.xy, 0.02);
                float row = floor(plane.y / size.y);
                // Irregular stonework, not bricks: every course has its own block length (0.65x..1.4x) and offset.
                size.x *= lerp(0.65, 1.4, Hash21(float2(row, 3.0)));
                float u = plane.x / size.x + (fmod(abs(row), 2.0) > 0.5 ? 0.5 : 0.0) + Hash21(float2(row, 7.0)) * 0.35;
                float column = floor(u);
                float2 inBlock = float2(frac(u) * size.x, frac(plane.y / size.y) * size.y);
                float2 edge = min(inBlock, size - inBlock);
                float mortar = 1.0 - smoothstep(_MortarWidth * 0.5, _MortarWidth, min(edge.x, edge.y));
                float2 id = float2(column, row);
                half tone = 1.0 + (Hash21(id) - 0.5) * 2.0 * _BlockVariation;
                half warm = (Hash21(id + 17.0) - 0.5) * 0.08;
                half3 stone = albedo * tone * half3(1.0 + warm, 1.0, 1.0 - warm);
                albedo = lerp(stone, _MortarColor.rgb, mortar);
                // Facet: tilt the normal of each block a little in the block plane so neighbours catch the light differently.
                float3 up = n.y > max(n.x, n.z) ? float3(0, 0, 1) : float3(0, 1, 0);
                float3 t1 = normalize(cross(up, normalWS));
                float3 t2 = cross(normalWS, t1);
                float2 tilt = (float2(Hash21(id + 3.1), Hash21(id + 9.7)) - 0.5) * 2.0 * _BlockTilt * (1.0 - mortar);
                normalWS = normalize(normalWS + t1 * tilt.x + t2 * tilt.y);
            }

            // Returns true where the pixel belongs to the round porthole; writes albedo/emission for ring and view.
            bool Porthole(float3 positionWS, half3 normalWS, inout half3 albedo, inout half3 emission)
            {
                float3 lo = _PaneMin.xyz, hi = _PaneMax.xyz;
                float3 size = hi - lo;
                if (all(size <= 1e-4)) return false;
                float3 center = (lo + hi) * 0.5;
                float3 d = positionWS - center;
                // The pane faces along its dominant normal axis; the circle lives in the other two axes of the renderer box
                // (the box may include a deep frame, so its thinnest axis is not reliable).
                float3 n = abs(normalWS);
                float2 q; float2 extent;
                if (n.x >= n.y && n.x >= n.z) { q = d.zy; extent = size.zy; }
                else if (n.z >= n.y) { q = d.xy; extent = size.xy; }
                else { q = d.xz; extent = size.xz; }
                float radius = 0.5 * min(extent.x, extent.y) * _PortholeScale;
                float r = length(q);
                if (r > radius) { albedo = _SideColor.rgb; return true; }
                float inner = radius * (1.0 - _RingWidth);
                if (r > inner)
                {
                    float t = (r - inner) / max(radius - inner, 1e-4);
                    half bevel = lerp(0.7, 1.12, smoothstep(0.0, 0.35, t)) * lerp(1.0, 0.82, smoothstep(0.75, 1.0, t));
                    albedo = _RimColor.rgb * bevel;
                    return true;
                }
                float h = q.y / max(inner, 1e-4);
                half3 view;
                if (h > _Horizon) view = lerp(_SkyColor.rgb, _SkyTopColor.rgb, saturate((h - _Horizon) / (1.0 - _Horizon)));
                else view = lerp(_SeaColor.rgb, _SeaDeepColor.rgb, saturate((_Horizon - h) / (1.0 + _Horizon)));
                albedo = half3(0.0, 0.0, 0.0);
                emission = view * _ViewIntensity;
                return true;
            }

            bool LanternGlass(float3 positionWS, half3 normalWS)
            {
                float3 lo = _PaneMin.xyz, hi = _PaneMax.xyz;
                float3 size = hi - lo;
                if (all(size <= 1e-4) || abs(normalWS.y) > 0.6) return false;
                float h = (positionWS.y - lo.y) / max(size.y, 1e-4);
                float2 c = (lo.xz + hi.xz) * 0.5;
                float radial = length(positionWS.xz - c) / max(0.5 * max(size.x, size.z), 1e-4);
                return h >= _GlassBand.x && h <= _GlassBand.y && radial <= _GlassBand.z;
            }

            void PatternLitFragment(Varyings input, out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceData surfaceData;
                InitializeStandardLitSurfaceData(input.uv, surfaceData);
                InputData inputData;
                InitializeInputData(input, surfaceData.normalTS, inputData);
                InitializeBakedGIData(input, inputData);
                half3 n = inputData.normalWS;
                int pattern = (int)round(_Pattern);
                if (_SplitByNormal > 0.5 && n.y < 0.5) surfaceData.albedo = _SideColor.rgb;
                if (_NormalFlatten > 0.0 && n.y > 0.2) inputData.normalWS = normalize(lerp(n, half3(0, 1, 0), _NormalFlatten));
                if (pattern == 1) StoneBlocks(input.positionWS, inputData.normalWS, surfaceData.albedo);
                else if (pattern == 2)
                {
                    half rim = pow(saturate(1.0 - abs(dot(n, inputData.viewDirectionWS))), _EmberPower);
                    surfaceData.emission += _EmberColor.rgb * rim;
                }
                else if (pattern == 3) Porthole(input.positionWS, n, surfaceData.albedo, surfaceData.emission);
                else if (pattern == 4 && LanternGlass(input.positionWS, n))
                {
                    surfaceData.albedo = half3(0.02, 0.015, 0.01);
                    surfaceData.emission = _EmissionColor.rgb;
                }
                if (pattern == 4 && !LanternGlass(input.positionWS, n)) surfaceData.emission = half3(0, 0, 0);
                // Warm bounce the night lights would give (stairs by the path lanterns): albedo x lift, all faces.
                surfaceData.emission += surfaceData.albedo * _AmbientLift.rgb;
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
