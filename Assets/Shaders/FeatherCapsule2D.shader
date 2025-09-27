Shader "Unlit/FeatherCapsule2D"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _FeatherColor ("Feather Color", Color) = (1,1,1,1)
        _FeatherAlpha ("Feather Alpha", Range(0,1)) = 0.25
        _FeatherWidth ("Feather Width (world)", Float) = 0.2
        _SpriteWidth ("Sprite Width (world)", Float) = 1
        _SpriteHeight("Sprite Height (world)", Float) = 1
        _BothSides ("Feather Both Sides (0/1)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        LOD 100
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "FEATHER"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            float4 _FeatherColor;
            float  _FeatherAlpha;
            float  _FeatherWidth;
            float  _SpriteWidth;
            float  _SpriteHeight;
            float  _BothSides;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float4 color : COLOR;
                float2 spriteXY : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                o.color = v.color;

                float2 local = (v.uv - 0.5) * float2(_SpriteWidth, _SpriteHeight);
                o.spriteXY = local;
                return o;
            }

            float sdCapsuleY(float2 p, float halfLen, float radius)
            {
                p.y = abs(p.y);
                float2 q = float2(p.x, max(p.y - halfLen, 0.0));
                return length(q) - radius;
            }

            float4 frag (v2f i) : SV_Target
            {
                float radius = 0.5 * _SpriteWidth;
                float totalH = _SpriteHeight;
                float halfLen = max(0.0, 0.5 * totalH - radius);

                float d = sdCapsuleY(i.spriteXY, halfLen, radius);

                float w = max(_FeatherWidth, 1e-5);
                float a = 0.0;

                if (_BothSides >= 0.5)
                {
                    float t = saturate(1.0 - abs(d) / w);
                    a = t * _FeatherAlpha;
                }
                else
                {
                    float t = saturate(1.0 - d / w);
                    t *= step(0.0, d) * step(d, w);
                    a = t * _FeatherAlpha;
                }

                if (a <= 0.001) discard;

                float4 col = _FeatherColor;
                col.a *= a;
                return col;
            }
            ENDHLSL
        }
    }
}
