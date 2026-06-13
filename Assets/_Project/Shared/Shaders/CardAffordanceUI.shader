Shader "VampirDek/UI/CardAffordance"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _AffordanceMode ("Affordance Mode", Float) = 0
        _AffordanceColor ("Affordance Color", Color) = (0,1,1,1)
        _SecondaryColor ("Secondary Color", Color) = (1,1,1,1)
        _Intensity ("Intensity", Range(0, 2)) = 0.95
        _PulseSpeed ("Pulse Speed", Range(0, 12)) = 2.4
        _BorderWidth ("Border Width", Range(0, 0.25)) = 0.055
        _PatternScale ("Pattern Scale", Range(2, 80)) = 14

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
                    col.rgb = saturate(col.rgb);
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

                // Shared table-language: each state gets one readable hero mark plus a quiet common edge.
                // This keeps the shader authored/stylized without turning every affordance into a VFX stack.
                float quietEdge = saturate(border * 0.62 + corner * 0.28);
                float paperDim = 0.045 * ink;
                col.rgb = baseRgb * (1.0 - vignette * paperDim) + grain * 0.45;

                if (mode < 1.5)
                {
                    // Compatible: a calm pact-glow. Success should feel inviting, not noisy.
                    float pact = saturate(quietEdge * (0.78 + pulse * 0.16) + rune * 0.10);
                    col.rgb = lerp(col.rgb, max(col.rgb, aff * 0.70 + sec * 0.16), 0.10 * ink);
                    col.rgb += (aff * pact + sec * corner * 0.18) * (0.48 * ink);
                    col.a = max(col.a, pact * affA * 0.72);
                }
                else if (mode < 2.5)
                {
                    // Incompatible: a single dry rejection stroke and desaturation.
                    float grey = dot(baseRgb, fixed3(0.299, 0.587, 0.114));
                    float refusal = SlashBand(uv, 0.0 + slowPulse * 0.018, 0.022);
                    float ashEdge = saturate(border * 0.30 + refusal * 0.85);
                    col.rgb = lerp(baseRgb, fixed3(grey, grey, grey) * fixed3(0.70, 0.62, 0.52), 0.48 * ink);
                    col.rgb += (aff * refusal * 0.52 + sec * border * 0.18) * ink;
                    col.a = max(col.a, ashEdge * affA * 0.66);
                }
                else if (mode < 3.5)
                {
                    // Selected: a focused moon-ring. No extra glyph noise.
                    float ring = RingSigil(uv, 0.42 + slowPulse * 0.010, 0.012);
                    float focus = saturate(quietEdge * 0.64 + ring * 0.92);
                    col.rgb = lerp(col.rgb, col.rgb + aff * 0.24, 0.22 * ink);
                    col.rgb += (aff * quietEdge + sec * ring * 0.72) * (0.56 * ink);
                    col.a = max(col.a, focus * affA * 0.82);
                }
                else if (mode < 4.5)
                {
                    // Target: a blood-moon reticle. Keep it circular/aimed so it cannot read as refusal.
                    float targetRing = RingSigil(uv, 0.34 + pulse * 0.014, 0.014);
                    float innerRing = RingSigil(uv, 0.18, 0.010);
                    float2 centered = abs(uv - 0.5);
                    float verticalSight = (1.0 - smoothstep(0.010, 0.026, centered.x)) * smoothstep(0.20, 0.34, centered.y);
                    float horizontalSight = (1.0 - smoothstep(0.010, 0.026, centered.y)) * smoothstep(0.20, 0.34, centered.x);
                    float reticle = saturate(targetRing * 0.95 + innerRing * 0.58 + max(verticalSight, horizontalSight) * 0.72);
                    float targetEdge = saturate(border * 0.36 + corner * 0.24 + reticle);
                    col.rgb = lerp(col.rgb, col.rgb + aff * 0.28, 0.18 * ink);
                    col.rgb += (aff * reticle + sec * targetRing * (0.28 + pulse * 0.14)) * (0.82 * ink);
                    col.a = max(col.a, targetEdge * affA * 0.90);
                }
                else if (mode < 5.5)
                {
                    // Blocked: a legible barred seal, darker but not buried.
                    float barA = SlashBand(uv, -0.18, 0.030);
                    float barB = SlashBand(uv, 0.18, 0.030);
                    float barred = saturate(max(barA, barB) * (0.82 + pulse * 0.12));
                    col.rgb = baseRgb * (1.0 - 0.34 * ink);
                    col.rgb += (aff * barred * 0.74 + sec * quietEdge * 0.28) * ink;
                    col.a = max(col.a, saturate(barred + border * 0.22) * affA * 0.76);
                }
                else if (mode < 6.5)
                {
                    // Planned: a thin fate-thread, intentionally quieter than selected/target.
                    float thread = CounterStroke(uv, max(_PatternScale * 0.44, 5.5), t * 0.045, 0.075);
                    float threadMask = saturate(thread * 0.46 + RingSigil(uv, 0.35, 0.018) * 0.34);
                    float planned = saturate(quietEdge * 0.46 + threadMask);
                    col.rgb = lerp(col.rgb, col.rgb + lerp(aff, sec, slowPulse) * 0.18, 0.20 * ink);
                    col.rgb += (aff * threadMask + sec * corner * 0.24) * (0.52 * ink);
                    col.a = max(col.a, planned * affA * 0.70);
                }
                else
                {
                    // Warning: fever pulse around the edge; readable urgency without full-surface clutter.
                    float feverRing = RingSigil(uv, 0.33 + pulse * 0.018, 0.018);
                    float alarm = saturate(border * (0.70 + pulse * 0.22) + feverRing * 0.72 + corner * 0.32);
                    col.rgb = lerp(col.rgb, aff, (0.08 + pulse * 0.13) * ink);
                    col.rgb += (aff * alarm + sec * feverRing * 0.36) * (0.62 * ink);
                    col.a = max(col.a, alarm * affA * 0.84);
                }
                return ApplyClip(col, i);
            }
            ENDCG
        }
    }
}
