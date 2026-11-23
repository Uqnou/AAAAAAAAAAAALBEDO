Shader "ALBEDO/BlackFire"
{
    Properties
    {
        _CoreColor ("Flame Core Color", Color) = (0, 0, 0, 1)
        [HDR] _RimColor ("White Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Sharpness", Range(0.5, 8.0)) = 2.4
        _FlickerSpeed ("Viscosity Flow Speed", Float) = 2.4
        _WaveAmplitude ("Flame Sway Amplitude", Float) = 0.11
        _BlobScale ("Viscous Blob Scale", Float) = 3.2
        _GlossStrength ("Viscous Sheen", Range(0.0, 1.0)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+5"
        }

        Pass
        {
            Name "BlackFireForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float fogCoord : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _RimColor;
                half _RimPower;
                float _FlickerSpeed;
                float _WaveAmplitude;
                float _BlobScale;
                half _GlossStrength;
            CBUFFER_END

            float ViscousField(float3 p, float t)
            {
                float3 q = p * _BlobScale;
                float w1 = sin(q.x + t) * cos(q.y - t * 1.3) + sin(q.z + t * 0.8);
                float w2 = sin(q.y * 1.7 - t * 1.6 + w1 * 1.2) * cos(q.x * 1.4 + q.z * 1.1 + t * 0.6);
                return 0.5 + 0.25 * (w1 * 0.5 + w2);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 pivotWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                half3 normalWS = NormalizeNormalPerVertex(TransformObjectToWorldNormal(input.normalOS));
                float3 relWS = posWS - pivotWS;
                float meshScale = saturate(length(relWS) * 1.5);
                float heightMask = saturate(relWS.y + 0.5);
                float t = _Time.y * _FlickerSpeed;

                float bulge = sin(relWS.y * 4.5 - t * 1.4) * cos(relWS.x * 3.8 + t * 0.9)
                            + sin(relWS.z * 4.2 + t * 1.1) * 0.5;
                float amp = _WaveAmplitude * meshScale;
                posWS += normalWS * (bulge * amp * 0.65 * (0.35 + heightMask * 0.65));
                posWS.x += sin(t * 0.9 + relWS.y * 4.0) * (amp * 0.7) * heightMask;
                posWS.z += cos(t * 1.1 + relWS.y * 3.5) * (amp * 0.7) * heightMask;

                output.positionCS = TransformWorldToHClip(posWS);
                output.positionWS = posWS;
                output.normalWS = normalWS;
                output.positionOS = posWS - pivotWS;
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half ndotv = saturate(dot(normalWS, viewDirWS));

                float t = _Time.y * _FlickerSpeed;
                float3 flowPos = input.positionOS + float3(0.0, -t * 0.35, 0.0);
                half blob = (half)ViscousField(flowPos, t);

                half fresnel = pow(1.0h - ndotv, _RimPower);
                half heightMask = (half)saturate(input.positionOS.y + 0.5);
                half rimSignal = fresnel * (0.72h + 0.45h * blob) + heightMask * 0.1h;
                half rimMask = smoothstep(0.28h, 0.48h, rimSignal);

                // Brilho viscoso suave nas cristas internas do fluido escuro
                half sheen = smoothstep(0.68h, 0.88h, blob) * ndotv * _GlossStrength;

                half3 col = lerp(_CoreColor.rgb, _RimColor.rgb, saturate(rimMask + sheen));
                col = MixFog(col, input.fogCoord);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
