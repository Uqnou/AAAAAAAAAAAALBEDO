Shader "ALBEDO/StainedGlassVitral"
{
    Properties
    {
        _BaseColor ("Base Filter Color", Color) = (0.1, 0.9, 0.3, 1.0)
        _SecondaryColor ("Secondary Splitter Color", Color) = (1.0, 0.15, 0.15, 1.0)
        _UseDualColor ("Use Dual Splitter Colors", Float) = 0.0
        [HDR] _EmissionColor ("Emission Color", Color) = (0.1, 0.9, 0.3, 1.0)
        _EmissionStrength ("Emission Strength", Range(0.0, 6.0)) = 1.15
        _Opacity ("Glass Opacity", Range(0.25, 1.0)) = 0.90

        [Header(Stained Glass Mosaic)]
        _LeadColor ("Lead Frame Color", Color) = (0.002, 0.002, 0.003, 1.0)
        _LeadThickness ("Lead Line Thickness (Meters)", Range(0.008, 0.12)) = 0.032
        _CellScale ("Radial Ring Size (Meters)", Range(0.2, 2.5)) = 0.65
        _AngularSectors ("Base Angular Sectors", Range(6.0, 28.0)) = 12.0
        _SpiralTwist ("Spiral Twist", Range(-1.5, 1.5)) = 0.25
        _CellSlant ("Tile Subdivide Ratio", Range(0.0, 1.0)) = 0.58

        [Header(Tone Variation and Texture)]
        _ToneVariation ("Shade Luminance Variation", Range(0.0, 1.0)) = 0.92
        _HueVariation ("Hue Shift Variation", Range(0.0, 0.22)) = 0.025
        _GlassRipple ("Hammered Glass Texture", Range(0.0, 1.0)) = 0.72
        _FresnelPower ("Fresnel Edge Glow", Range(0.5, 8.0)) = 2.6
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "StainedGlassPass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define ALBEDO_MAX_BEAMS 24
            int _AlbedoBeamCount;
            float4 _AlbedoBeamStarts[ALBEDO_MAX_BEAMS];
            float4 _AlbedoBeamDirs[ALBEDO_MAX_BEAMS];
            float4 _AlbedoBeamColors[ALBEDO_MAX_BEAMS];
            float4 _AlbedoBeamParams[ALBEDO_MAX_BEAMS];

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 posScaledOS : TEXCOORD2;
                float3 normalOS    : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SecondaryColor;
                float _UseDualColor;
                half4 _EmissionColor;
                float _EmissionStrength;
                float _Opacity;
                half4 _LeadColor;
                float _LeadThickness;
                float _CellScale;
                float _AngularSectors;
                float _SpiralTwist;
                float _CellSlant;
                float _ToneVariation;
                float _HueVariation;
                float _GlassRipple;
                float _FresnelPower;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float3 RgbToHsv(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-5;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            float3 HsvToRgb(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            float EvaluateBeamBacklight(float3 posWS, out float3 outBeamColor)
            {
                int count = min(_AlbedoBeamCount, ALBEDO_MAX_BEAMS);
                float glow = 0.0;
                float3 colAccum = float3(0.0, 0.0, 0.0);
                float weightSum = 0.0;
                for (int i = 0; i < count; i++)
                {
                    float strength = _AlbedoBeamParams[i].z;
                    if (strength <= 0.02) continue;
                    float3 origin = _AlbedoBeamStarts[i].xyz;
                    float topR = _AlbedoBeamStarts[i].w;
                    float3 dir = _AlbedoBeamDirs[i].xyz;
                    float hitLen = max(_AlbedoBeamDirs[i].w, 0.01);
                    float botR = _AlbedoBeamColors[i].w;
                    float fullLen = max(_AlbedoBeamParams[i].x, 0.1);
                    float3 toP = posWS - origin;
                    float tProj = dot(toP, dir);
                    if (tProj < -0.5 || tProj > hitLen + 0.8) continue;
                    float coneR = max(0.2, lerp(topR, botR, saturate(tProj / fullLen))) + 0.35;
                    float rDist = length(toP - dir * tProj);
                    float mask = 1.0 - smoothstep(coneR * 0.4, coneR, rDist);
                    float w = mask * saturate(strength);
                    glow = max(glow, w);
                    colAccum += _AlbedoBeamColors[i].rgb * w;
                    weightSum += w;
                }
                outBeamColor = weightSum > 0.001 ? saturate(colAccum / weightSum) : float3(1.0, 1.0, 1.0);
                return glow;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vtx = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS);
                output.positionCS = vtx.positionCS;
                output.positionWS = vtx.positionWS;
                output.normalWS = nrm.normalWS;

                float3 worldScale = float3(
                    length(float3(UNITY_MATRIX_M[0][0], UNITY_MATRIX_M[1][0], UNITY_MATRIX_M[2][0])),
                    length(float3(UNITY_MATRIX_M[0][1], UNITY_MATRIX_M[1][1], UNITY_MATRIX_M[2][1])),
                    length(float3(UNITY_MATRIX_M[0][2], UNITY_MATRIX_M[1][2], UNITY_MATRIX_M[2][2]))
                );
                output.posScaledOS = input.positionOS.xyz * worldScale;
                output.normalOS = input.normalOS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 an = abs(normalize(input.normalOS));
                float2 p;
                if (an.z >= an.x && an.z >= an.y) p = input.posScaledOS.xy;
                else if (an.x >= an.y) p = input.posScaledOS.zy;
                else p = input.posScaledOS.xz;

                float r = length(p);
                float ang = atan2(p.y, p.x);

                // Clean aligned concentric rings + twisted radial spokes + randomized sub-splits inside outer tiles
                float cellStep = max(0.18, _CellScale);
                float hubRadius = cellStep * 0.48;
                float ringCoord = max(0.0, (r - hubRadius) / cellStep);
                float ringIdx = floor(ringCoord);
                float ringF = frac(ringCoord);

                // Grow angular sector count cleanly with ring index
                float ringDivs = max(6.0, round(_AngularSectors * (0.75 + 0.45 * ringIdx)));
                float ringPhase = Hash21(float2(ringIdx, 7.13)) * 6.2831853;
                float twistedAng = ang + (r - hubRadius) * _SpiralTwist + ringPhase;
                float normAng = frac(twistedAng / 6.2831853);
                float sectorCoord = normAng * ringDivs;
                float sectorIdx = floor(sectorCoord);
                float sectorF = frac(sectorCoord);

                // Optional staggered sub-divider inside individual tiles (creates irregular quadradinhos without broken arcs)
                float baseTileHash = Hash21(float2(sectorIdx + ringIdx * 41.0, ringIdx * 13.7));
                float splitAt = lerp(0.32, 0.68, Hash21(float2(sectorIdx * 3.1, ringIdx + 5.2)));
                float hasSubSplit = step(1.0 - _CellSlant, baseTileHash);
                float subTileIdx = hasSubSplit * step(splitAt, ringF);
                float distToSubSplitM = hasSubSplit > 0.5 ? abs(ringF - splitAt) * cellStep : 99.0;

                bool isCenterHub = (r < hubRadius);
                float2 cellId = isCenterHub ? float2(99.0, 99.0) : float2(sectorIdx + ringIdx * 53.0, ringIdx * 2.0 + subTileIdx);

                // Distance in world meters to ALL seams so every boundary gets a crisp lead line
                float arcMetersPerSector = (6.2831853 * max(r, hubRadius)) / ringDivs;
                float distToSpokeM = min(sectorF, 1.0 - sectorF) * arcMetersPerSector;
                float distToRingM  = min(ringF, 1.0 - ringF) * cellStep;
                float distToHubM   = abs(r - hubRadius);
                float minEdgeM = isCenterHub ? distToHubM : min(min(min(distToSpokeM, distToRingM), distToHubM), distToSubSplitM);

                // Crisp jet-black lead line mask
                float aa = max(fwidth(minEdgeM), 0.0025);
                float leadMask = 1.0 - smoothstep(_LeadThickness - aa, _LeadThickness + aa, minEdgeM);

                // Evaluate incoming beam illumination and reflected color
                float3 beamCol;
                float beamGlow = EvaluateBeamBacklight(input.positionWS, beamCol);

                // Select base tint (DualSplitter alternates between Color A & Color B)
                float hHue = Hash21(cellId + 1.13);
                float hSat = Hash21(cellId + 7.41);
                float hVal = Hash21(cellId + 19.73);
                float useSec = (_UseDualColor > 0.5) ? step(0.5, Hash21(cellId * 1.37 + 5.9)) : 0.0;
                float3 rawTint = lerp(_BaseColor.rgb, _SecondaryColor.rgb, useSec);

                // Dynamic color reflection when uncolored/neutral glass receives a colored beam
                float3 preHsv = RgbToHsv(max(rawTint, float3(0.002, 0.002, 0.002)));
                if (preHsv.y < 0.14 && beamGlow > 0.02)
                {
                    rawTint = lerp(rawTint, beamCol, saturate(beamGlow * 1.4));
                }

                // Vary hue, saturation and luminance per tile within the reflected/filtered color family
                float3 hsv = RgbToHsv(max(rawTint, float3(0.002, 0.002, 0.002)));
                float satBase = max(hsv.y, 0.02);
                hsv.x = frac(hsv.x + (hHue - 0.5) * 2.0 * _HueVariation);
                hsv.y = saturate(satBase * lerp(1.0, 0.93 + 0.07 * hSat, _ToneVariation));
                float toneCurve = lerp(0.10, 0.92, pow(hVal, 1.2));
                float valMult = lerp(1.0, toneCurve, _ToneVariation);

                // Internal watercolor gradient + hammered crystalline ripple + darkened bevel near lead
                float tileGrad = lerp(0.84, 1.14, saturate(sectorF * 0.55 + (1.0 - ringF) * 0.45));
                float n1 = ValueNoise(p * 14.0 + cellId * 3.1);
                float n2 = ValueNoise(p * 38.0 - cellId * 5.7);
                float crackle = ValueNoise(p * 74.0 + cellId * 11.3);
                float glassTex = lerp(1.0, 0.72 + 0.54 * (n1 * 0.5 + n2 * 0.35 + crackle * 0.15), _GlassRipple);
                float edgeShade = smoothstep(0.0, _LeadThickness * 3.8, minEdgeM);

                float3 tileColor = HsvToRgb(hsv) * valMult * tileGrad * glassTex * lerp(0.62, 1.06, edgeShade);

                // Fresnel edge highlight + beam transmission backlight (kept inside jewel tone range without pink/white blowout)
                float3 nWS = normalize(input.normalWS);
                float3 vWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(1.0 - saturate(abs(dot(nWS, vWS))), _FresnelPower);

                float3 glassLit = tileColor * (0.78 + _EmissionStrength * (0.14 + 0.32 * fresnel))
                                + tileColor * beamCol * beamGlow * (1.15 + _EmissionStrength * 0.45);

                float3 finalRgb = lerp(glassLit, _LeadColor.rgb, leadMask);
                float glassAlpha = saturate(_Opacity * lerp(0.82, 0.96, 1.0 - hVal * 0.30) + fresnel * 0.14 + beamGlow * 0.12);
                float finalAlpha = lerp(glassAlpha, 1.0, leadMask);

                return half4(finalRgb, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
