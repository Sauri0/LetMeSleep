Shader "LetMeSleep/Higgsfield/GradientSky"
{
    // Stylized sky for the Higgsfield maps (v0.3.0 atmosphere pass). The horizon color is meant to be
    // identical to the map's RenderSettings fog color, so distant geometry dissolves into the sky.
    // Optional terms (all default to off, preserving the original two-color gradient):
    //   ground color below the horizon, horizon blend exponent, a wide sun glow, a sharp sun/moon disc with an
    //   optional crescent and a short halo (radius in disc radii), a sparse star field for night maps and flat
    //   stylized cumulus clouds for day maps (ENV-04). No textures.
    Properties
    {
        _HorizonColor("Horizon (match fog color)", Color)=(.42,.62,.72,1)
        _ZenithColor("Upper sky", Color)=(.15,.3,.55,1)
        _GroundColor("Below horizon", Color)=(.42,.62,.72,1)
        _GroundBlend("Below-horizon blend (0 = horizon color)", Range(0,1))=0
        _HorizonExponent("Horizon to zenith exponent", Range(0.25,4))=1
        [HDR] _GlowColor("Sun wide glow (HDR)", Color)=(0,0,0,1)
        _GlowTightness("Glow tightness", Range(1,128))=8
        [HDR] _DiscColor("Sun/moon disc (HDR)", Color)=(0,0,0,1)
        _DiscSize("Disc angular radius (degrees)", Range(0.1,12))=2.5
        _MoonPhase("Crescent (0 = full disc)", Range(0,0.95))=0
        _HaloColor("Disc halo (sRGB, alpha = opacity)", Color)=(1,0.95,0.82,0)
        _HaloRadius("Halo radius (disc radii)", Range(1,6))=3
        _StarDensity("Star density", Range(0,1))=0
        [HDR] _StarColor("Star color (HDR)", Color)=(1,1,1,1)
        // w = 1: draw the sun/moon toward xyz instead of the main light (stylized framing; the light can then
        // front-light characters while the moon still sits in the default view).
        _CelestialDirection("Sun/moon direction override (xyz, w=1 enables)", Vector)=(0,1,0,0)
        _CloudCoverage("Cloud coverage (0 = none)", Range(0,1))=0
        _CloudScale("Cloud scale", Range(0.05,4))=0.6
        _CloudSpeed("Cloud drift", Range(0,0.2))=0.01
        _CloudColor("Cloud lit color", Color)=(1,1,1,1)
        _CloudShade("Cloud underside color", Color)=(0.78,0.85,0.95,1)
        _CloudOpacity("Cloud opacity", Range(0,1))=0.95
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
                half _MoonPhase;
                half4 _HaloColor;
                half _HaloRadius;
                half _StarDensity;
                half4 _StarColor;
                float4 _CelestialDirection;
                half _CloudCoverage;
                half _CloudScale;
                half _CloudSpeed;
                half4 _CloudColor;
                half4 _CloudShade;
                half _CloudOpacity;
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
            float Hash21(float2 p)
            {
                float3 q = frac(float3(p.xyx) * 0.1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }
            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), u.x), lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), u.x), u.y);
            }
            float CloudField(float2 p)
            {
                // Big puffy lobes first, small detail last (stylized, not realistic).
                return ValueNoise(p) * 0.58 + ValueNoise(p * 2.03 + 7.1) * 0.27 + ValueNoise(p * 4.1 + 3.7) * 0.15;
            }
            float AngleTo(float3 d, float3 center) { return 2.0 * asin(saturate(length(d - center) * 0.5)); }

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

                // Sun or moon: URP main light direction (towards the light) unless overridden.
                float3 toLight=_CelestialDirection.w>.5
                    ? normalize(_CelestialDirection.xyz+float3(0,1e-5,0))
                    : normalize(_MainLightPosition.xyz+float3(0,1e-5,0));
                float facing=saturate(dot(direction,toLight));
                float aboveHorizon=smoothstep(-.02,.04,height);
                color+=_GlowColor.rgb*pow(facing,_GlowTightness)*aboveHorizon;

                // Sharp disc: angular distance with a ~1-2 px anti-aliased edge (fwidth), optional crescent.
                float radius=radians(_DiscSize);
                float angle=AngleTo(direction,toLight);
                float aa=max(fwidth(angle),1e-5);
                float disc=1.0-smoothstep(radius-aa,radius+aa,angle);
                if (_MoonPhase>0.001)
                {
                    float3 side=normalize(cross(toLight,float3(0,1,0))+float3(1e-5,0,0));
                    float3 shadowCenter=normalize(toLight+side*(tan(radius)*2.0*_MoonPhase));
                    float shadowAngle=AngleTo(direction,shadowCenter);
                    disc*=smoothstep(radius-aa,radius+aa,shadowAngle);
                }
                // Short halo: never beyond _HaloRadius disc radii, opacity _HaloColor.a.
                float halo=1.0-smoothstep(radius,radius*_HaloRadius,angle);
                color=lerp(color,_HaloColor.rgb,halo*halo*_HaloColor.a*aboveHorizon);
                float discOn=saturate(dot(_DiscColor.rgb,1));
                color=lerp(color,_DiscColor.rgb,disc*aboveHorizon*discOn);

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
                    star*=smoothstep(.08,.35,height)*(1-disc)*(1-halo);
                    color+=_StarColor.rgb*star*(.55+.45*Hash31(cell+3.0));
                }

                // Flat stylized clouds on a virtual dome (day maps).
                if (_CloudCoverage>0.001 && height>0.0)
                {
                    float2 uv=direction.xz/(height+0.18)*_CloudScale+_Time.y*_CloudSpeed*float2(1.0,0.35);
                    float field=CloudField(uv);
                    float threshold=1.0-_CloudCoverage;
                    float edge=max(fwidth(field)*1.2,0.003);
                    float shape=smoothstep(threshold,threshold+edge,field);
                    // Underside shading: density just "below" (toward the viewer's horizon) darkens the base.
                    float lower=CloudField(uv+float2(0.0,-0.09)*_CloudScale);
                    float lit=saturate((field-lower)*5.0+0.55+(field-threshold)*2.0);
                    float3 cloud=lerp(_CloudShade.rgb,_CloudColor.rgb,lit);
                    shape*=smoothstep(0.03,0.2,height)*_CloudOpacity;
                    color=lerp(color,cloud,shape);
                }
                return half4(color,1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
