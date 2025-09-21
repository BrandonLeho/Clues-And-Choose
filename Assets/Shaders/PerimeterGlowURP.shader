Shader "UI/PerimeterGlowURP"
{
    Properties
    {
        _Intensity ("Intensity", Range(0,8)) = 1
        _OuterFade ("Outer Fade (pow)", Range(0.1,4)) = 1.5
        _InnerFeather ("Inner Feather", Range(0,1)) = 0.15
        _Waves ("Wave Amplitude", Range(0,1)) = 0.25
        _WaveCount ("Wave Count", Range(1,16)) = 6
        _WaveSpeed ("Wave Speed", Range(-6,6)) = 1.5
        _OverallPulse ("Overall Pulse Amp", Range(0,2)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline"}
        ZTest Always
        ZWrite Off
        Blend One One
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0; 
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
                float2 uv          : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
            float _Intensity;
            float _OuterFade;
            float _InnerFeather;
            float _Waves;
            float _WaveCount;
            float _WaveSpeed;
            float _OverallPulse;
            CBUFFER_END

            Varyings Vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 Frag (Varyings i) : SV_Target
            {
                float r = saturate(i.uv.y);
                float inner = smoothstep(0.0, _InnerFeather, r); 
                float outer = pow(saturate(1.0 - r), _OuterFade);  

                float t = _Time.y * _WaveSpeed;
                float s = i.uv.x; 
                float waves = 0.0;
                float k1 = max(1.0, floor(_WaveCount));
                float k2 = k1 * 2.0;
                waves += sin( (s + t) * TWO_PI * k1 );
                waves += 0.5 * sin( (s - t*1.2) * TWO_PI * k2 );

                float waveMask = 1.0 + _Waves * (waves * 0.5);
                waveMask = max(0.0, waveMask);

                float breathe = 1.0 + _OverallPulse * (0.5 + 0.5 * sin(_Time.y * 0.8));

                float prof = inner * outer * waveMask * breathe;

                half3 col = i.color.rgb * (_Intensity * prof);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
