Shader "LetMeSleep/Higgsfield/Glint"
{
    // v0.3.0 r3 warm reflection of a lamp on night water (maps-r3 #9, "reflejos verticales #FFB347 bajo cada farol").
    // A flat strip lying on the water surface (HiggsfieldAtmosphereVisuals.GlintQuad, uv.y = distance from the point
    // under the lamp toward the viewer) is turned toward the camera here, so in screen space it always reads as the
    // vertical broken reflection line of the sketches. Faded by scene fog.
    // v0.3.0 r4 (director #4): one continuous column of stacked, broken horizontal strokes (no loose ellipses), alpha
    // blended so the amber stays amber over the navy water instead of turning salmon (additive amber + blue), with the
    // opacity of _GlintColor.a (0.7) at the lamp fading to 0 at the end of the streak (3-4 m); rows have irregular
    // thickness and spacing and a faint continuous core joins them.
    // _GlintColor (sRGB, alpha = opacity) and _GlintSize (x width m, y length m, z seed) come from a MaterialPropertyBlock.
    Properties
    {
        _GlintColor("Glint color (alpha = opacity)", Color) = (1, 0.702, 0.278, 0.7)
        _GlintSize("Width, length (m), seed", Vector) = (0.5, 3.5, 0, 0)
        _GlintIntensity("Color intensity", Range(0, 8)) = 1
        _BandFrequency("Strokes per metre", Range(0.5, 24)) = 9
        _BandSpeed("Shimmer speed", Range(0, 4)) = 0.6
        _StrokeFill("Stroke thickness (fraction of a row)", Range(0.2, 1)) = 0.62
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Glint"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
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
                half _StrokeFill;
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
                float seed = _GlintSize.z;
                // Rows of horizontal strokes of irregular thickness and spacing (no ladder): each row gets its own height,
                // width, sideways offset and at most one break, drifting a little over time. A faint continuous core joins
                // them so the reflection reads as one vertical streak.
                float rowPosition = metres * _BandFrequency + Hash11(floor(metres * _BandFrequency * 0.5) + seed) * 0.35;
                float row = floor(rowPosition);
                float inRow = frac(rowPosition);
                float thickness = _StrokeFill * lerp(0.55, 1.35, Hash11(row * 3.1 + seed));
                float vertical = 1.0 - smoothstep(thickness * 0.5 - 0.14, thickness * 0.5 + 0.04, abs(inRow - 0.5 + (Hash11(row * 6.7 + seed) - 0.5) * 0.2));
                float shimmer = sin(_Time.y * _BandSpeed * 2.0 + row * 1.7 + seed) * 0.04;
                float taper = lerp(1.0, 0.62, along);
                float halfWidth = lerp(0.22, 0.5, Hash11(row * 1.37 + seed * 7.1)) * taper;
                float centerX = 0.5 + (Hash11(row * 2.9 + seed * 3.3) - 0.5) * 0.12 + shimmer;
                float x = (input.uv.x - centerX) / max(halfWidth, 1e-3); // -1..1 across the stroke
                float horizontal = 1.0 - smoothstep(0.7, 1.0, abs(x));
                float gap = Hash11(row * 5.3 + seed) * 1.4 - 0.7;
                float gapWidth = lerp(0.06, 0.18, Hash11(row * 9.1 + seed));
                float broken = lerp(1.0, smoothstep(gapWidth * 0.5, gapWidth, abs(x - gap)), step(0.55, Hash11(row * 4.4 + seed)));
                float stroke = vertical * horizontal * broken;
                float core = (1.0 - smoothstep(0.08, 0.2 * taper, abs(input.uv.x - 0.5))) * 0.3;
                float fade = pow(saturate(1.0 - along), 1.1) * smoothstep(0.0, 0.03, along);
                float strength = max(stroke, core) * fade;
                half alpha = saturate(_GlintColor.a * strength);
                half3 color = _GlintColor.rgb * _GlintIntensity;
                color = MixFogColor(color, unity_FogColor.rgb, input.fog);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
