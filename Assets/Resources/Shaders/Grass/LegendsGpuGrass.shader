Shader "Legends/Procedural GPU Grass"
{
    Properties
    {
        _BaseMap ("Grass Atlas", 2D) = "white" {}
        _BottomColor ("Bottom Color", Color) = (0.24, 0.33, 0.12, 1)
        _TopColor ("Top Color", Color) = (0.58, 0.64, 0.34, 1)
        _Ambient ("Ambient", Range(0, 1)) = 0.92
        _WindStrength ("Wind Strength", Range(0, 2)) = 0.16
        _WindSpeed ("Wind Speed", Range(0, 8)) = 1.25
        _WindScale ("Wind Scale", Range(0.01, 1)) = 0.13
        _WindDirection ("Wind Direction", Vector) = (0.82, 0, 0.57, 0)
        _AtlasColumns ("Atlas Columns", Float) = 1
        _AtlasRows ("Atlas Rows", Float) = 1
        _Cutoff ("Cutoff", Range(0, 1)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Cull Off
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BottomColor;
                float4 _TopColor;
                float4 _WindDirection;
                float _Ambient;
                float _WindStrength;
                float _WindSpeed;
                float _WindScale;
                float _AtlasColumns;
                float _AtlasRows;
                float _Cutoff;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(GrassPerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float4, _GrassTint)
                UNITY_DEFINE_INSTANCED_PROP(float4, _GrassInstanceData)
            UNITY_INSTANCING_BUFFER_END(GrassPerInstance)

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float heightMask : TEXCOORD3;
                float fade : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float2 ResolveAtlasUv(float2 uv, float variant)
            {
                float columns = max(1.0, floor(_AtlasColumns + 0.5));
                float rows = max(1.0, floor(_AtlasRows + 0.5));
                float maxVariant = columns * rows;
                float index = fmod(max(0.0, floor(variant + 0.5)), maxVariant);
                float row = floor(index / columns);
                float column = index - row * columns;
                return (uv + float2(column, row)) / float2(columns, rows);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(GrassPerInstance, _GrassInstanceData);
                float3 positionOS = input.positionOS;
                float3 rootPositionWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float heightMask = saturate(max(input.uv.y, positionOS.y));
                float rootMask = heightMask * heightMask;
                float phase = _Time.y * _WindSpeed +
                              rootPositionWS.x * _WindScale +
                              rootPositionWS.z * (_WindScale * 1.37) +
                              instanceData.z * 6.2831853;
                float gust = sin(phase) + 0.35 * cos(phase * 0.73);
                float2 windDirection = normalize(_WindDirection.xz + float2(0.0001, 0.0002));
                float bend = gust * _WindStrength * rootMask;
                positionOS.xz += windDirection * bend;

                output.positionWS = TransformObjectToWorld(positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.uv = ResolveAtlasUv(input.uv, instanceData.x) * _BaseMap_ST.xy + _BaseMap_ST.zw;
                output.heightMask = heightMask;
                output.fade = saturate(instanceData.y);
                return output;
            }

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float dither = Hash12(floor(input.positionCS.xy));
                clip(input.fade - dither * 0.98);

                float4 atlas = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float bladeHalfWidth = lerp(0.30, 0.018, input.heightMask);
                float bladeMask = 1.0 - smoothstep(bladeHalfWidth * 0.68, bladeHalfWidth, abs(input.uv.x - 0.5));
                clip(bladeMask - _Cutoff);

                float4 grassTint = UNITY_ACCESS_INSTANCED_PROP(GrassPerInstance, _GrassTint);
                float3 heightTint = lerp(_BottomColor.rgb, _TopColor.rgb, input.heightMask);
                float3 tint = lerp(float3(1.0, 1.0, 1.0), max(grassTint.rgb, float3(0.001, 0.001, 0.001)), saturate(grassTint.a));
                float atlasValue = dot(atlas.rgb, float3(0.299, 0.587, 0.114));
                float3 color = heightTint * tint * lerp(0.98, 1.05, saturate(atlasValue));
                float3 lightDir = normalize(float3(-0.42, 0.74, -0.52));
                float nDotL = saturate(dot(normalize(input.normalWS), lightDir) * 0.5 + 0.5);
                float sunlit = lerp(_Ambient, 1.08, nDotL) * lerp(0.96, 1.04, input.heightMask);
                color *= sunlit;
                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
