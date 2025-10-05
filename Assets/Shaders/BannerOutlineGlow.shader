Shader "UI/BannerOutlineGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GlowColor ("Glow Color", Color) = (1,1,1,1)

        _InnerUV ("Inner UV", Vector) = (0.1, 0.1, 0.9, 0.9)

        _Thickness  ("Glow Thickness (0-0.5)", Range(0,0.5)) = 0.15
        _Feather    ("Feather (0-1)", Range(0,1)) = 0.35
        _TopFalloff ("Top Falloff (>=1)", Range(1,8)) = 2.0
        _Intensity  ("Intensity", Range(0,5)) = 1.0
        _UseAlphaFromTex ("Use Alpha From Tex", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        _ClipRect ("Clip Rect", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" "PreviewType"="Plane" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t { float4 vertex:POSITION; float4 color:COLOR; float2 texcoord:TEXCOORD0; };
            struct v2f {
                float4 vertex:SV_POSITION;
                float2 uv:TEXCOORD0;
                float4 color:COLOR;
                float4 worldPosition:TEXCOORD1;
            };

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Color, _GlowColor; float4 _InnerUV;
            float _Thickness, _Feather, _TopFalloff, _Intensity, _UseAlphaFromTex;
            float4 _ClipRect;

            v2f vert (appdata_t v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                o.worldPosition = v.vertex;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv;
                float x0 = _InnerUV.x, y0 = _InnerUV.y, x1 = _InnerUV.z, y1 = _InnerUV.w;

                float dx = max(max(x0 - p.x, 0.0), max(p.x - x1, 0.0));
                float dy = max(max(y0 - p.y, 0.0), max(p.y - y1, 0.0));
                float outsideDist = length(float2(dx, dy));

                float outsideFlag = step(1e-6, dx + dy);

                float thickness = max(_Thickness, 1e-4);
                float t = 1.0 - smoothstep(0.0, thickness, outsideDist);
                t = pow(saturate(t), lerp(1.0, 3.0, saturate(_Feather)));
                t *= outsideFlag;

                float topWeight = pow(saturate(i.uv.y), max(_TopFalloff, 1.0));
                t *= topWeight;

                fixed4 col = _GlowColor * (t * max(_Intensity, 0.0));
                if (_UseAlphaFromTex > 0.5) { col.a *= tex2D(_MainTex, i.uv).a; }
                col *= i.color;

                #ifdef UNITY_UI_CLIP_RECT
                    col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                    clip(col.a - 0.001);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
