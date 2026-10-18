Shader "Cardboard/CrushWaveUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI texture", 2D) = "white" {}
        _ImpactTime ("Seconds after crush", Float) = 0
        _Aspect ("Screen aspect", Float) = 1.6
        [HideInInspector] _Cull ("Cull", Float) = 0
        [HideInInspector] _Smoothness ("Smoothness", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            float _ImpactTime,_Aspect;
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float radius=length((i.uv-.5)*float2(_Aspect,1))*2;
                float progress=saturate(_ImpactTime/.65);
                float ring=lerp(0,length(float2(_Aspect,1))+.24,1-pow(1-progress,1.35));
                float distanceFromRing=abs(radius-ring);
                float core=exp(-pow(distanceFromRing/.036,2));
                float halo=exp(-pow(distanceFromRing/.15,2));
                float fade=1-smoothstep(.5,.84,_ImpactTime);
                float flash=exp(-_ImpactTime*22)*exp(-radius*radius*5);
                return fixed4(1,.025+halo*.025,.015,(core*.78+halo*.20+flash*.25)*fade*i.color.a);
            }
            ENDHLSL
        }
    }
}
