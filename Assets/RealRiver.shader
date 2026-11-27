Shader "Custom/RealRiver"
{
    Properties
    {
        [Header(Colors)]
        _ShallowColor ("Shallow color", Color) = (0.20, 0.55, 0.52, 1)
        _DeepColor ("Deep color", Color) = (0.02, 0.12, 0.18, 1)
        _DepthDistance ("Depth until deep color (m)", Float) = 4
        _Clarity ("Visibility under water (m)", Float) = 3

        [Header(Waves)]
        _WaveHeight ("Wave height (m)", Range(0, 1)) = 0.12
        _WaveLength ("Wave length (m)", Float) = 9
        _WaveSpeed ("Wave speed", Float) = 1.2
        _FlowDir ("Flow direction (X, Z)", Vector) = (1, 0, 0.3, 0)
        _FlowSpeed ("Flow speed (m per s)", Float) = 1.5

        [Header(Ripples)]
        _RippleScale ("Ripple size (m)", Float) = 2.2
        _RippleStrength ("Ripple strength", Range(0, 1)) = 0.25

        [Header(Light)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.92
        _Reflection ("Reflection strength", Range(0, 1)) = 0.8
        _Refraction ("Refraction strength", Range(0, 0.1)) = 0.025

        [Header(Foam)]
        _FoamColor ("Foam color", Color) = (0.9, 0.95, 0.95, 1)
        _FoamDepth ("Foam width by depth (m)", Float) = 0.9
        _FoamScale ("Foam pattern size (m)", Float) = 0.8
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor, _DeepColor, _FoamColor, _FlowDir;
                float _DepthDistance, _Clarity;
                float _WaveHeight, _WaveLength, _WaveSpeed, _FlowSpeed;
                float _RippleScale, _RippleStrength;
                float _Smoothness, _Reflection, _Refraction;
                float _FoamDepth, _FoamScale;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
                float2 baseXZ     : TEXCOORD2;
                float  fog        : TEXCOORD3;
            };

            float2 FlowDirection()
            {
                float2 d = _FlowDir.xz;
                return dot(d, d) < 0.0001 ? float2(1, 0) : normalize(d);
            }

            // adds one sine wave: height and slope
            void Wave(float2 p, float2 dir, float len, float amp, float speed, inout float h, inout float2 slope)
            {
                float k = 6.28318 / max(len, 0.1);
                float phase = dot(p, dir) * k - _Time.y * speed * sqrt(9.81 * k);
                h += sin(phase) * amp;
                slope += dir * (cos(phase) * amp * k);
            }

            void Waves(float2 p, out float h, out float2 slope)
            {
                h = 0;
                slope = 0;
                float2 f = FlowDirection();
                float2 s = float2(-f.y, f.x);
                Wave(p, f, _WaveLength, _WaveHeight * 0.55, _WaveSpeed, h, slope);
                Wave(p, normalize(f + s * 0.7), _WaveLength * 0.63, _WaveHeight * 0.30, _WaveSpeed * 1.1, h, slope);
                Wave(p, normalize(f - s * 1.1), _WaveLength * 0.37, _WaveHeight * 0.15, _WaveSpeed * 1.2, h, slope);
            }

            float Hash(float2 p)
            {
                float3 q = frac(float3(p.xyx) * 0.1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            float RippleHeight(float2 p)
            {
                float2 flow = FlowDirection() * _FlowSpeed * _Time.y;
                float scale = max(_RippleScale, 0.05);
                float a = Noise((p - flow) / scale);
                float b = Noise((p - flow * 0.6) / (scale * 0.47) + float2(17.3, 5.1) + _Time.y * 0.25);
                return a * 0.65 + b * 0.35;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                o.baseXZ = ws.xz;
                float h; float2 slope;
                Waves(ws.xz, h, slope);
                ws.y += h;
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 toCam = _WorldSpaceCameraPos - i.positionWS;
                float dist = length(toCam);
                float3 V = toCam / dist;

                // normal from big waves + small ripples (ripples fade out far away to stop flickering)
                float h; float2 slope;
                Waves(i.baseXZ, h, slope);
                float e = 0.15;
                float r0 = RippleHeight(i.baseXZ);
                float2 ripple = float2(RippleHeight(i.baseXZ + float2(e, 0)) - r0,
                                       RippleHeight(i.baseXZ + float2(0, e)) - r0) / e;
                float fade = saturate(1.0 - dist / 250.0);
                slope += ripple * _RippleStrength * fade;
                float3 N = normalize(float3(-slope.x, 1, -slope.y));

                // water depth under this pixel
                float2 uv = i.screenPos.xy / i.screenPos.w;
                float surfaceEye = i.screenPos.w;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float3 sceneWS = _WorldSpaceCameraPos - toCam * (sceneEye / surfaceEye);
                float shoreDepth = max(0, i.positionWS.y - sceneWS.y);

                // refraction (do not pull in objects that are in front of the water)
                float2 ruv = uv + N.xz * _Refraction * saturate(shoreDepth);
                float refrEye = LinearEyeDepth(SampleSceneDepth(ruv), _ZBufferParams);
                if (refrEye < surfaceEye) { ruv = uv; refrEye = sceneEye; }
                float3 refrWS = _WorldSpaceCameraPos - toCam * (refrEye / surfaceEye);
                float depth = max(0, i.positionWS.y - refrWS.y);
                float3 below = SampleSceneColor(ruv);

                Light light = GetMainLight();
                float3 ambient = SampleSH(float3(0, 1, 0));
                float3 lit = ambient + light.color * saturate(dot(N, light.direction));

                float3 water = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(depth / max(_DepthDistance, 0.01))) * lit;
                float opacity = 1.0 - exp(-depth * 2.0 / max(_Clarity, 0.01));
                float3 color = lerp(below, water, opacity);

                // sky reflection (stronger at flat viewing angles)
                float3 R = reflect(-V, N);
                R.y = abs(R.y);
                float3 sky = GlossyEnvironmentReflection(R, 1.0 - _Smoothness, 1.0);
                float fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(N, V)), 5.0);
                color = lerp(color, sky, saturate(fresnel * _Reflection));

                // sun glitter
                float3 H = normalize(light.direction + V);
                float spec = pow(saturate(dot(N, H)), exp2(_Smoothness * 10.0));
                color += light.color * spec * 1.5;

                // foam along the banks
                float shore = 1.0 - saturate(shoreDepth / max(_FoamDepth, 0.01));
                float2 flow = FlowDirection() * _FlowSpeed * _Time.y * 0.5;
                float pattern = Noise((i.baseXZ - flow) / max(_FoamScale, 0.05)) * 0.7
                              + Noise((i.baseXZ + flow * 0.4) / max(_FoamScale * 0.4, 0.02)) * 0.3;
                float bands = 0.15 * sin(shore * 14.0 + _Time.y * 2.0);
                float foam = smoothstep(0.55, 0.75, pattern * 0.6 + shore * 0.65 + bands) * saturate(shore * 2.0);
                color = lerp(color, _FoamColor.rgb * (ambient + light.color), foam * _FoamColor.a);

                // soft edge where water meets the ground
                color = lerp(below, color, saturate(shoreDepth / 0.15));

                color = MixFog(color, i.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
