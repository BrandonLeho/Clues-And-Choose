Shader "Sprites/SoftCapsuleCutout"
{
    Properties
    {
        [PerRendererData] [NoScaleOffset] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [HideInInspector]_StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector]_Stencil ("Stencil ID", Float) = 0
        [HideInInspector]_StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector]_StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector]_StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector]_Cull ("Cull", Float) = 0
        [HideInInspector]_ZWrite ("ZWrite", Float) = 0
        [HideInInspector]_ZTest ("ZTest", Float) = 4
        [HideInInspector]_Blend ("Blend", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
            "PreviewType"="Plane"
        }
        LOD 100

        Pass
        {
            Tags { "LightMode"="Universal2D" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP core transforms
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END

            float4 _CapsuleP0;
            float4 _CapsuleP1;
            float  _CapsuleRadius;
            float  _CapsuleFeather;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
                float2 uv          : TEXCOORD0;
                float2 worldPos    : TEXCOORD1;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS);
                OUT.positionHCS = TransformWorldToHClip(posWS);
                OUT.uv = IN.uv;
                OUT.color = IN.color * _Color; 
                OUT.worldPos = posWS.xy;
                return OUT;
            }

            float CapsuleSDF(float2 p, float2 a, float2 b, float r)
            {
                float2 ab = b - a;
                float2 ap = p - a;
                float t = saturate(dot(ap, ab) / max(1e-5, dot(ab, ab)));
                float2 c = a + t * ab;
                return length(p - c) - r;
            }

            float4 frag (Varyings IN) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float4 col = tex * IN.color;

                if (_CapsuleRadius <= 0.0) return col;

                float d  = CapsuleSDF(IN.worldPos, _CapsuleP0.xy, _CapsuleP1.xy, _CapsuleRadius);
                float fw = max(1e-4, _CapsuleFeather);

                float t = smoothstep(0.0, -fw, d);
                float visibility = 1.0 - t;

                col.a *= visibility;
                return col;
            }
            ENDHLSL
        }
    }
}
