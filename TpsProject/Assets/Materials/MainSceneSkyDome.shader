Shader "TPS/Main Scene Sky Dome"
{
    Properties
    {
        _ZenithColor ("Zenith Color", Color) = (0.055, 0.18, 0.42, 1)
        _HorizonColor ("Horizon Color", Color) = (0.48, 0.72, 0.92, 1)
        _GroundColor ("Ground Color", Color) = (0.14, 0.20, 0.28, 1)
        _HorizonSharpness ("Horizon Sharpness", Range(0.25, 4)) = 1.35
        _Exposure ("Exposure", Range(0, 4)) = 1.1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 directionOS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                half _HorizonSharpness;
                half _Exposure;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.directionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half height = normalize(input.directionOS).y;
                half skyBlend = pow(saturate(height), _HorizonSharpness);
                half groundBlend = saturate(-height * 3.0h);
                half3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, skyBlend);
                half3 color = lerp(sky, _GroundColor.rgb, groundBlend) * _Exposure;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
