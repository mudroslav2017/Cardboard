Shader "Cardboard/SoftLightScattering"
{
    Properties
    {
        [HDR] _ScatterColor ("Warm scattered light", Color) = (1.1,.86,.56,1)
        _Density ("Subtle scattering density", Range(0,.1)) = .026
        [HideInInspector] _Cull ("Cull", Float) = 0
        [HideInInspector] _Smoothness ("Smoothness", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off ZTest Always
            Blend One One
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _ScatterColor;float _Density;float _Cull;float _Smoothness;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;};
            Varyings vert(Attributes v)
            {
                Varyings o;o.world=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);return o;
            }
            half4 frag(Varyings i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                // Integrate once, at the volume exit, including when the viewer is inside the haze.
                if(IS_FRONT_VFACE(face,true,false))discard;
                float3 ray=normalize(i.world-_WorldSpaceCameraPos);
                float3 origin=TransformWorldToObject(_WorldSpaceCameraPos);
                float3 direction=TransformWorldToObjectDir(ray,false);
                float3 inv=rcp(direction+1e-7);
                float3 a=(-.5-origin)*inv,b=(.5-origin)*inv;
                float3 nearT=min(a,b),farT=max(a,b);
                float entry=max(0,max(nearT.x,max(nearT.y,nearT.z)));
                float leave=min(farT.x,min(farT.y,farT.z));
                float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
                float depth=SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                float3 surface=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
                leave=min(leave,distance(surface,_WorldSpaceCameraPos));
                float stepLength=max(0,leave-entry)/12;
                float jitter=frac(dot(i.positionCS.xy,float2(.06711056,.00583715))*52.9829189);
                float integral=0;
                [unroll] for(int sampleIndex=0;sampleIndex<12;sampleIndex++)
                {
                    float3 p=origin+direction*(entry+(sampleIndex+jitter)*stepLength);
                    float down=saturate(.5-p.y);
                    float radius=lerp(.035,.5,down);
                    float radial=length(p.xz)/radius;
                    float density=smoothstep(1,.45,radial)*smoothstep(0,.07,down)*(1-smoothstep(.78,1,down));
                    integral+=density*stepLength;
                }
                half amount=1-exp(-integral*_Density);
                return half4(_ScatterColor.rgb*amount,0);
            }
            ENDHLSL
        }
    }
}
