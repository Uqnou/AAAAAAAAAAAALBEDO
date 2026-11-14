Shader "ALBEDO/BlackFire"
{
    Properties
    {
        _CoreColor ("Flame Core Color", Color) = (0, 0, 0, 1)
        [HDR] _RimColor ("White Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Sharpness", Range(0.5, 8.0)) = 2.6
        _FlickerSpeed ("Flicker Speed", Float) = 6.5
        _WaveAmplitude ("Flame Sway Amplitude", Float) = 0.08
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
                float heightMask : TEXCOORD2;
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
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float heightMask = saturate(input.positionOS.y + 0.5);
                float t = _Time.y * _FlickerSpeed;

                float3 posOS = input.positionOS.xyz;
                posOS.x += sin(t + posOS.y * 6.0 + posOS.z * 4.0) * _WaveAmplitude * heightMask;
                posOS.z += cos(t * 1.3 + posOS.y * 5.0 + posOS.x * 4.0) * _WaveAmplitude * heightMask;
                posOS.y += sin(t * 2.1 + posOS.x * 3.0) * (_WaveAmplitude * 0.6) * heightMask;

                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normInputs.normalWS);
                output.heightMask = heightMask;
                output.fogCoord = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half ndotv = saturate(dot(normalWS, viewDirWS));

                half fresnel = pow(1.0h - ndotv, _RimPower);
                half flicker = 0.85h + 0.15h * (half)sin(_Time.y * _FlickerSpeed * 1.7 + input.positionWS.y * 8.0);
                half rimMask = smoothstep(0.28h, 0.52h, fresnel * flicker + (half)input.heightMask * 0.12h);

                half3 col = lerp(_CoreColor.rgb, _RimColor.rgb, rimMask);
                col = MixFog(col, input.fogCoord);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
