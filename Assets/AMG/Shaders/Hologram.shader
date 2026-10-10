// 拠点の研究室で使う、ホログラムの設計図（青く光る半透明・加算合成）。
// ・輪郭ほど明るく光る（フレネル）＋物体に貼り付いた設計図のグリッド線
// ・走査線とちらつき、下から上へ流れる光の帯
// ・_Reveal（0〜1）で下から組み上がっていき、組み上がる境目が白く光る
// ・_Glitch で横にずれるノイズ（消えるとき・切り替わるとき）
// ・_IsLine = 1 は線（ワイヤーフレーム・リング・注釈線）用。法線を使わず一定の明るさで描く
Shader "AMG/Hologram"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.25, 0.85, 1.8, 1)
        [HDR] _EdgeColor ("Reveal Edge Color", Color) = (1.5, 2.6, 3.2, 1)
        _Alpha ("Alpha", Range(0, 2)) = 1
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.2
        _FillAlpha ("Fill", Range(0, 1)) = 0.05
        _GridDensity ("Grid Density", Float) = 22
        _GridWidth ("Grid Width", Range(0.001, 0.25)) = 0.035
        _GridAlpha ("Grid Alpha", Range(0, 2)) = 0.45
        _ScanDensity ("Scanline Density", Float) = 90
        _ScanSpeed ("Scanline Speed", Float) = 0.6
        _Flicker ("Flicker", Range(0, 1)) = 0.12
        _Reveal ("Reveal (0-1)", Range(0, 1.05)) = 1.05
        _BoundsMinY ("Bounds Min Y (world)", Float) = 0
        _BoundsMaxY ("Bounds Max Y (world)", Float) = 1
        _IsLine ("Is Line", Float) = 0
        _Glitch ("Glitch", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Hologram"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _EdgeColor;
                half _Alpha;
                half _RimPower;
                half _FillAlpha;
                float _GridDensity;
                half _GridWidth;
                half _GridAlpha;
                float _ScanDensity;
                float _ScanSpeed;
                half _Flicker;
                half _Reveal;
                float _BoundsMinY;
                float _BoundsMaxY;
                half _IsLine;
                half _Glitch;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };

            float Hash(float n) { return frac(sin(n) * 43758.5453); }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                // グリッチ：細い横帯ごとに、ときどき横へずれる
                float band = floor(ws.y * 24.0);
                float tick = floor(_Time.y * 18.0);
                float on = step(0.72, Hash(band * 1.7 + tick));
                ws.x += (Hash(band + tick * 3.1) - 0.5) * 0.18 * _Glitch * on;
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // 下から組み上がる
                float h = saturate((i.positionWS.y - _BoundsMinY) / max(0.0001, _BoundsMaxY - _BoundsMinY));
                clip(_Reveal - h);
                float edge = saturate(1.0 - (_Reveal - h) / 0.035) * step(_Reveal, 1.0);

                half intensity;
                if (_IsLine > 0.5)
                {
                    intensity = 0.9;
                }
                else
                {
                    half3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                    half3 N = normalize(i.normalWS);
                    half rim = pow(1.0 - abs(dot(N, V)), _RimPower);
                    // 物体に貼り付いた設計図のグリッド（物体の座標で引くので、回転しても一緒に回る）
                    float3 g = abs(frac(i.positionOS * _GridDensity) - 0.5);
                    float grid = step(0.5 - _GridWidth, max(max(g.x, g.y), g.z));
                    intensity = rim * 1.5 + grid * _GridAlpha + _FillAlpha;
                }

                // 走査線・ちらつき・下から上へ流れる光の帯
                float scan = 0.7 + 0.3 * sin((i.positionWS.y * _ScanDensity - _Time.y * _ScanSpeed * 40.0));
                float flick = 1.0 - _Flicker * Hash(floor(_Time.y * 24.0));
                float sweep = pow(saturate(1.0 - abs(frac(_Time.y * 0.3) - h) * 10.0), 2.0);
                intensity = intensity * scan * flick + sweep * 0.5 + _Glitch * 0.3 * Hash(floor(_Time.y * 40.0) + h * 13.0);

                half3 col = _Color.rgb * intensity + _EdgeColor.rgb * edge * 1.5;
                return half4(col * _Alpha, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
