Shader "RiverFlow/Decay"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Decay ("Decay", Float) = 0.92
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
            float _Decay;

            half4 frag(Varyings input) : SV_Target
            {
                // テクスチャをサンプリングして減衰
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoord);
                return col * _Decay;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
