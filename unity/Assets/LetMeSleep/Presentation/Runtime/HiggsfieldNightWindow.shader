Shader "LetMeSleep/Higgsfield/NightWindow"
{
    // v0.3.0 windows coherent with the night (art direction #4). Seen from outside, the pane glows warm amber
    // (HDR 1.5-2, so bloom spills onto the frame). Seen from inside one of the map's interior volumes (published
    // by HiggsfieldMapLighting as _LMS_Interior*), the same pane shows the night outside: navy sky, stars, a
    // stylized moon and dark hill silhouettes, sampled along the view ray so it reads as depth, not a sticker.
    // _ForceNight = 1 always shows the night (menu/lobby window).
    Properties
    {
        _OutsideColor("Outside glow (sRGB)", Color) = (1, 0.72, 0.36, 1)
        _OutsideIntensity("Outside HDR intensity", Range(0, 8)) = 1.8
        _OutsideGradient("Outside vertical gradient", Range(0, 1)) = 0.3
        _SkyHorizon("Night horizon (sRGB)", Color) = (0.102, 0.165, 0.416, 1)
        _SkyZenith("Night zenith (sRGB)", Color) = (0.043, 0.071, 0.2, 1)
        _HillColor("Hill silhouette (sRGB)", Color) = (0.035, 0.05, 0.1, 1)
        _StarDensity("Star density", Range(0, 1)) = 0.45
        _StarColor("Star color (HDR)", Color) = (1.2, 1.2, 1.3, 1)
        _MoonDirection("Moon direction (world xyz)", Vector) = (0.4, 0.45, 0.8, 0)
        _MoonSize("Moon angular radius (deg)", Range(0.2, 8)) = 2.2
        _MoonColor("Moon color (sRGB)", Color) = (1, 0.953, 0.82, 1)
        _MoonIntensity("Moon intensity", Range(0, 4)) = 1
        _MoonPhase("Crescent (0 = full)", Range(0, 0.95)) = 0
        _MoonGlow("Moon halo opacity", Range(0, 0.5)) = 0.25
        _ForceNight("Always night (menu)", Float) = 0
        _InteriorMargin("Pane distance to interior (m)", Range(0, 1)) = 0.4
        _Muntins("Muntin width (0 = none, uv)", Range(0, 0.08)) = 0
        _MuntinColor("Muntin color (sRGB)", Color) = (0.2, 0.12, 0.07, 1)
    }
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _OutsideColor;
            half _OutsideIntensity;
            half _OutsideGradient;
            half4 _SkyHorizon;
            half4 _SkyZenith;
            half4 _HillColor;
            half _StarDensity;
            half4 _StarColor;
            float4 _MoonDirection;
            half _MoonSize;
            half4 _MoonColor;
            half _MoonIntensity;
            half _MoonPhase;
            half _MoonGlow;
            half _ForceNight;
            half _InteriorMargin;
            half _Muntins;
            half4 _MuntinColor;
        CBUFFER_END
        float4 _LMS_InteriorMin[8];
        float4 _LMS_InteriorMax[8];
        float _LMS_InteriorCount;
    ENDHLSL
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "NightWindow"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1; float fog:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };

            float Hash31(float3 p) { p = frac(p * 0.1031); p += dot(p, p.zyx + 31.32); return frac((p.x + p.y) * p.z); }
            float3 Hash33(float3 p) { p = frac(p * float3(0.1031, 0.1030, 0.0973)); p += dot(p, p.yxz + 33.33); return frac((p.xxy + p.yxx) * p.zyx); }
            float Disc(float3 d, float3 center, float radius)
            {
                float angle = 2.0 * asin(saturate(length(d - center) * 0.5));
                float aa = max(fwidth(angle), 1e-5);
                return 1.0 - smoothstep(radius - aa, radius + aa, angle);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            bool SeenFromInside(float3 pane, float3 eye)
            {
                if (_ForceNight > 0.5) return true;
                int count = (int)_LMS_InteriorCount;
                [unroll] for (int i = 0; i < 8; i++)
                {
                    if (i >= count) break;
                    float3 lo = _LMS_InteriorMin[i].xyz, hi = _LMS_InteriorMax[i].xyz;
                    bool paneNear = all(pane >= lo - _InteriorMargin) && all(pane <= hi + _InteriorMargin);
                    bool eyeInside = all(eye >= lo) && all(eye <= hi);
                    if (paneNear && eyeInside) return true;
                }
                return false;
            }

            half3 Night(float3 d)
            {
                float height = d.y;
                half3 color = lerp(_SkyHorizon.rgb, _SkyZenith.rgb, smoothstep(0.0, 0.7, height));
                // Distant rolling hills and pine tops below ~6 degrees.
                float azimuth = atan2(d.z, d.x);
                float ridge = 0.035 + 0.03 * sin(azimuth * 3.0 + 1.3) + 0.018 * sin(azimuth * 11.0) + 0.012 * abs(sin(azimuth * 37.0));
                float hill = 1.0 - smoothstep(ridge - 0.004, ridge + 0.004, height);
                // Stars.
                float3 p = d * 60.0;
                float3 cell = floor(p);
                float3 local = frac(p) - 0.5;
                float3 offset = (Hash33(cell) - 0.5) * 0.6;
                float star = step(1.0 - _StarDensity * 0.35, Hash31(cell + 17.0)) * smoothstep(0.12, 0.02, length(local - offset));
                star *= smoothstep(0.08, 0.3, height);
                color += _StarColor.rgb * star * (0.55 + 0.45 * Hash31(cell + 3.0));
                // Stylized moon: sharp disc, optional crescent, short halo.
                float3 moon = normalize(_MoonDirection.xyz + float3(0, 1e-5, 0));
                float radius = radians(_MoonSize);
                float disc = Disc(d, moon, radius);
                float3 side = normalize(cross(moon, float3(0, 1, 0)) + float3(1e-5, 0, 0));
                float3 shadowCenter = normalize(moon + side * (tan(radius) * 2.0 * _MoonPhase));
                float lit = disc * (1.0 - (_MoonPhase > 0.001 ? Disc(d, shadowCenter, radius) : 0.0));
                float angle = 2.0 * asin(saturate(length(d - moon) * 0.5));
                float glow = (1.0 - smoothstep(radius, radius * 3.0, angle));
                color = lerp(color, _MoonColor.rgb, glow * glow * _MoonGlow);
                color = lerp(color, _MoonColor.rgb * _MoonIntensity, lit);
                color = lerp(color, _HillColor.rgb, hill);
                return color;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 eye = GetCameraPositionWS();
                half3 color;
                if (SeenFromInside(input.positionWS, eye))
                {
                    color = Night(normalize(input.positionWS - eye));
                }
                else
                {
                    half gradient = lerp(1.0, 0.75 + 0.5 * saturate(input.uv.y), _OutsideGradient);
                    color = _OutsideColor.rgb * (_OutsideIntensity * gradient);
                    color = MixFog(color, input.fog);
                }
                if (_Muntins > 0.0001)
                {
                    float2 m = abs(input.uv - 0.5);
                    float2 aa = max(fwidth(input.uv), 1e-5);
                    float bar = max(1.0 - smoothstep(_Muntins - aa.x, _Muntins + aa.x, m.x), 1.0 - smoothstep(_Muntins - aa.y, _Muntins + aa.y, m.y));
                    color = lerp(color, _MuntinColor.rgb, bar);
                }
                return half4(color, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #include "Packages/com.unity.render-pipelines.universal/Shaders/UnlitDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}
