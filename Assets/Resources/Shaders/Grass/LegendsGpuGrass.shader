Shader "Legends/Procedural GPU Grass"
{
    Properties
    {
        _BaseMap ("Grass Atlas", 2D) = "white" {}
        _BottomColor ("Bottom Color", Color) = (0.09, 0.24, 0.07, 1)
        _TopColor ("Top Color", Color) = (0.42, 0.72, 0.20, 1)
        _Ambient ("Ambient", Range(0, 1)) = 0.62
        _WindStrength ("Wind Strength", Range(0, 2)) = 0.23
        _WindSpeed ("Wind Speed", Range(0, 8)) = 1.25
        _WindScale ("Wind Scale", Range(0.01, 1)) = 0.13
        _Cutoff ("Cutoff", Range(0, 1)) = 0.05
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
                float _Ambient;
                float _WindStrength;
                float _WindSpeed;
                float _WindScale;
                float _Cutoff;
            CBUFFER_END

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionOS = input.positionOS;
                float3 basePositionWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float heightMask = saturate(max(input.uv.y, positionOS.y * 0.85));
                float windPhase = _Time.y * _WindSpeed +
                                  basePositionWS.x * _WindScale +
                                  basePositionWS.z * (_WindScale * 1.37);
                float bendMask = heightMask * heightMask;
                positionOS.x += sin(windPhase) * _WindStrength * bendMask;
                positionOS.z += cos(windPhase * 0.73) * _WindStrength * 0.35 * bendMask;

                output.positionWS = TransformObjectToWorld(positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                output.heightMask = heightMask;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float4 atlas = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float maxChannel = max(atlas.r, max(atlas.g, atlas.b));
                float colorMask = smoothstep(0.06, 0.16, maxChannel);
                clip(min(atlas.a, colorMask) - _Cutoff);

                return float4(atlas.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
