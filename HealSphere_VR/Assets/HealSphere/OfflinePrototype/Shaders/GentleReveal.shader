Shader "HealSphere/Offline Gentle Reveal"
{
    Properties
    {
        _Tint ("Pastel Tint", Color) = (1,0.8,0.55,1)
        _Reveal ("Reveal", Range(0,1)) = 0
        _BaseMap ("Original Base Color", 2D) = "white" {}
        _FormationMinY ("Formation World Base", Float) = 0
        _FormationInvHeight ("Formation Inverse Height", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" }
        Pass
        {
            Name "GentleReveal"
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Reveal;
                float4 _BaseMap_ST;
                float _FormationMinY;
                float _FormationInvHeight;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // A stable world-space frontier follows the staggered surface arrivals across child meshes.
                float3 cell = floor(input.positionWS * 500.0);
                float noise = frac(sin(dot(cell, float3(12.9898,78.233,37.719))) * 43758.5453);
                float height = saturate((input.positionWS.y - _FormationMinY) * _FormationInvHeight);
                float threshold = 0.06 + 0.80 * saturate(height + (noise - 0.5) * 0.16);
                clip(_Reveal - threshold);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _Tint.rgb;
                half3 normal = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half3 light = SampleSH(normal) + mainLight.color * saturate(dot(normal, mainLight.direction));
                half edge = 1.0h - smoothstep(0.0h, 0.065h, abs(_Reveal - threshold));
                half glowFade = 1.0h - smoothstep(0.84h, 1.0h, _Reveal);
                return half4(albedo * light + half3(0.32,0.22,0.10) * edge * glowFade, 1);
            }
            ENDHLSL
        }
    }
}
