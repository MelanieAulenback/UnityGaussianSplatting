Shader "Custom/DepthDisplay"
{
    Properties
    {
        _DepthTex ("Depth Texture", 2D) = "white" {}
        _MaxDepth ("Max Depth", Float) = 10
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _DepthTex;
            float _MaxDepth;


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
                float depth = tex2D(_DepthTex, i.uv).r;


                // convert meters to grayscale
                float grey = saturate(depth / _MaxDepth);


                return float4(grey, grey, grey, 1);
            }


            ENDCG
        }
    }
}