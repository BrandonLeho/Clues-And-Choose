Shader "UI/SoftCapsuleMask"
{
    Properties
    {
        _Color ("Tint", Color) = (0,0,0,0.6)
        _Center ("Center (UV)", Vector) = (0.5, 0.5, 0, 0)
        _HalfSize ("Half Size (UV)", Vector) = (0.18, 0.10, 0, 0)
        _CornerRadius ("Corner Radius (UV)", Float) = 0.10
        _Feather ("Edge Feather (UV)", Float) = 0.05
        _AlphaMult ("Alpha Multiplier", Float) = 1.0
        [HideInInspector]_MainTex ("Sprite Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float2 _Center;
            float2 _HalfSize;
            float  _CornerRadius;
            float  _Feather; 
            float  _AlphaMult;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            float sdRoundRect(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - (b - r);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;

                float2 p = uv - _Center;

                float d = sdRoundRect(p, _HalfSize, _CornerRadius);

                float edge0 = 0.0;
                float edge1 = max(_Feather, 1e-5);
                float outside = smoothstep(edge0, edge1, d);

                fixed4 dimCol = _Color;
                dimCol.a *= outside * _AlphaMult;

                return dimCol;
            }
            ENDCG
        }
    }
}
