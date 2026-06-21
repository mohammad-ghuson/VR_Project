Shader "Custom/LiquidPaint"
{
    // Phase 1 - Step 3. Hand-written URP lit shader for the liquid surface.
    // All lighting math (Lambert / Blinn-Phong / Fresnel) is written by us.
    // We only include the URP core library to get light data and matrices
    // (the unavoidable rendering platform), not to do the shading logic.
    Properties
    {
        _BaseColor  ("Base Color", Color) = (0.85, 0.10, 0.15, 1)
        _RimColor   ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower   ("Rim Power", Range(0.5, 8)) = 3.0
        _SpecColor  ("Specular Color", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(1, 256)) = 64
        _Ambient    ("Ambient", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _RimColor;
                float  _RimPower;
                float4 _SpecColor;
                float  _Smoothness;
                float  _Ambient;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.normalWS    = nrmInputs.normalWS;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));

                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float3 lightCol = mainLight.color;

                // Lambert diffuse
                float  NdotL   = saturate(dot(N, L));
                float3 diffuse = _BaseColor.rgb * lightCol * NdotL;

                // Ambient fill so the shadowed side is not pure black
                float3 ambient = _BaseColor.rgb * _Ambient;

                // Blinn-Phong specular (wet sheen)
                float3 H        = normalize(L + V);
                float  NdotH    = saturate(dot(N, H));
                float  specTerm = pow(NdotH, _Smoothness) * NdotL;
                float3 specular = _SpecColor.rgb * lightCol * specTerm;

                // Fresnel rim (grazing-angle highlight)
                float  fresnel = pow(1.0 - saturate(dot(N, V)), _RimPower);
                float3 rim     = _RimColor.rgb * fresnel;

                float3 color = ambient + diffuse + specular + rim;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
