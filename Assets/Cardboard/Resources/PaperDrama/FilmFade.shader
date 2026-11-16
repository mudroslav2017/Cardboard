Shader "Cardboard/FilmFade"
{
    Properties { _Opacity("Opacity", Range(0,1))=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float _Opacity;
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; };
            Varyings Vert(Attributes input) { Varyings output;output.positionCS=float4(input.positionOS.xy,0,1);return output; }
            half4 Frag(Varyings input):SV_Target { return half4(0,0,0,_Opacity); }
            ENDHLSL
        }
    }
}