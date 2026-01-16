Shader "RiverFlow/Blur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            float2 _Direction;

            half4 frag(Varyings input) : SV_Target
            {
                // 1-2-1カーネルBlur
                float2 offset = _MainTex_TexelSize.xy * _Direction;

                half4 c0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoord - offset);
                half4 c1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoord);
                half4 c2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoord + offset);

                // (1 * c0 + 2 * c1 + 1 * c2) / 4
                return (c0 + 2.0 * c1 + c2) * 0.25;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
