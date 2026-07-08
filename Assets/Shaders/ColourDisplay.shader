Shader "Custom/ColourDisplay"
{
    Properties
    {
        _ColTex ("Color Texture", 2D) = "white" {}
        _Opacity ("Opacity", Range(0,1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _ColTex;
            float _Opacity;


            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };


            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };


            v2f vert(appdata v)
            {
                v2f o;

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                return o;
            }


            fixed4 frag(v2f i) : SV_Target
            {
                float4 color =
                    tex2D(_ColTex, i.uv);

                color.a *= _Opacity;

                return color;
            }

            ENDCG
        }
    }
}