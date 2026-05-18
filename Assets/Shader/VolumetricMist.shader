Shader "Custom/VolumetricFog"
{
    Properties
    {
        _Color("Color", Color) = (0.8, 0.85, 0.95, 1)
        _MaxDistance("Max distance", float) = 30
        _StepSize("Step size", Range(0.1, 5)) = 0.5
        _DensityMultiplier("Density multiplier", Range(0, 10)) = 0.7
        _NoiseOffset("Noise offset", float) = 1
        _FogNoise("Fog noise", 3D) = "white" {}
        _NoiseTiling("Noise tiling", float) = 1
        _DensityThreshold("Density threshold", Range(0, 1)) = 0.4
        [HDR]_LightContribution("Light contribution", Color) = (1, 1, 1, 1)
        _LightScattering("Light scattering", Range(0, 1)) = 0.3
        _FogHeight("Fog height", float) = 6
        _FloorY("Floor Y", float) = 0
        _HeightPower("Height power", Range(0.5, 4)) = 2
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
            CBUFFER_END

            TEXTURE3D(_FogNoise);
            SAMPLER(sampler_FogNoise);

            TEXTURE2D(_MistTrailMap);
            SAMPLER(sampler_MistTrailMap);
            float _MistTrailWorldSize;
            float _MistTrailOriginX;
            float _MistTrailOriginZ;

            float henyey_greenstein(float cosAngle, float scattering)
            {
                float g2 = scattering * scattering;
                return (1.0 - g2) / (4.0 * PI * pow(abs(1.0 + g2 - 2.0 * scattering * cosAngle), 1.5));
            }

            float get_density(float3 worldPos)
            {
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

                return density;
            }

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

                float  transmittance = 1.0;
                float4 fogCol        = _Color;

                while (distTravelled < distLimit)
                {
                    float3 rayPos = _WorldSpaceCameraPos + rayDir * distTravelled;
                    float  density = get_density(rayPos);

                    if (density > 0)
                    {
                        Light mainLight = GetMainLight(TransformWorldToShadowCoord(rayPos));
                        fogCol.rgb += mainLight.color * _LightContribution.rgb
                            * henyey_greenstein(dot(rayDir, mainLight.direction), _LightScattering)
                            * density * mainLight.shadowAttenuation * _StepSize;

                        #if defined(_ADDITIONAL_LIGHTS)
                        uint lightCount = GetAdditionalLightsCount();
                        for (uint i = 0; i < lightCount; i++)
                        {
                            Light addLight = GetAdditionalLight(i, rayPos);
                            if (addLight.distanceAttenuation > 0.001)
                            {
                                fogCol.rgb += addLight.color * _LightContribution.rgb
                                    * henyey_greenstein(dot(rayDir, addLight.direction), _LightScattering)
                                    * density * addLight.distanceAttenuation * _StepSize * 3.0;
                            }
                        }
                        #endif

                        transmittance *= exp(-density * _StepSize);
                    }

                    distTravelled += _StepSize;
                }

                return lerp(col, fogCol, 1.0 - saturate(transmittance));
            }
            ENDHLSL
        }
    }
}