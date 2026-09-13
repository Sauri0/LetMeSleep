Shader "LetMeSleep/Higgsfield/GradientSky"
{
    Properties
    {
        _HorizonColor("Horizon and lower hemisphere", Color)=(.42,.62,.72,1)
        _ZenithColor("Upper sky", Color)=(.15,.3,.55,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _HorizonColor;
                half4 _ZenithColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.direction=TransformObjectToWorldDir(input.positionOS.xyz);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float height=normalize(input.direction).y;
                // Lower hemisphere and first degrees above horizon share exact fog color.
                float blend=smoothstep(.03,.85,height);
                return half4(lerp(_HorizonColor.rgb,_ZenithColor.rgb,blend),1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
