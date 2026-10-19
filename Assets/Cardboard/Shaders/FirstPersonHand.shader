Shader "Cardboard/FirstPersonHand"
{
    Properties { _BaseColor ("Paper color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay-20" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; half shade:TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                float3 normal=TransformObjectToWorldNormal(v.normalOS);o.shade=.7+.3*saturate(dot(normal,normalize(float3(-.4,.8,-.5))));return o;
            }
            half4 frag(Varyings v):SV_Target { return half4(_BaseColor.rgb*v.shade,_BaseColor.a); }
            ENDHLSL
        }
    }
}