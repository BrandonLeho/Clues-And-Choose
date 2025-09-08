Shader "UI/NeonRect"
{
    Properties
    {
        // Required by Unity UI
        [PerRendererData]_MainTex ("Sprite Texture", 2D) = "white" {}

        [HDR]_Color("Glow Color", Color) = (1,1,1,1)
        _Intensity("Glow Intensity", Range(0,10)) = 2.5
        _Thickness("Border Thickness", Range(0.0,0.5)) = 0.10
        _Softness("Edge Softness", Range(0.0,0.5)) = 0.10
        _CornerRadius("Corner Radius", Range(0.0,0.5)) = 0.08
        _Alpha("Overall Alpha", Range(0,1)) = 1

        // UI stencil stuff
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        ZWrite Off
        Blend One One              // additive = neon
        ColorMask [_ColorMask]

        Pass
        {
            Name "NeonRectBorder"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float  _Intensity;
            float  _Thickness;
            float  _Softness;
            float  _CornerRadius;
            float  _Alpha;

            // Set by the binder script below so thickness looks uniform on any aspect
            float _RectAspect;  // width / height

            struct appdata_t {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color    : COLOR;
            };

            struct v2f {
                float4 pos      : SV_POSITION;
                float2 uv       : TEXCOORD0;
                fixed4 color    : COLOR;
                float4 worldPos : TEXCOORD1;
            };

            v2f vert (appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.texcoord;
                o.color = v.color;
                o.worldPos = v.vertex;
                return o;
            }

            // Inigo Quilez: signed distance to rounded rectangle
            float sdRoundRect(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - (b - r);
                return length(max(q,0.0)) + min(max(q.x,q.y),0.0) - r;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Map UV (0..1) to centered coords (-1..1) and scale for aspect
                float2 p = (i.uv * 2.0 - 1.0);
                float2 scale = (_RectAspect >= 1.0) ? float2(_RectAspect, 1.0) : float2(1.0, 1.0/_RectAspect);
                p *= scale;

                // Half-extents of the rectangle after scaling
                float2 b = scale;

                // Signed distance to the rounded-rect border (0 at the border)
                float sd = sdRoundRect(p, b, _CornerRadius);

                // Ring around the border inside the rect
                float inner = _Thickness;
                float soft  = max(1e-5, _Softness);
                float ring  = saturate(1.0 - smoothstep(inner, inner + soft, abs(sd))) * _Alpha;

                // Respect UI RectMask2D, etc.
                #ifdef UNITY_UI_CLIP_RECT
                ring *= UnityGet2DClipping(i.worldPos.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(ring - 0.001);
                #endif

                float3 col = _Color.rgb * _Intensity;
                return fixed4(col * ring, ring);
            }
            ENDCG
        }
    }
}
