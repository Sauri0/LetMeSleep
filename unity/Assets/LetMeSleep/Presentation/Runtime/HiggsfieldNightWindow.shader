Shader "LetMeSleep/Higgsfield/NightWindow"
{
    // v0.3.0 windows coherent with the night (art direction #4).
    // Seen from outside, the pane glows warm (HDR ~2, so bloom spills onto the frame). v0.3.0 r3 (maps-r3 #8): when the
    // binder publishes the pane box (_PaneMin/_PaneMax, world AABB, MaterialPropertyBlock; the imported panes have no
    // UVs) the glow is a lit room seen through the glass: the view ray is traced into a fake room behind the pane
    // (interior mapping), so the back wall shows a vertical gradient (_OutsideColor on top to _OutsideBottom below),
    // the side walls, floor and ceiling shift with parallax, and backlit curtains (_CurtainOpacity, ~30 %) frame the
    // window from a plane just behind the glass. Without a box it falls back to a flat vertical gradient.
    // Seen from inside one of the map's interior volumes (published by HiggsfieldMapLighting as _LMS_Interior*), the
    // same pane shows the view outside sampled along the view ray: the night (navy sky, stars, a stylized moon and dark
    // hills) or, with _ViewMode = 1, a day sea (sky gradient over a blue sea, e.g. a yacht porthole).
    // _ForceNight = 1 always shows the view outside (menu/lobby window, portholes).
    Properties
    {
        _OutsideColor("Outside glow, top (sRGB)", Color) = (1, 0.72, 0.36, 1)
        _OutsideBottom("Outside glow, bottom (sRGB)", Color) = (0.784, 0.376, 0.165, 1)
        _OutsideIntensity("Outside HDR intensity", Range(0, 8)) = 1.8
        _OutsideGradient("Outside vertical gradient", Range(0, 1)) = 0.3
        _RoomDepth("Fake room depth (m)", Range(0.3, 6)) = 2.4
        _RoomShade("Room side/floor darkening", Range(0, 1)) = 0.35
        _LampGlow("Lamp glow on the back wall", Range(0, 2)) = 0.35
        _CurtainColor("Curtain transmission (sRGB)", Color) = (0.55, 0.24, 0.13, 1)
        _CurtainOpacity("Curtain opacity", Range(0, 1)) = 0.3
        _CurtainWidth("Curtain width (fraction of half pane)", Range(0, 0.8)) = 0.34
        _SkyHorizon("View horizon (sRGB)", Color) = (0.102, 0.165, 0.416, 1)
        _SkyZenith("View zenith (sRGB)", Color) = (0.043, 0.071, 0.2, 1)
        _HillColor("Hill silhouette (sRGB)", Color) = (0.035, 0.05, 0.1, 1)
        _ViewMode("View: 0 night hills, 1 day sea", Float) = 0
        _SeaColor("Sea, deep (sRGB)", Color) = (0.1, 0.42, 0.72, 1)
        _SeaHorizonColor("Sea at the horizon (sRGB)", Color) = (0.36, 0.66, 0.86, 1)
        _HorizonLift("Horizon lift (sin of elevation)", Range(-0.3, 0.5)) = 0
        _StarDensity("Star density", Range(0, 1)) = 0.45
        _StarColor("Star color (HDR)", Color) = (1.2, 1.2, 1.3, 1)
        _MoonDirection("Moon direction (world xyz)", Vector) = (0.4, 0.45, 0.8, 0)
        _MoonSize("Moon angular radius (deg)", Range(0.2, 8)) = 2.2
        _MoonColor("Moon color (sRGB)", Color) = (1, 0.953, 0.82, 1)
        _MoonIntensity("Moon intensity", Range(0, 4)) = 1
        _MoonPhase("Crescent (0 = full)", Range(0, 0.95)) = 0
        _MoonGlow("Moon halo opacity", Range(0, 0.5)) = 0.25
        _ForceNight("Always show the view outside (menu, portholes)", Float) = 0
        _InteriorMargin("Pane distance to interior (m)", Range(0, 1)) = 0.4
        _Muntins("Muntin width (0 = none, fraction of half pane)", Range(0, 0.08)) = 0
        _MuntinColor("Muntin color (sRGB)", Color) = (0.2, 0.12, 0.07, 1)
        _PaneMin("Pane box min (world, per renderer)", Vector) = (0, 0, 0, 0)
        _PaneMax("Pane box max (world, per renderer)", Vector) = (0, 0, 0, 0)
    }
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _OutsideColor;
            half4 _OutsideBottom;
            half _OutsideIntensity;
            half _OutsideGradient;
            half _RoomDepth;
            half _RoomShade;
            half _LampGlow;
            half4 _CurtainColor;
            half _CurtainOpacity;
            half _CurtainWidth;
            half4 _SkyHorizon;
            half4 _SkyZenith;
            half4 _HillColor;
            half _ViewMode;
            half4 _SeaColor;
            half4 _SeaHorizonColor;
            half _HorizonLift;
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
            float4 _PaneMin;
            float4 _PaneMax;
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

            half3 ViewOutside(float3 d)
            {
                // A lifted horizon lets a porthole just above the eye still show the sea in its lower half.
                float height = d.y - _HorizonLift;
                half3 color = lerp(_SkyHorizon.rgb, _SkyZenith.rgb, smoothstep(0.0, 0.7, height));
                bool sea = _ViewMode > 0.5;
                // Stars (night only).
                if (!sea)
                {
                    float3 p = d * 60.0;
                    float3 cell = floor(p);
                    float3 local = frac(p) - 0.5;
                    float3 offset = (Hash33(cell) - 0.5) * 0.6;
                    float star = step(1.0 - _StarDensity * 0.35, Hash31(cell + 17.0)) * smoothstep(0.12, 0.02, length(local - offset));
                    star *= smoothstep(0.08, 0.3, height);
                    color += _StarColor.rgb * star * (0.55 + 0.45 * Hash31(cell + 3.0));
                }
                // Stylized moon/sun: sharp disc, optional crescent, short halo.
                float3 moon = normalize(_MoonDirection.xyz + float3(0, 1e-5, 0));
                float radius = radians(_MoonSize);
                float disc = Disc(d, moon, radius);
                float3 side = normalize(cross(moon, float3(0, 1, 0)) + float3(1e-5, 0, 0));
                float3 shadowCenter = normalize(moon + side * (tan(radius) * 2.0 * _MoonPhase));
                float lit = disc * (1.0 - (_MoonPhase > 0.001 ? Disc(d, shadowCenter, radius) : 0.0));
                float angle = 2.0 * asin(saturate(length(d - moon) * 0.5));
                float glow = (1.0 - smoothstep(radius, radius * 3.0, angle));
                color = lerp(color, _MoonColor.rgb, glow * glow * _MoonGlow);
                color = lerp(color, _MoonColor.rgb * _MoonIntensity, lit * saturate(_MoonIntensity));
                if (sea)
                {
                    // Day sea below the horizon: light band at the horizon, deeper blue toward the viewer.
                    float below = saturate(-height);
                    half3 water = lerp(_SeaHorizonColor.rgb, _SeaColor.rgb, smoothstep(0.0, 0.35, below));
                    float sparkle = step(0.93, Hash31(floor(d * 90.0 + float3(0, _Time.y * 0.6, 0)))) * smoothstep(0.0, 0.02, below) * 0.35;
                    water += sparkle;
                    float horizon = smoothstep(-0.004, 0.004, height);
                    color = lerp(water, color, horizon);
                }
                else
                {
                    // Distant rolling hills and pine tops below ~6 degrees.
                    float azimuth = atan2(d.z, d.x);
                    float ridge = 0.035 + 0.03 * sin(azimuth * 3.0 + 1.3) + 0.018 * sin(azimuth * 11.0) + 0.012 * abs(sin(azimuth * 37.0));
                    float hill = 1.0 - smoothstep(ridge - 0.004, ridge + 0.004, height);
                    color = lerp(color, _HillColor.rgb, hill);
                }
                return color;
            }

            // Backlit curtains just behind the glass; pane coordinates u, v in -1..1.
            float CurtainMask(float u, float v, float2 aa)
            {
                // Tie-back: narrower around a third of the height, gathered folds along the inner edge.
                float gather = exp(-(v + 0.25) * (v + 0.25) / 0.06);
                float width = _CurtainWidth * lerp(1.0, 0.55, gather) + 0.035 * sin(v * 11.0);
                float left = 1.0 - smoothstep(-1.0 + width - aa.x, -1.0 + width + aa.x, u);
                float right = smoothstep(1.0 - width - aa.x, 1.0 - width + aa.x, u);
                // Scalloped valance across the top.
                float valance = smoothstep(0.84 - 0.05 * abs(sin(u * 7.0)) - aa.y, 0.84 - 0.05 * abs(sin(u * 7.0)) + aa.y, v);
                return saturate(left + right + valance);
            }

            half3 LitRoom(float3 positionWS, float3 eye)
            {
                float3 lo = _PaneMin.xyz, hi = _PaneMax.xyz;
                float3 center = (lo + hi) * 0.5;
                float3 extent = max((hi - lo) * 0.5, 1e-3);
                // Geometric pane normal toward the viewer, kept horizontal (windows are vertical).
                float3 n = normalize(cross(ddy(positionWS), ddx(positionWS)));
                if (dot(n, eye - positionWS) < 0.0) n = -n;
                n.y = 0.0;
                n = dot(n, n) > 1e-4 ? normalize(n) : float3(0, 0, 1);
                float3 r = normalize(cross(float3(0, 1, 0), n));
                float halfW = max(abs(r.x) * extent.x + abs(r.z) * extent.z, 0.05);
                float halfH = max(extent.y, 0.05);
                float x0 = dot(positionWS - center, r);
                float y0 = positionWS.y - center.y;
                float3 view = normalize(positionWS - eye);
                float3 dir = float3(dot(view, r), view.y, max(dot(view, -n), 0.05));
                // Room box in metres around the pane: wider than the window, floor below the sill.
                float roomW = halfW + 1.2;
                float floorY = -halfH - 0.9;
                float ceilY = halfH + 0.55;
                float tBack = _RoomDepth / dir.z;
                float tSide = (sign(dir.x) * roomW - x0) / (abs(dir.x) > 1e-4 ? dir.x : 1e-4);
                float tFlat = ((dir.y > 0.0 ? ceilY : floorY) - y0) / (abs(dir.y) > 1e-4 ? dir.y : 1e-4);
                tSide = tSide > 0.0 ? tSide : 1e5;
                tFlat = tFlat > 0.0 ? tFlat : 1e5;
                float t = min(tBack, min(tSide, tFlat));
                float3 hit = float3(x0, y0, 0.0) + dir * t;
                // Warm gradient by height, mostly from the pane itself (so every window reads top-bright/bottom-warm from
                // any viewpoint) with a little of the room hit height for parallax.
                float heightT = saturate((lerp(y0, hit.y, 0.3) + halfH) / (2.0 * halfH));
                half3 wall = lerp(_OutsideBottom.rgb, _OutsideColor.rgb, lerp(1.0, heightT, _OutsideGradient));
                // Practical lamp glow on the back wall, up and to one side.
                float2 lamp = float2(hit.x - halfW * 0.45, hit.y - halfH * 0.35);
                wall *= 1.0 + _LampGlow * exp(-dot(lamp, lamp) / (0.35 * halfW * halfW + 0.15));
                half shade = 1.0;
                if (t == tSide) shade = 1.0 - _RoomShade * 0.6;
                else if (t == tFlat) shade = dir.y > 0.0 ? 1.0 - _RoomShade * 0.35 : 1.0 - _RoomShade;
                half3 color = wall * shade;
                // Curtains on a plane 6 cm behind the glass.
                float tc = 0.06 / dir.z;
                float cu = (x0 + dir.x * tc) / halfW;
                float cv = (y0 + dir.y * tc) / halfH;
                float2 aa = max(float2(fwidth(cu), fwidth(cv)) * 1.2, 1e-4);
                float curtain = CurtainMask(cu, cv, aa);
                color = lerp(color, color * _CurtainColor.rgb * 1.5, curtain * _CurtainOpacity);
                if (_Muntins > 0.0001)
                {
                    float u = x0 / halfW, v = y0 / halfH;
                    float2 duv = max(float2(fwidth(u), fwidth(v)), 1e-5);
                    float bar = max(1.0 - smoothstep(_Muntins - duv.x, _Muntins + duv.x, abs(u)),
                        1.0 - smoothstep(_Muntins - duv.y, _Muntins + duv.y, abs(v)));
                    color = lerp(color, _MuntinColor.rgb / max(_OutsideIntensity, 1e-3), bar);
                }
                return color;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 eye = GetCameraPositionWS();
                // Both views are evaluated before choosing: they use screen derivatives, which must not sit inside a
                // per-pixel branch.
                half3 inside = ViewOutside(normalize(input.positionWS - eye));
                if (_Muntins > 0.0001)
                {
                    float2 m = abs(input.uv - 0.5);
                    float2 aa = max(fwidth(input.uv), 1e-5);
                    float bar = max(1.0 - smoothstep(_Muntins - aa.x, _Muntins + aa.x, m.x), 1.0 - smoothstep(_Muntins - aa.y, _Muntins + aa.y, m.y));
                    inside = lerp(inside, _MuntinColor.rgb, bar);
                }
                half3 outside;
                bool hasPane = dot(_PaneMax.xyz - _PaneMin.xyz, float3(1, 1, 1)) > 1e-3;
                if (hasPane)
                    outside = LitRoom(input.positionWS, eye) * _OutsideIntensity;
                else
                {
                    half gradient = lerp(1.0, 0.75 + 0.5 * saturate(input.uv.y), _OutsideGradient);
                    outside = _OutsideColor.rgb * (_OutsideIntensity * gradient);
                }
                outside = MixFog(outside, input.fog);
                return half4(SeenFromInside(input.positionWS, eye) ? inside : outside, 1);
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
