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

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float RectBorder(float2 uv, float width)
            {
                float2 edge = min(uv, 1.0 - uv);
                float d = min(edge.x, edge.y);
                return 1.0 - smoothstep(width * 0.35, width, d);
            }

            float InnerVignette(float2 uv)
            {
                float2 edge = min(uv, 1.0 - uv);
                float d = min(edge.x, edge.y);
                return 1.0 - smoothstep(0.0, 0.42, d);
            }

            float DiagonalStroke(float2 uv, float scale, float offset, float softness)
            {
                float stripe = abs(frac((uv.x + uv.y + offset) * scale) - 0.5);
                return 1.0 - smoothstep(0.055, 0.055 + softness, stripe);
            }

            float CounterStroke(float2 uv, float scale, float offset, float softness)
            {
                float stripe = abs(frac((uv.x - uv.y + offset) * scale) - 0.5);
                return 1.0 - smoothstep(0.045, 0.045 + softness, stripe);
            }

            float CornerSigil(float2 uv, float width)
            {
                float2 q = min(uv, 1.0 - uv);
                float corner = 1.0 - smoothstep(0.055, 0.17, max(q.x, q.y));
                float armA = 1.0 - smoothstep(width * 0.45, width, abs(q.x - 0.035));
                float armB = 1.0 - smoothstep(width * 0.45, width, abs(q.y - 0.035));
                float notch = smoothstep(0.035, 0.12, max(q.x, q.y));
                return saturate(corner * max(armA, armB) * notch);
            }

            float RuneTicks(float2 uv, float scale, float timeOffset)
            {
                float2 gridUv = uv * scale;
                float2 cell = frac(gridUv) - 0.5;
                float2 id = floor(gridUv);
                float rnd = Hash21(id);
                float rare = step(0.72, rnd);
                float dash = 1.0 - smoothstep(0.018, 0.055, min(abs(cell.x), abs(cell.y)));
                float radialGate = smoothstep(0.16, 0.34, length(uv - 0.5));
                float flicker = 0.55 + 0.45 * sin(_Time.y * 2.1 + rnd * 6.283 + timeOffset);
                return rare * dash * radialGate * flicker;
            }

            float RingSigil(float2 uv, float radius, float width)
            {
                float d = length(uv - 0.5);
                return 1.0 - smoothstep(width, width * 2.3, abs(d - radius));
            }

            float SlashBand(float2 uv, float offset, float width)
            {
                float d = abs((uv.x - uv.y) + offset);
                return 1.0 - smoothstep(width, width * 2.4, d);
            }

            fixed4 ApplyClip(fixed4 col, v2f i)
            {
                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif
                return col;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = (tex2D(_MainTex, i.uv) + _TextureSampleAdd) * i.color;
                float mode = _AffordanceMode;
                if (mode < 0.5)
                {
                    return ApplyClip(col, i);
                }

                float2 uv = i.uv;
                float t = _Time.y;
                float pulse = 0.5 + 0.5 * sin(t * max(_PulseSpeed, 0.01));
                float slowPulse = 0.5 + 0.5 * sin(t * max(_PulseSpeed, 0.01) * 0.43 + 1.7);
                float border = RectBorder(uv, _BorderWidth);
                float corner = CornerSigil(uv, max(_BorderWidth * 0.65, 0.024));
                float rune = RuneTicks(uv, max(_PatternScale * 0.42, 6.0), mode * 0.71);
                float vignette = InnerVignette(uv);
                float grain = (Hash21(floor(uv * 96.0) + floor(t * 6.0)) - 0.5) * 0.035;

                fixed3 baseRgb = col.rgb;
                fixed3 aff = _AffordanceColor.rgb;
                fixed3 sec = _SecondaryColor.rgb;
                float affA = _AffordanceColor.a;
                float ink = saturate(_Intensity);

                // Shared table-language: muted interior, occult edges, and sparse glyph ticks.
                col.rgb = baseRgb * (1.0 - vignette * 0.08 * ink) + grain;

                if (mode < 1.5)
                {
                    float vine = DiagonalStroke(uv + float2(0.0, sin((uv.x + t * 0.18) * 6.283) * 0.018), max(_PatternScale * 0.55, 8.0), t * 0.055, 0.065);
                    float glow = saturate(border * (0.42 + pulse * 0.48) + corner * 0.95 + rune * 0.38 + vine * 0.16);
                    col.rgb = lerp(col.rgb, max(col.rgb, aff * 0.82 + sec * 0.18), 0.18 * ink);
                    col.rgb += (aff * glow + sec * corner * 0.45) * (0.72 * ink);
                    col.a = max(col.a, glow * affA * 0.9);
                }
                else if (mode < 2.5)
                {
                    float grey = dot(baseRgb, fixed3(0.299, 0.587, 0.114));
                    float scratch = max(DiagonalStroke(uv, max(_PatternScale * 0.9, 10.0), t * 0.035, 0.035), CounterStroke(uv, max(_PatternScale * 0.72, 8.0), -t * 0.028, 0.04));
                    float mask = saturate(border * 0.42 + scratch * 0.34 + corner * 0.45);
                    col.rgb = lerp(baseRgb, fixed3(grey, grey, grey) * fixed3(0.72, 0.64, 0.52), 0.62 * ink);
                    col.rgb += (aff * scratch * 0.42 + sec * border * 0.35) * ink;
                    col.a = max(col.a, mask * affA * 0.62);
                }
                else if (mode < 3.5)
                {
                    float ring = RingSigil(uv, 0.43 + slowPulse * 0.012, 0.01);
                    float glow = saturate(border * (0.55 + pulse * 0.38) + corner + ring * 0.42);
                    col.rgb = lerp(col.rgb, col.rgb + aff * 0.32, 0.36 * ink);
                    col.rgb += (aff * glow + sec * ring * 0.55) * (0.72 * ink);
                    col.a = max(col.a, glow * affA * 0.98);
                }
                else if (mode < 4.5)
                {
                    float slash = max(SlashBand(uv, -0.18 + pulse * 0.025, 0.018), SlashBand(uv, 0.18 - pulse * 0.02, 0.014));
                    float ember = DiagonalStroke(uv, max(_PatternScale * 0.68, 8.0), t * 0.11, 0.07);
                    float glow = saturate(border * 0.55 + corner * 0.65 + slash * 0.92 + ember * 0.16);
                    col.rgb = lerp(col.rgb, col.rgb + aff * 0.34, 0.28 * ink);
                    col.rgb += (aff * slash + lerp(aff, sec, pulse) * border * 0.7 + sec * ember * 0.18) * ink;
                    col.a = max(col.a, glow * affA);
                }
                else if (mode < 5.5)
                {
                    float ward = max(SlashBand(uv, -0.25, 0.026), SlashBand(uv, 0.0, 0.022));
                    ward = max(ward, SlashBand(uv, 0.25, 0.026));
                    float pulseWard = ward * (0.72 + pulse * 0.28);
                    col.rgb = baseRgb * (1.0 - 0.46 * ink);
                    col.rgb += aff * (pulseWard * 0.62 + border * 0.32 + corner * 0.28) * ink;
                    col.rgb += sec * vignette * 0.12 * ink;
                    col.a = max(col.a, saturate(pulseWard + border * 0.4) * affA * 0.74);
                }
                else if (mode < 6.5)
                {
                    float thread = CounterStroke(uv, max(_PatternScale * 0.62, 7.0), t * 0.08, 0.05);
                    float ring = RingSigil(uv, 0.36, 0.014);
                    float glow = saturate(border * 0.52 + corner * 0.62 + thread * 0.35 + ring * 0.34);
                    col.rgb = lerp(col.rgb, col.rgb + lerp(aff, sec, slowPulse) * 0.28, 0.38 * ink);
                    col.rgb += (aff * (border + thread * 0.28) + sec * (ring + corner * 0.45)) * (0.68 * ink);
                    col.a = max(col.a, glow * affA * 0.92);
                }
                else
                {
                    float slash = max(SlashBand(uv, -0.08 + sin(t * 3.0) * 0.018, 0.02), CounterStroke(uv, max(_PatternScale * 0.92, 10.0), -t * 0.1, 0.045));
                    float ring = RingSigil(uv, 0.31 + pulse * 0.035, 0.018);
                    float heat = saturate(border * 0.75 + corner + slash * 0.45 + ring * 0.5);
                    col.rgb = lerp(col.rgb, aff, (0.16 + pulse * 0.22) * ink);
                    col.rgb += (aff * heat + sec * (ring + slash * 0.22)) * (0.76 * ink);
                    col.a = max(col.a, heat * affA);
                }

                col.rgb = saturate(col.rgb);
                return ApplyClip(col, i);
            }
            ENDCG
        }
    }
}
