Shader "Custom/全息"
{
    Properties
    {
        _Color ("Color", Color) = (0.1, 0.4, 1.0, 1.0)
        _PulseSpeed ("Pulse Speed", Range(0.1, 5)) = 1.0
        _MinAlpha ("Min Alpha", Range(0,1)) = 0.2
        _MaxAlpha ("Max Alpha", Range(0,1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _PulseSpeed;
                float _MinAlpha;
                float _MaxAlpha;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float pulse = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed);
                float alpha  = lerp(_MinAlpha, _MaxAlpha, pulse);
                return half4(_Color.rgb, _Color.a * alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
