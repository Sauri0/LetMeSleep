Shader "LetMeSleep/Higgsfield/Flame"
{
    // v0.3.0 stylized low-poly flame layer (ENV-04 campsite, ENV-03 fireplace): unlit HDR color well above the
    // bloom threshold, soft sway and flicker growing toward the tip, facet shading so the low-poly shape reads.
    // Layers are separate materials drawn outer -> middle -> core (render queue order, no depth write), so the
    // yellow core always sits on top of the orange and red-orange tongues like the sketches.
    Properties
    {
        _FlameColor("Color (sRGB)", Color) = (1, 0.54, 0.12, 1)
        _FlameIntensity("HDR intensity", Range(0, 16)) = 6
        _SwayAmplitude("Sway at tip (object units)", Range(0, 0.3)) = 0.06
        _SwaySpeed("Sway speed", Range(0, 12)) = 3.2
        _Flicker("Height flicker", Range(0, 0.4)) = 0.12
        _FacetShade("Facet shading", Range(0, 1)) = 0.35
        _FlameSeed("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Flame"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend One Zero
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _FlameColor;
                half _FlameIntensity;
                half _SwayAmplitude;
                half _SwaySpeed;
                half _Flicker;
                half _FacetShade;
                float _FlameSeed;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float fog:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 p = input.positionOS.xyz;
                float h = saturate(p.y);
                float t = _Time.y * _SwaySpeed + _FlameSeed * 7.13;
                float lobe = p.x * 3.1 + p.z * 2.3;
                float2 sway = float2(sin(t + lobe) + 0.5 * sin(t * 2.3 + lobe * 1.7), cos(t * 1.3 + lobe) + 0.4 * sin(t * 2.9 - lobe));
                p.xz += sway * (_SwayAmplitude * h * h);
                p.y *= 1.0 + _Flicker * (0.6 * sin(t * 2.7 + lobe * 2.0) + 0.4 * sin(t * 5.3 + lobe)) * h;
                output.positionWS = TransformObjectToWorld(p);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 facet = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                float3 view = normalize(GetCameraPositionWS() - input.positionWS);
                half facing = abs(dot(facet, view));
                half shade = lerp(1.0, 0.72 + 0.28 * facing, _FacetShade);
                half3 color = _FlameColor.rgb * (_FlameIntensity * shade);
                color = MixFog(color, input.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
