Shader "Custom/URP_ItemOccludedGlow"
{
    Properties
    {
        [HDR] _GlowColor ("Glow Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _ProximityAlpha ("Proximity Alpha", Range(0.0, 1.0)) = 0.0
        _OutlineWidth ("Outline Screen Thickness", Range(0.0, 5.0)) = 1.0
        _FillOpacity ("Fill Interior Opacity", Range(0.0, 1.0)) = 0.34
        _RimStrength ("Rim Edge Strength", Range(0.0, 2.0)) = 0.75
        _DepthBias ("Occlusion Depth Margin", Float) = 0.06
        [HideInInspector] _MeshCenterOS ("Mesh Bounds Center OS", Vector) = (0.0, 0.0, 0.0, 0.0)
        _StencilRef ("Stencil Ref", Float) = 44
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comp", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass ("Stencil Pass", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+200"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "OccludedGlowPass"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            Stencil
            {
                Ref [_StencilRef]
                Comp [_StencilComp]
                Pass [_StencilPass]
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
                float eyeDepth : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                float _ProximityAlpha;
                float _OutlineWidth;
                float _FillOpacity;
                float _RimStrength;
                float _DepthBias;
                float4 _MeshCenterOS;
            CBUFFER_END

            bool IsSkyDepth(float rawDepth)
            {
                #if UNITY_REVERSED_Z
                    return rawDepth <= 0.00001;
                #else
                    return rawDepth >= 0.99999;
                #endif
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 posCS = TransformWorldToHClip(posWS);

                if (_OutlineWidth > 0.0001)
                {
                    float4 centerCS = TransformObjectToHClip(_MeshCenterOS.xyz);
                    float2 centerNDC = centerCS.xy / max(centerCS.w, 0.0001);
                    float2 vertNDC = posCS.xy / max(posCS.w, 0.0001);
                    float2 radial2D = vertNDC - centerNDC;
                    float2 rDir = length(radial2D) > 0.0001 ? normalize(radial2D) : float2(0.0, 1.0);
                    float3 viewNormal = TransformWorldToViewDir(normalWS, true);
                    float2 nDir = length(viewNormal.xy) > 0.0001 ? normalize(viewNormal.xy) : rDir;
                    float2 dir2D = normalize(lerp(nDir, rDir, 0.65) + float2(1e-5, 1e-5));
                    float aspect = _ScreenParams.y / max(_ScreenParams.x, 1.0);
                    float pxClip = clamp(_OutlineWidth, 0.035, 0.12) * 0.22;
                    posCS.xy += float2(dir2D.x * aspect, dir2D.y) * (pxClip * posCS.w);
                }

                output.positionWS = posWS;
                output.normalWS = normalWS;
                output.positionCS = posCS;
                output.eyeDepth = -TransformWorldToView(posWS).z;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                clip(_ProximityAlpha - 0.005);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawSceneDepth = SampleSceneDepth(screenUV);
                if (IsSkyDepth(rawSceneDepth))
                    discard;

                float3 scenePosWS = ComputeWorldSpacePosition(screenUV, rawSceneDepth, UNITY_MATRIX_I_VP);
                float sceneEyeDepth = -TransformWorldToView(scenePosWS).z;

                // Discard pixels that are NOT strictly behind an opaque occluder
                clip(input.eyeDepth - sceneEyeDepth - _DepthBias);

                float alpha;
                half3 rgb;
                if (_OutlineWidth > 0.0001)
                {
                    rgb = _GlowColor.rgb * 1.5h;
                    alpha = _ProximityAlpha;
                }
                else
                {
                    float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                    float ndotv = saturate(abs(dot(normalize(input.normalWS), viewDirWS)));
                    float rim = pow(1.0 - ndotv, 1.8);
                    rgb = _GlowColor.rgb * 1.15h;
                    alpha = saturate(0.12 + rim * 0.35) * _ProximityAlpha;
                }

                return half4(rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
