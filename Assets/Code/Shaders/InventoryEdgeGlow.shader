Shader "Cardboard/InventoryEdgeGlow"
{
    Properties
    {
        _BaseMap ("Marker silhouette", 2D) = "white" {}
        _GlowColor ("Beige edge glow", Color) = (1, .82, .52, 1)
        _Strength ("Selection", Range(0,1)) = 0
        [HideInInspector] _Cull ("Cull", Float) = 0
        [HideInInspector] _Smoothness ("Smoothness", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Soft silhouette glow"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _GlowColor;
                float _Strength;
                float _Cull;
                float _Smoothness;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;
            }
            half Alpha(float2 uv){return SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).a;}
            half4 frag(Varyings i):SV_Target
            {
                half halo=0;
                // Several soft rings follow the supplied alpha silhouette, not the icon's square.
                [unroll] for(int ring=1;ring<=3;ring++)
                {
                    float radius=ring*.009;
                    half sum=0;
                    [unroll] for(int tap=0;tap<12;tap++)
                    {
                        float angle=tap*6.2831853/12;
                        sum+=Alpha(i.uv+float2(cos(angle),sin(angle))*radius);
                    }
                    halo+=sum/12*(4-ring)/6;
                }
                return half4(_GlowColor.rgb,saturate(halo*2.8)*(1-Alpha(i.uv))*_Strength*_GlowColor.a);
            }
            ENDHLSL
        }
    }
}
