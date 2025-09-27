Shader "Sprites/FeatheredOcclusionUnlit"
{
    Properties
    {
        [MainTex] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color   ("Tint", Color) = (1,1,1,1)
        _P0      ("Capsule Start (world xy)", Vector) = (0,0,0,0)
        _P1      ("Capsule End (world xy)",   Vector) = (0,0,0,0)
        _Radius  ("Capsule Radius (world)", Float) = 0.5
        _Feather ("Feather (world)",         Float) = 0.2
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
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
            fixed4 _Color;

            float4 _P0; 
            float4 _P1; 
            float _Radius;
            float _Feather;

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                float2 worldXY  : TEXCOORD1;
            };

            v2f vert (appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;

                float3 w = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldXY = w.xy;
                return o;
            }

            float segDist(float2 P, float2 A, float2 B)
            {
                float2 AB = B - A;
                float l2 = dot(AB, AB);
                if (l2 <= 1e-8) return length(P - A);
                float t = saturate( dot(P - A, AB) / l2 );
                float2 Q = A + t * AB;
                return length(P - Q);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;

                float d = segDist(i.worldXY, _P0.xy, _P1.xy);

                float r = _Radius;
                float f = max(1e-5, _Feather);
                float m = smoothstep(r - f, r + f, d);

                c.a *= m;
                return c;
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
