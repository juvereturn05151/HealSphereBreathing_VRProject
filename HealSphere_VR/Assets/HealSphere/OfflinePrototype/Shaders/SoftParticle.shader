Shader "HealSphere/Offline Soft Particle"
{
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            // Premultiplied core + halo in one billboard pass; no bloom or extra lights.
            Blend One OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes input)
            {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz); output.color = input.color; output.uv = input.uv; return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half2 uv = input.uv * 2.0h - 1.0h;
                half radiusSquared = dot(uv, uv);
                half halo = saturate(1.0h - radiusSquared);
                halo = halo * halo * halo * 0.32h;
                half core = saturate(1.0h - radiusSquared * 12.0h);
                core *= core;
                half alpha = saturate(core + halo) * input.color.a;
                half3 glow = input.color.rgb * (core * 1.35h + halo) * input.color.a;
                return half4(glow, alpha);
            }
            ENDHLSL
        }
    }
}
