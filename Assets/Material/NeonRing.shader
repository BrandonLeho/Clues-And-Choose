Shader "UI/NeonRing (Built-in)"
{
    Properties
    {
        [PerRendererData]_MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR]_Color("Glow Color", Color) = (1,1,1,1)
        _Intensity("Glow Intensity", Range(0,10)) = 2
        _Thickness("Ring Thickness", Range(0,0.5)) = 0.10
        _Softness("Edge Softness", Range(0,0.5)) = 0.08
        _Alpha("Overall Alpha", Range(0,1)) = 1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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
        Blend One One
        ColorMask [_ColorMask]

        Pass
        {
            Name "NeonRing"
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
            float  _Alpha;

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
                o.uv = v.texcoord;
                o.color = v.color;
                o.worldPos = v.vertex;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float r = length(p);

                float outer = 1.0 - _Softness;
                float inner = outer - _Thickness;

                float outerEdge = 1.0 - smoothstep(outer, outer + _Softness, r);
                float innerEdge = smoothstep(inner - _Softness, inner, r);
                float ring = saturate(innerEdge * outerEdge) * _Alpha;

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
