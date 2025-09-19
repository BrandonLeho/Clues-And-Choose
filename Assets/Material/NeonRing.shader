Shader "Sprites/NeonRing"
{
    Properties
    {
        _FillColor  ("Fill Color", Color)      = (1, 1, 1, 1)
        _RingColor  ("Ring Color", Color)      = (1, 1, 1, 1)
        _Radius     ("Coin Radius (UV)", Range(0.0, 0.5)) = 0.40
        _RingThick  ("Ring Thickness", Range(0.0, 0.5))   = 0.07
        _EdgeSoft   ("Edge Softness", Range(0.0001, 0.2)) = 0.02
        _GlowWidth  ("Glow Width", Range(0.0, 0.5))       = 0.10
        _GlowBoost  ("Glow Boost", Range(0.0, 5.0))       = 1.5
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" "RenderPipeline"="UniversalPipeline" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                float4 col : COLOR;
            };

            float4 _FillColor;
            float4 _RingColor;
            float  _Radius;
            float  _RingThick;
            float  _EdgeSoft;
            float  _GlowWidth;
            float  _GlowBoost;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.uv  = v.uv;                 // expect a quad with UV [0..1]
                o.col = v.color;
                return o;
            }

            // smoothstep helper that tolerates swapped edges
            float sstep(float a, float b, float x)
            {
                float lo = min(a,b);
                float hi = max(a,b);
                return smoothstep(lo, hi, x);
            }

            half4 frag (v2f i) : SV_Target
            {
                // Map UV to centered coordinates (-0.5..+0.5), maintain aspect for non-square sprites via ddx/ddy length.
                float2 p = i.uv - 0.5;
                float d  = length(p); // distance from center in UV space

                // Base coin fill: inside radius (soft edge)
                float fillAlpha = 1.0 - sstep(_Radius - _EdgeSoft, _Radius + _EdgeSoft, d);

                // Ring: annulus around radius with thickness _RingThick and soft edges
                float inner = sstep(_Radius - _RingThick - _EdgeSoft, _Radius - _RingThick + _EdgeSoft, d);
                float outer = 1.0 - sstep(_Radius + _EdgeSoft, _Radius - _EdgeSoft, d); // inverted soft step
                float ringMask = saturate(inner * outer);

                // Glow: soft falloff outside the ring
                float glowStart = _Radius;
                float glowEnd   = _Radius + _GlowWidth + _EdgeSoft;
                float glow      = 1.0 - sstep(glowStart, glowEnd, d);
                glow *= _GlowBoost;

                // Compose colors (premultiplied-ish alpha)
                float3 col = _FillColor.rgb * fillAlpha;
                col = lerp(col, _RingColor.rgb, ringMask);   // brighten ring zone
                col += _RingColor.rgb * glow;                // add outer glow (relies on Bloom for real neon)

                // Composite alpha: keep visuals soft but clickable
                float alpha = saturate(max(fillAlpha, max(ringMask, glow * 0.4)));

                return half4(col, alpha) * i.col;
            }
            ENDHLSL
        }
    }
}
