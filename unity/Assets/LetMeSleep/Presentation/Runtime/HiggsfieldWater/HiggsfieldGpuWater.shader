Shader "LetMeSleep/Higgsfield/FlatGpuWater"
{
    Properties
    {
        [MainColor] _BaseColor("Flat palette color", Color) = (1,1,1,1)
        _WaterAmplitude("Amplitude in world metres", Range(0,0.15)) = 0.025
        _WaterWavelength("Wavelength in world metres", Float) = 4
        _WaterSpeed("Angular speed", Float) = 0.65
        _WaterPhase("Phase in radians", Float) = 0
        _WaterTimeOverride("Explicit time in seconds", Float) = 0
        _WaterUseTimeOverride("Use explicit time", Float) = 0
        _WaterUseVertexColors("Multiply authored vertex colors", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Unlit" }
        Cull Off
        ZWrite On
        ZTest LEqual

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _WaterAmplitude;
            float _WaterWavelength;
            float _WaterSpeed;
            float _WaterPhase;
            float _WaterTimeOverride;
            float _WaterUseTimeOverride;
            float _WaterUseVertexColors;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            half4 color : COLOR;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        // Both passes share this exact world-Y deformation. Mesh buffers remain untouched.
        Varyings WaterVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            float3 world = TransformObjectToWorld(input.positionOS.xyz);
            float k = TWO_PI / max(0.1, _WaterWavelength);
            float t = _WaterUseTimeOverride > 0.5 ? _WaterTimeOverride : _Time.y;
            float phase = t * _WaterSpeed + _WaterPhase;
            float height = clamp(_WaterAmplitude, 0.0, 0.15) *
                (0.65 * sin((world.x + world.z * 0.37) * k + phase) +
                 0.35 * sin((world.z - world.x * 0.21) * k * 0.71 + phase * 0.83));
            world.y += height;
            output.positionCS = TransformWorldToHClip(world);
            output.color = _WaterUseVertexColors > 0.5 ? input.color : half4(1,1,1,1);
            return output;
        }
        half4 WaterColor(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            // Flat authored palette; no textures, specular, normal smoothing or PBR.
            return half4(_BaseColor.rgb * input.color.rgb, 1);
        }
        half WaterDepth(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return input.positionCS.z;
        }
        ENDHLSL

        Pass
        {
            Name "FlatWater"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WaterVertex
            #pragma fragment WaterColor
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WaterVertex
            #pragma fragment WaterDepth
            #pragma multi_compile_instancing
            ENDHLSL
        }
        // Intentionally no ShadowCaster, texture sampling, tessellation or motion vectors.
    }
    Fallback Off
}
