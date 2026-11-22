Shader "Cardboard/FirstPersonHand"
{
    Properties
    {
        _BaseColor ("Paper color", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Faces", Float) = 2
    }
    SubShader
    {
        // Transparent sorting orders the separate convex hand parts from back to front.
        // Opaque output and back-face culling keep the model solid without world-depth tests.
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; half3 normalWS:TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                half3 normal=normalize(input.normalWS);
                Light mainLight=GetMainLight();
                half diffuse=saturate(dot(normal,mainLight.direction));
                half3 lighting=max(half3(.12,.12,.12),SampleSH(normal))+mainLight.color*diffuse;
                return half4(_BaseColor.rgb*lighting,1);
            }
            ENDHLSL
        }
    }
}
