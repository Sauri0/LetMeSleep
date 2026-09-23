Shader "LetMeSleep/Higgsfield/GradientSky"
{
    // Stylized sky for the Higgsfield maps (v0.3.0 atmosphere pass). The horizon color is meant to be
    // identical to the map's RenderSettings fog color, so distant geometry dissolves into the sky.
    // Optional terms (all default to off, preserving the original two-color gradient):
    //   ground color below the horizon, horizon blend exponent, a sun/moon glow and disc that follow the
    //   URP main light, and a sparse procedural star field for night maps. No textures, no clouds.
    Properties
    {
        _HorizonColor("Horizon (match fog color)", Color)=(.42,.62,.72,1)
        _ZenithColor("Upper sky", Color)=(.15,.3,.55,1)
        _GroundColor("Below horizon", Color)=(.42,.62,.72,1)
        _GroundBlend("Below-horizon blend (0 = horizon color)", Range(0,1))=0
        _HorizonExponent("Horizon to zenith exponent", Range(0.25,4))=1
        [HDR] _GlowColor("Sun/moon glow (HDR)", Color)=(0,0,0,1)
        _GlowTightness("Glow tightness", Range(1,128))=8
        [HDR] _DiscColor("Sun/moon disc (HDR)", Color)=(0,0,0,1)
        _DiscSize("Disc angular size (degrees)", Range(0.1,12))=2.5
        _StarDensity("Star density", Range(0,1))=0
        [HDR] _StarColor("Star color (HDR)", Color)=(1,1,1,1)
        // w = 1: draw the sun/moon toward xyz instead of the main light (stylized framing; the light can then
        // front-light characters while the moon still sits in the default view).
        _CelestialDirection("Sun/moon direction override (xyz, w=1 enables)", Vector)=(0,1,0,0)
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
                half4 _GroundColor;
                half _GroundBlend;
                half _HorizonExponent;
                half4 _GlowColor;
                half _GlowTightness;
                half4 _DiscColor;
                half _DiscSize;
                half _StarDensity;
                half4 _StarColor;
                float4 _CelestialDirection;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }
            float3 Hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

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
                float3 direction=normalize(input.direction);
                float height=direction.y;
                // Lower hemisphere and first degrees above horizon share exact fog color.
                float blend=pow(smoothstep(.03,.85,height),_HorizonExponent);
                float3 color=lerp(_HorizonColor.rgb,_ZenithColor.rgb,blend);
                color=lerp(color,_GroundColor.rgb,_GroundBlend*smoothstep(0,-.35,height));

                // Sun or moon: URP main light direction (towards the light).
                float3 toLight=_CelestialDirection.w>.5
                    ? normalize(_CelestialDirection.xyz+float3(0,1e-5,0))
                    : normalize(_MainLightPosition.xyz+float3(0,1e-5,0));
                float facing=saturate(dot(direction,toLight));
                float aboveHorizon=smoothstep(-.02,.04,height);
                color+=_GlowColor.rgb*pow(facing,_GlowTightness)*aboveHorizon;
                float discCos=cos(radians(_DiscSize));
                float disc=smoothstep(discCos-.0006,discCos+.0004,facing);
                color=lerp(color,_DiscColor.rgb,disc*aboveHorizon*saturate(dot(_DiscColor.rgb,1)));

                // Sparse, static star field (night maps only when density > 0).
                if (_StarDensity>0.001)
                {
                    float3 p=direction*64.0;
                    float3 cell=floor(p);
                    float3 local=frac(p)-.5;
                    float3 offset=(Hash33(cell)-.5)*.6;
                    float h=Hash31(cell+17.0);
                    float d=length(local-offset);
                    float star=step(1-_StarDensity*.35,h)*smoothstep(.11,.02,d);
                    star*=smoothstep(.08,.35,height)*(1-disc);
                    color+=_StarColor.rgb*star*(.55+.45*Hash31(cell+3.0));
                }
                return half4(color,1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
