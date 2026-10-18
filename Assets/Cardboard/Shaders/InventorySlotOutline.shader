Shader "Cardboard/InventorySlotOutline"
{
    Properties
    {
        _OutlinePixels ("Outline width in screen pixels", Float) = 4
        _PixelStep ("One pixel in clip coordinates", Vector) = (.002, .002, 0, 0)
        [HideInInspector] _Cull ("Cull", Float) = 0
        [HideInInspector] _Smoothness ("Smoothness", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-20" "RenderType"="Opaque" }
        Pass
        {
            Name "Four pixel silhouette"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _PixelStep;
                float _OutlinePixels;
                float _Cull;
                float _Smoothness;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 direction:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION; };
            Varyings vert(Attributes v)
            {
                Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                // Offset the supplied silhouette by display pixels, independent of perspective and UI scale.
                o.positionCS.xy+=v.direction*_OutlinePixels*_PixelStep.xy*o.positionCS.w;
                return o;
            }
            half4 frag(Varyings i):SV_Target { return half4(0,0,0,1); }
            ENDHLSL
        }
    }
}
