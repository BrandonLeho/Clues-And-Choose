Shader "UI/BannerTopLight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GlowColor ("Glow Color", Color) = (1,1,1,1)
        _TopHeight ("Top Height (0-1)", Range(0,1)) = 0.6
        _Feather   ("Feather (0-1)", Range(0,1)) = 0.25
        _Intensity ("Intensity", Range(0,5)) = 1.0
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
            fixed4 _Color, _GlowColor; float _TopHeight, _Feather, _Intensity, _UseAlphaFromTex;
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
                float depth = 1.0 - i.uv.y;
                float height = saturate(_TopHeight);
                float g = 1.0 - smoothstep(0.0, max(height, 1e-4), depth);

                g = pow(saturate(g), lerp(1.0, 2.5, saturate(_Feather)));

                fixed4 col = _GlowColor * (g * _Intensity);
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
