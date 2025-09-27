Shader "Hidden/ScreenHoleFeather"
{
    Properties
    {
        _OverlayColor ("Overlay Color (RGBA)", Color) = (0,0,0,0.85)
        _Center ("Hole Center (Viewport 0..1)", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Hole Radius (Viewport 0..1)", Vector) = (0.18, 0.12, 0, 0)
        _Feather ("Feather (0..0.5 of radius)", Float) = 0.15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _OverlayColor;
            float4 _Center;
            float4 _Radius;
            float  _Feather;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos       : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = (i.screenPos.xy / i.screenPos.w);
                uv.x /= _ScreenParams.x;
                uv.y /= _ScreenParams.y;

                float2 center = _Center.xy;
                float2 radii  = max(_Radius.xy, float2(1e-5,1e-5));

                float2 d = (uv - center) / radii;
                float dist = length(d);

                float fe = saturate(_Feather);
                float inner = 1.0 - fe;

                float a = smoothstep(inner, 1.0, dist);

                return fixed4(_OverlayColor.rgb, _OverlayColor.a * a);
            }
            ENDCG
        }
    }
}
