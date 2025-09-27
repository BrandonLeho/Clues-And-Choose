Shader "Sprites/SoftCapsuleMaskURP"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SoftCapA("Capsule A (world)", Vector) = (0,0,0,0)
        _SoftCapB("Capsule B (world)", Vector) = (1,0,0,0)
        _SoftCapRadius("Capsule Radius (world)", Float) = 0.5
        _SoftCapFeather("Feather (world)", Float) = 0.2
        _SoftCapEnable("Enable", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _Color;

            float4 _SoftCapA;
            float4 _SoftCapB;
            float  _SoftCapRadius;
            float  _SoftCapFeather;
            float  _SoftCapEnable;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
                float3 positionWS  : TEXCOORD1;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(posWS);
                o.uv = v.uv;
                o.color = v.color * _Color;
                o.positionWS = posWS;
                return o;
            }

            float DistPointSegment(float3 P, float3 A, float3 B)
            {
                float3 AB = B - A;
                float t = 0.0;
                float denom = dot(AB, AB);
                if (denom > 1e-8)
                    t = saturate(dot(P - A, AB) / denom);
                float3 C = A + t * AB;
                return length(P - C);
            }

            float4 frag (Varyings i) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;

                if (_SoftCapEnable < 0.5)
                    return tex;

                float d = DistPointSegment(i.positionWS, _SoftCapA.xyz, _SoftCapB.xyz);

                float R = _SoftCapRadius;
                float F = max(_SoftCapFeather, 1e-6);
                float alphaMul = smoothstep(R - F, R, d); 

                tex.a *= alphaMul;
                tex.rgb *= alphaMul;

                return tex;
            }
            ENDHLSL
        }
    }
}
