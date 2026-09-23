Shader "LetMeSleep/Higgsfield/Beam"
{
    // v0.3.0 r3 lighthouse light shaft (maps-r3 #2): a soft additive cone (HiggsfieldAtmosphereVisuals.BeamCone,
    // uv.x = distance along the shaft) swept around the lighthouse by HiggsfieldBeamSweep. Brightest near the lens,
    // fading along its length and toward its silhouette edges so it reads as a faint beam of light (alpha ~0.15),
    // never as a solid cone. Color (sRGB, alpha = opacity) comes from a MaterialPropertyBlock. Faded by scene fog.
    Properties
    {
        _BeamColor("Beam color (alpha = opacity)", Color) = (1, 0.824, 0.478, 0.15)
        _BeamIntensity("Intensity", Float) = 1
        _EdgeSoftness("Edge softness", Range(0.2, 4)) = 1.4
        _LengthFalloff("Length falloff", Range(0.2, 4)) = 1.3
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Beam"
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
                half4 _BeamColor;
                half _BeamIntensity;
                half _EdgeSoftness;
                half _LengthFalloff;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float along:TEXCOORD2; float fog:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.along = input.uv.x;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 view = normalize(GetCameraPositionWS() - input.positionWS);
                // Faces seen head-on are the middle of the shaft; grazing faces are its edges.
                half facing = pow(saturate(abs(dot(normalize(input.normalWS), view))), _EdgeSoftness);
                half along = saturate(input.along);
                half fade = pow(1.0 - along, _LengthFalloff) * smoothstep(0.0, 0.04, along);
                half3 color = _BeamColor.rgb * (_BeamColor.a * _BeamIntensity * facing * fade);
                color = MixFogColor(color, half3(0, 0, 0), input.fog);
                return half4(color, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
