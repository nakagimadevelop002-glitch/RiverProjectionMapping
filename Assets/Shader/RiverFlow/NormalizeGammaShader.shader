Shader "RiverFlow/NormalizeGamma"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _P99 ("99th Percentile", Float) = 1.0
        _Gamma ("Gamma", Float) = 0.72
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
            float _P99;
            float _Gamma;

            half4 frag(Varyings input) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoord);

                // 正規化（0-1にクランプ）
                float normalized = saturate(col.r / _P99);

                // ガンマ補正
                float corrected = pow(normalized, _Gamma);

                // グレースケール出力
                return half4(corrected, corrected, corrected, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
