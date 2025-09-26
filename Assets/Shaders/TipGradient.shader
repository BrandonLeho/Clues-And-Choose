Shader "Sprites/TipGradient"
{
    Properties
    {
        [PerRendererData]_MainTex ("Sprite Texture", 2D) = "white" {}
        _ColorTop ("Color Top", Color) = (1,1,1,1)
        _ColorBottom ("Color Bottom", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "CanUseSpriteAtlas"="True"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _ColorTop;
            fixed4 _ColorBottom;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos  : SV_POSITION;
                float2 uv   : TEXCOORD0;
                float2 luv  : TEXCOORD1;
                fixed4 col  : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);
                o.col = v.color;

                o.luv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv) * i.col;
                fixed t = saturate(i.luv.y);
                fixed4 grad = lerp(_ColorBottom, _ColorTop, t);
                return tex * grad;
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
