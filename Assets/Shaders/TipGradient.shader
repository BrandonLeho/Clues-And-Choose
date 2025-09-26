Shader "Sprites/ArrowGradientWhiteTop"
{
    Properties
    {
        [PerRendererData]_MainTex ("Sprite Texture", 2D) = "white" {}
        _ColorBottom ("Bottom Color", Color) = (1,1,1,1)
        _GradStart ("Gradient Start (0-1)", Range(0,1)) = 0
        _GradEnd   ("Gradient End (0-1)", Range(0,1)) = 0.6
        _GradPower ("Gradient Power", Range(0.1,4)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
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

            fixed4 _ColorBottom;
            float  _GradStart;
            float  _GradEnd;
            float  _GradPower;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float2 luv : TEXCOORD1;
                fixed4 col : COLOR;
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

                float denom = max(1e-5, _GradEnd - _GradStart);
                float t = saturate((i.luv.y - _GradStart) / denom);
                t = pow(t, _GradPower);

                fixed4 grad = lerp(_ColorBottom, fixed4(1,1,1,1), t);

                return tex * grad;
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
