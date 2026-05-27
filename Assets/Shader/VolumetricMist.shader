Shader "Custom/VolumetricFog"
{
    Properties
    {
        _Color("Fog color", Color) = (0.8, 0.85, 0.95, 1)
        _MaxDistance("Max distance", Float) = 60
        _StepSize("Step size", Range(0.1, 5)) = 0.5
        _DensityMultiplier("Density multiplier", Range(0, 10)) = 0.7
        _NoiseOffset("Noise offset", Float) = 1
        _FogNoise("Fog noise", 3D) = "white" {}
        _NoiseTiling("Noise tiling", Float) = 1
        _DensityThreshold("Density threshold", Range(0, 1)) = 0.4
        [HDR]_LightContribution("Light contribution", Color) = (1, 1, 1, 1)
        _LightScattering("Light scattering", Range(0, 1)) = 0.3
        _FogHeight("Fog height", Float) = 6
        _FloorY("Floor Y", Float) = 0
        _HeightPower("Height power", Range(0.5, 4)) = 2

        [Header(Flashlight)]
        [HDR]_FlashlightBeamColor("Flashlight beam color", Color) = (1, 0.9, 0.7, 1)
        _FlashlightScatterStrength("Flashlight scatter strength", Range(0, 20)) = 6

        [Header(Visibility Wall)]
        _VisibilityDistance("Visibility distance", Float) = 20
        _WallColor("Wall color", Color) = (0.55, 0.55, 0.6, 1)
        _WallDensity("Wall density", Range(0, 10)) = 3

        [Header(Ground Bounds)]
        _GroundCenterX("Ground center X", Float) = 0
        _GroundCenterZ("Ground center Z", Float) = 0
        _GroundRadius("Ground radius", Float) = 100

        [Header(Map Edge Wall)]
        _MapRadius("Map radius", Float) = 100
        _MapWallDensity("Map wall density", Range(0, 20)) = 6
        _MapWallGhostRadius("Ghost opening radius", Float) = 8
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _MaxDistance;
                float  _DensityMultiplier;
                float  _StepSize;
                float  _NoiseOffset;
                float  _DensityThreshold;
                float  _NoiseTiling;
                float4 _LightContribution;
                float  _LightScattering;
                float  _FogHeight;
                float  _FloorY;
                float  _HeightPower;
                float4 _FlashlightBeamColor;
                float  _FlashlightScatterStrength;
                float  _VisibilityDistance;
                float4 _WallColor;
                float  _WallDensity;
                float  _GroundCenterX;
                float  _GroundCenterZ;
                float  _GroundRadius;
                float  _MapRadius;
                float  _MapWallDensity;
                float  _MapWallGhostRadius;
            CBUFFER_END

            TEXTURE3D(_FogNoise);
            SAMPLER(sampler_FogNoise);

            TEXTURE2D(_MistTrailMap);
            SAMPLER(sampler_MistTrailMap);
            float _MistTrailWorldSize;
            float _MistTrailOriginX;
            float _MistTrailOriginZ;

            float3 _FlashlightWorldPos;
            float3 _FlashlightWorldDir;
            float  _FlashlightCosHalfAngle;
            float  _FlashlightRange;
            float  _FlashlightEnabled;

            // Geest-posities — gezet door VolumetricMistController elke frame
            float4 _DisplacerPositions[16];
            float  _DisplacerCount;

            // ── Helpers ──────────────────────────────────────────────────────

            float henyey_greenstein(float cosAngle, float scattering)
            {
                float g2 = scattering * scattering;
                return (1.0 - g2) / (4.0 * PI * pow(abs(1.0 + g2 - 2.0 * scattering * cosAngle), 1.5));
            }

            float flashlight_contribution(float3 worldPos)
            {
                if (_FlashlightEnabled < 0.5) return 0;
                float3 toPoint = worldPos - _FlashlightWorldPos;
                float  dist    = length(toPoint);
                if (dist < 0.01 || dist > _FlashlightRange) return 0;
                float cosA = dot(toPoint / dist, _FlashlightWorldDir);
                if (cosA < _FlashlightCosHalfAngle) return 0;
                float angleT = saturate((cosA - _FlashlightCosHalfAngle) / max(1.0 - _FlashlightCosHalfAngle, 0.001));
                float distT  = 1.0 - saturate(dist / _FlashlightRange);
                return angleT * angleT * distT * distT;
            }

            bool within_ground_bounds(float3 worldPos)
            {
                float2 xzOffset = float2(worldPos.x - _GroundCenterX, worldPos.z - _GroundCenterZ);
                return length(xzOffset) <= _GroundRadius;
            }

            // ── Normale mist (noise + trail, alleen binnen zichtbereik) ──────

           float get_fog_density(float3 worldPos, float distFromPlayer)
{
    if (!within_ground_bounds(worldPos)) return 0;
    if (distFromPlayer >= _VisibilityDistance) return 0;

    float height = worldPos.y - _FloorY;
    if (height < 0 || height > _FogHeight) return 0;

    float heightT       = saturate(height / _FogHeight);
    float heightFalloff = pow(1.0 - heightT, _HeightPower);
    if (heightFalloff < 0.001) return 0;

    float4 noise   = _FogNoise.SampleLevel(sampler_FogNoise, worldPos * 0.01 * _NoiseTiling, 0);
    float  density = saturate(dot(noise, noise) - _DensityThreshold) * _DensityMultiplier * heightFalloff;

    float2 trailUV = float2(
        (worldPos.x - _MistTrailOriginX) / _MistTrailWorldSize,
        (worldPos.z - _MistTrailOriginZ) / _MistTrailWorldSize
    );
    float trail = SAMPLE_TEXTURE2D_LOD(_MistTrailMap, sampler_MistTrailMap, trailUV, 0).r;
    density *= trail;

    // Gegarandeerde gradient: altijd iets meer mist hoe verder van de speler,
    // zodat objecten consequent minder zichtbaar worden op afstand
    float distFactor    = saturate(distFromPlayer / _VisibilityDistance);
    float gradientFog   = distFactor * distFactor * 0.15 * heightFalloff;
    density             = max(density, gradientFog);

    return density;
}

// effectiveVis = min(_VisibilityDistance, afstand tot kaartrand langs deze ray)
// zodat de persoonlijke mist-muur stopt op de kaartrand als die dichterbij is.
float get_wall_density(float3 worldPos, float distFromPlayer, float effectiveVis)
{
    if (!within_ground_bounds(worldPos)) return 0;

    float height = worldPos.y - _FloorY;
    if (height < 0 || height > _FogHeight) return 0;

    float heightT = saturate(height / _FogHeight);
    float wallHeightFactor = lerp(0.6, 1.0, heightT);

    float wallStart = effectiveVis * 0.65;

    if (distFromPlayer >= effectiveVis)
        return _WallDensity * 8.0 * wallHeightFactor;
    else if (distFromPlayer >= wallStart)
    {
        float t = saturate((distFromPlayer - wallStart) / max(effectiveVis - wallStart, 0.001));
        return t * t * t * _WallDensity * wallHeightFactor;
    }

    return 0;
}

            // ── Mist buiten de kaartrand ─────────────────────────────────────────
            // Voegt ondoordringbare mist toe voor rays die voorbij de rand komen.
            // Het zichtbaar maken van de rand zelf gebeurt via effectiveVis in get_wall_density.

            float get_map_wall_density(float3 worldPos, float distFromCenter)
            {
                if (distFromCenter <= _MapRadius) return 0;

                float height = worldPos.y - _FloorY;
                if (height < 0 || height > _FogHeight) return 0;

                float heightT = saturate(height / _FogHeight);
                return _MapWallDensity * 8.0 * lerp(0.6, 1.0, heightT);
            }

            // ── Fragment ──────────────────────────────────────────────────────

            half4 frag(Varyings IN) : SV_Target
            {
                float4 col      = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, IN.texcoord);
                float  depth    = SampleSceneDepth(IN.texcoord);
                float3 worldPos = ComputeWorldSpacePosition(IN.texcoord, depth, UNITY_MATRIX_I_VP);

                float3 rayDir    = normalize(worldPos - _WorldSpaceCameraPos);
                float  viewLen   = length(worldPos - _WorldSpaceCameraPos);
                float2 pixCoords = IN.texcoord * _BlitTexture_TexelSize.zw;

                float distLimit     = min(viewLen, _MaxDistance);
                float distTravelled = InterleavedGradientNoise(
                    pixCoords, (int)(_Time.y / max(HALF_EPS, unity_DeltaTime.x))) * _NoiseOffset;

                // Bereken hoe ver de ray reist voordat hij de kaartrand raakt (XZ-vlak).
                // effectiveVis = min(persoonlijke zichtbaarheid, afstand tot kaartrand).
                // Zo stopt de mist-muur precies op de rand, zichtbaar vanuit elke positie.
                float2 camXZ    = float2(_WorldSpaceCameraPos.x - _GroundCenterX,
                                         _WorldSpaceCameraPos.z - _GroundCenterZ);
                float2 dirXZ    = float2(rayDir.x, rayDir.z);
                float  rA       = dot(dirXZ, dirXZ);
                float  rB       = 2.0 * dot(camXZ, dirXZ);
                float  rC       = dot(camXZ, camXZ) - _MapRadius * _MapRadius;
                float  rDisc    = rB * rB - 4.0 * rA * rC;
                float  tEdge    = (rDisc >= 0.0 && rA > 0.0001)
                                    ? (-rB + sqrt(rDisc)) / (2.0 * rA)
                                    : _VisibilityDistance;
                float  effectiveVis = min(_VisibilityDistance, max(tEdge, 0.5));

                float  transmittance = 1.0;
                float3 fogAccum      = float3(0, 0, 0);
                float3 wallAccum     = float3(0, 0, 0);

                while (distTravelled < distLimit)
                {
                    float3 rayPos = _WorldSpaceCameraPos + rayDir * distTravelled;

                    float2 toPlayer       = float2(rayPos.x - _WorldSpaceCameraPos.x, rayPos.z - _WorldSpaceCameraPos.z);
                    float  distFromPlayer = length(toPlayer);

                    float2 toCenter       = float2(rayPos.x - _GroundCenterX, rayPos.z - _GroundCenterZ);
                    float  distFromCenter = length(toCenter);

                    float fogDensity     = get_fog_density(rayPos, distFromPlayer);
                    float wallDensity    = get_wall_density(rayPos, distFromPlayer, effectiveVis);
                    float mapWallDensity = get_map_wall_density(rayPos, distFromCenter);
                    float totalDensity   = fogDensity + wallDensity + mapWallDensity;

                    if (totalDensity > 0)
                    {
                        // Normale mist met belichting
                        if (fogDensity > 0.001)
                        {
                            float3 litFog = _Color.rgb;

                            Light mainLight = GetMainLight(TransformWorldToShadowCoord(rayPos));
                            litFog += mainLight.color * _LightContribution.rgb
                                * henyey_greenstein(dot(rayDir, mainLight.direction), _LightScattering)
                                * mainLight.shadowAttenuation;

                            #if defined(_ADDITIONAL_LIGHTS)
                            uint lightCount = GetAdditionalLightsCount();
                            for (uint i = 0; i < lightCount; i++)
                            {
                                Light addLight = GetAdditionalLight(i, rayPos);
                                if (addLight.distanceAttenuation > 0.001)
                                {
                                    litFog += addLight.color * _LightContribution.rgb
                                        * henyey_greenstein(dot(rayDir, addLight.direction), _LightScattering)
                                        * addLight.distanceAttenuation * 3.0;
                                }
                            }
                            #endif

                            fogAccum += litFog * fogDensity * _StepSize * transmittance;
                        }

                        // Muur (speler-zichtmuur + rand-muur) — puur grijs, geen belichting
                        wallAccum += _WallColor.rgb * (wallDensity + mapWallDensity) * _StepSize * transmittance;

                        // Zaklamp scattering
                        float flashContrib = flashlight_contribution(rayPos);
                        if (flashContrib > 0.001)
                            fogAccum += _FlashlightBeamColor.rgb * flashContrib * fogDensity * _StepSize * _FlashlightScatterStrength * transmittance;

                        transmittance *= exp(-totalDensity * _StepSize);
                        if (transmittance < 0.005) break;
                    }

                    distTravelled += _StepSize;
                }

                // Scène * doorzichtigheid + mist + muur
                float3 result = col.rgb * transmittance + fogAccum + wallAccum;
                return float4(result, col.a);
            }
            ENDHLSL
        }
    }
}