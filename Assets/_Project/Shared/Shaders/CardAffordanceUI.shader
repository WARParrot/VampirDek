Shader "VampirDek/UI/CardAffordance"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _AffordanceMode ("Affordance Mode", Float) = 0
        _AffordanceColor ("Affordance Color", Color) = (0,1,1,1)
        _SecondaryColor ("Secondary Color", Color) = (1,1,1,1)
        _Intensity ("Intensity", Range(0, 2)) = 1
        _PulseSpeed ("Pulse Speed", Range(0, 12)) = 4
        _BorderWidth ("Border Width", Range(0, 0.25)) = 0.055
        _PatternScale ("Pattern Scale", Range(2, 80)) = 22

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
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
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "UIAffordance"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _AffordanceMode;
            fixed4 _AffordanceColor;
            fixed4 _SecondaryColor;
            float _Intensity;
            float _PulseSpeed;
            float _BorderWidth;
            float _PatternScale;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            float BorderMask(float2 uv, float width)
            {
                float2 edge = min(uv, 1.0 - uv);
                float d = min(edge.x, edge.y);
                return 1.0 - smoothstep(width * 0.45, width, d);
            }

            float Hatch(float2 uv, float scale, float offset)
            {
                float stripe = frac((uv.x + uv.y + offset) * scale);
                return smoothstep(0.52, 0.58, stripe) * (1.0 - smoothstep(0.72, 0.82, stripe));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = (tex2D(_MainTex, i.uv) + _TextureSampleAdd) * i.color;
                float mode = _AffordanceMode;
                if (mode < 0.5)
                {
                    #ifdef UNITY_UI_CLIP_RECT
                    col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                    #endif
                    #ifdef UNITY_UI_ALPHACLIP
                    clip(col.a - 0.001);
                    #endif
                    return col;
                }

                float pulse = 0.5 + 0.5 * sin(_Time.y * max(_PulseSpeed, 0.01));
                float border = BorderMask(i.uv, _BorderWidth);
                float hatch = Hatch(i.uv, _PatternScale, _Time.y * 0.18);
                float scanPhase = frac((i.uv.x - i.uv.y) * 3.0 + _Time.y * 0.55);
                float scan = smoothstep(0.42, 0.5, scanPhase) * (1.0 - smoothstep(0.56, 0.64, scanPhase));

                fixed3 baseRgb = col.rgb;
                fixed3 aff = _AffordanceColor.rgb;
                fixed3 sec = _SecondaryColor.rgb;

                if (mode < 1.5)
                {
                    col.rgb = lerp(baseRgb, max(baseRgb, aff), 0.18 * _Intensity);
                    col.rgb += aff * (border * (0.55 + pulse * 0.45) + scan * 0.12) * _Intensity;
                    col.a = max(col.a, border * _AffordanceColor.a * 0.92);
                }
                else if (mode < 2.5)
                {
                    float grey = dot(baseRgb, fixed3(0.299, 0.587, 0.114));
                    col.rgb = lerp(baseRgb, fixed3(grey, grey, grey) * 0.72, 0.58 * _Intensity);
                    col.rgb += aff * (hatch * 0.35 + border * 0.25) * _Intensity;
                    col.a = max(col.a, max(border * _AffordanceColor.a * 0.55, hatch * _AffordanceColor.a * 0.28));
                }
                else if (mode < 3.5)
                {
                    col.rgb = lerp(baseRgb, baseRgb + aff * 0.35, 0.35 * _Intensity);
                    col.rgb += aff * border * (0.65 + pulse * 0.75) * _Intensity;
                    col.a = max(col.a, border * _AffordanceColor.a);
                }
                else if (mode < 4.5)
                {
                    col.rgb = lerp(baseRgb, baseRgb + aff * 0.28, 0.32 * _Intensity);
                    col.rgb += lerp(aff, sec, pulse) * border * (0.85 + pulse * 0.45) * _Intensity;
                    col.a = max(col.a, border * _AffordanceColor.a);
                }
                else if (mode < 5.5)
                {
                    col.rgb = baseRgb * (1.0 - 0.42 * _Intensity);
                    col.rgb += aff * (hatch * 0.48 + border * 0.35) * _Intensity;
                    col.a = max(col.a, max(border * _AffordanceColor.a * 0.75, hatch * _AffordanceColor.a * 0.35));
                }
                else if (mode < 6.5)
                {
                    col.rgb = lerp(baseRgb, baseRgb + lerp(aff, sec, pulse) * 0.34, 0.42 * _Intensity);
                    col.rgb += (aff * border + sec * scan * 0.4) * _Intensity;
                    col.a = max(col.a, border * _AffordanceColor.a * 0.95);
                }
                else
                {
                    col.rgb = lerp(baseRgb, aff, (0.18 + pulse * 0.25) * _Intensity);
                    col.rgb += aff * (border + hatch * 0.4) * _Intensity;
                    col.a = max(col.a, border * _AffordanceColor.a);
                }

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
