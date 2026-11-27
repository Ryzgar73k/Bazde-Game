Shader "Bazde/StylizedWavySea"
{
    Properties
    {
        _WaveTex ("Wave Pattern", 2D) = "white" {}
        _DeepColor ("Deep Color", Color) = (0.015, 0.12, 0.30, 1)
        _ShallowColor ("Shallow Color", Color) = (0.04, 0.42, 0.62, 1)
        _FoamColor ("Foam Color", Color) = (0.92, 0.97, 1.0, 1)
        _FresnelColor ("Fresnel Sky Color", Color) = (0.20, 0.58, 0.82, 1)
        _FresnelPower ("Fresnel Power", Range(1, 8)) = 3.5
        _SpecColorSea ("Specular Tint", Color) = (1, 0.97, 0.88, 1)
        _SpecPower ("Specular Power", Range(8, 128)) = 72
        _SpecIntensity ("Specular Intensity", Range(0, 2)) = 0.90
        _WaveAmplitude ("Wave Amplitude", Range(0, 3.0)) = 1.15
        _WaveSpeed ("Wave Speed", Range(0, 4)) = 1.45
        _WaveScale ("Wave Frequency Scale", Range(0.02, 0.6)) = 0.24
        _NormalStrength ("Wave Slope Contrast", Range(0.5, 4.0)) = 2.2
        _PatternScale ("Pattern World Scale", Range(0.005, 0.2)) = 0.045
        _FoamThreshold ("Foam Threshold", Range(0.2, 0.95)) = 0.80
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float crest : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            TEXTURE2D(_WaveTex);
            SAMPLER(sampler_WaveTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _WaveTex_ST;
                half4 _DeepColor;
                half4 _ShallowColor;
                half4 _FoamColor;
                half4 _FresnelColor;
                half4 _SpecColorSea;
                float _FresnelPower;
                float _SpecPower;
                float _SpecIntensity;
                float _WaveAmplitude;
                float _WaveSpeed;
                float _WaveScale;
                float _NormalStrength;
                float _PatternScale;
                float _FoamThreshold;
            CBUFFER_END

            void EvalWave(
                float2 xz,
                float2 dir,
                float freq,
                float amp,
                float speed,
                float t,
                inout float h,
                inout float dhdx,
                inout float dhdz)
            {
                float phase = dot(xz, dir) * freq + t * speed;
                float s = sin(phase);
                float c = cos(phase);
                // Dik tepeli ve geniş çukurlu trokoidal dalga profili
                h += (s + 0.24 * (s * s - 0.5)) * amp;
                float d = c * (1.0 + 0.48 * s) * amp * freq;
                dhdx += d * dir.x;
                dhdz += d * dir.y;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float2 xz = worldPos.xz;
                float t = _Time.y * _WaveSpeed;

                float h = 0.0;
                float dhdx = 0.0;
                float dhdz = 0.0;

                EvalWave(xz, float2(0.86, 0.51), 1.00 * _WaveScale, 0.40 * _WaveAmplitude, 1.00, t, h, dhdx, dhdz);
                EvalWave(xz, float2(-0.42, 0.91), 1.37 * _WaveScale, 0.26 * _WaveAmplitude, 1.24, t, h, dhdx, dhdz);
                EvalWave(xz, float2(0.28, -0.96), 2.00 * _WaveScale, 0.16 * _WaveAmplitude, 1.56, t, h, dhdx, dhdz);
                EvalWave(xz, float2(-0.92, -0.39), 2.75 * _WaveScale, 0.11 * _WaveAmplitude, 1.88, t, h, dhdx, dhdz);
                EvalWave(xz, float2(0.62, -0.78), 3.70 * _WaveScale, 0.07 * _WaveAmplitude, 2.20, t, h, dhdx, dhdz);

                worldPos.y += h;

                OUT.worldPos = worldPos;
                OUT.normalWS = normalize(float3(-dhdx * _NormalStrength, 1.0, -dhdz * _NormalStrength));
                OUT.crest = saturate((h / max(0.05, _WaveAmplitude)) * 0.5 + 0.5);
                OUT.positionCS = TransformWorldToHClip(worldPos);
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 V = SafeNormalize(GetWorldSpaceViewDir(IN.worldPos));

                float t = _Time.y * _WaveSpeed;
                float2 uvA = IN.worldPos.xz * _PatternScale + float2(0.032, 0.021) * t;
                float2 uvB = IN.worldPos.xz * (_PatternScale * 1.47) + float2(-0.025, 0.034) * t;

                half texA = SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, uvA).r;
                half texB = SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, uvB).r;

                // StylizedWaterPattern [0.85..1.0] aralığını [0..1] aralığına aç
                float pA = saturate(((float)texA - 0.85) / 0.15);
                float pB = saturate(((float)texB - 0.85) / 0.15);
                float causticLine = pow(saturate(pA * pB * 1.8), 2.2);

                float sparkle = (pA - pB) * 0.16;
                float3 N = normalize(IN.normalWS + float3(sparkle, 0.0, -sparkle));

                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float ndotl = saturate(dot(N, L));

                float3 H = SafeNormalize(L + V);
                float spec = pow(saturate(dot(N, H)), _SpecPower) * _SpecIntensity;

                float shoulder = smoothstep(0.18, 0.88, IN.crest);
                half3 baseWater = lerp(_DeepColor.rgb, _ShallowColor.rgb, (half)shoulder);
                baseWater *= lerp(0.54h, 1.28h, (half)ndotl);

                half fresnel = pow(1.0h - saturate((half)dot(N, V)), (half)_FresnelPower) * 0.22h;
                half3 color = lerp(baseWater, _FresnelColor.rgb, fresnel);

                color += _ShallowColor.rgb * (half)(causticLine * 0.16);
                color += (half3)mainLight.color * _SpecColorSea.rgb * (half)spec;

                // Köpük yalnızca gerçek dalga tepelerinin en üst kısmında çıksın
                float foamMask = smoothstep(_FoamThreshold, 0.98, IN.crest + (pA - 0.5) * 0.18);
                color = lerp(color, _FoamColor.rgb, (half)foamMask);

                color = MixFog(color, IN.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}
