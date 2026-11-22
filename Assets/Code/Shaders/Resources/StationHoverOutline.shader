Shader "Cardboard/StationHoverOutline"
{
    Properties
    {
        _OutlineColor ("Outline color", Color) = (1, .76, .28, 1)
        _OutlinePixels ("Outline width in screen pixels", Float) = 3
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" "RenderType"="Opaque" }
        Pass
        {
            Name "Interaction silhouette"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlinePixels;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS=TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS=TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS=TransformWorldToHClip(positionWS);
                float4 normalCS=mul(UNITY_MATRIX_VP,float4(normalWS,0));
                float2 direction=(normalCS.xy*positionCS.w-positionCS.xy*normalCS.w)*_ScaledScreenParams.xy;
                direction*=rsqrt(max(dot(direction,direction),1e-8));
                positionCS.xy+=direction*(2*_OutlinePixels/_ScaledScreenParams.xy)*positionCS.w;
                output.positionCS=positionCS;return output;
            }
            half4 frag(Varyings input):SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}