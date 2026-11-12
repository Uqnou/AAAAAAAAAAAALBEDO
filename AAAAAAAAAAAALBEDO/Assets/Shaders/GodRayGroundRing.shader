Shader "Custom/GodRayGroundRing"
{
    Properties
    {
        [HDR] _TintColor ("Tint Color", Color) = (1.0, 0.9, 0.6, 1.0)
        _Intensity ("Intensity", Range(0.1, 10.0)) = 2.0
        _InnerRadius ("Inner Radius", Range(0.0, 1.0)) = 0.65
        _Feather ("Edge Feather", Range(0.01, 0.5)) = 0.15
        _PulseSpeed ("Pulse Speed", Range(0.0, 10.0)) = 2.0
        _PulseAmount ("Pulse Amount", Range(0.0, 1.0)) = 0.25
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+10" 
            "RenderPipeline" = "UniversalPipeline" 
            "IgnoreProjector" = "True"
        }

        LOD 100
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "GroundRingPass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                float _Intensity;
                float _InnerRadius;
                float _Feather;
                float _PulseSpeed;
                float _PulseAmount;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centeredUV = (input.uv - 0.5) * 2.0;
                float dist = length(centeredUV);

                float outerFade = 1.0 - smoothstep(1.0 - _Feather, 1.0, dist);
                float innerFade = smoothstep(_InnerRadius - _Feather, _InnerRadius, dist);
                float ringAlpha = outerFade * innerFade;

                float innerDisc = (1.0 - smoothstep(0.0, 0.95, dist)) * 0.15;
                float totalAlpha = saturate(ringAlpha + innerDisc);

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                half3 finalColor = _TintColor.rgb * _Intensity * pulse * totalAlpha;
                return half4(finalColor, totalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
