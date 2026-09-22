Shader "Landsong/HologramTriangleWireframe"
{
    Properties
    {
        [Header(Wireframe)]
        [HDR] _LineColor ("Line Color", Color) = (0.0, 2.5, 3.5, 0.9)
        [Range(0.25, 5)] _LineWidth ("Line Width (Pixels)", Float) = 1.25
        [Range(0.1, 3)] _LineSmoothness ("Line Smoothness", Float) = 0.75

        [Header(Vertices)]
        [HDR] _VertexColor ("Vertex Color", Color) = (0.4, 4.0, 5.0, 1.0)
        [Range(0, 16)] _VertexSize ("Vertex Size (Pixels)", Float) = 3.0
        [Range(0, 3)] _VertexIntensity ("Vertex Intensity", Float) = 1.0

        [Header(Hologram)]
        [Range(0, 3)] _Brightness ("Brightness", Float) = 1.0
        [Range(0, 2)] _Opacity ("Opacity", Float) = 1.0
        [Range(0, 2)] _ScanIntensity ("Moving Glow Intensity", Float) = 0.25
        _ScanDensity ("Moving Glow Density", Float) = 3.0
        _ScanSpeed ("Moving Glow Speed", Float) = 0.35
        [Range(0, 0.8)] _FlickerAmount ("Flicker Amount", Float) = 0.04

        [Header(Render State)]
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Z Test", Float) = 4
    }

    HLSLINCLUDE
    #pragma multi_compile_instancing
    #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half4 _LineColor;
        half4 _VertexColor;
        float _LineWidth;
        float _LineSmoothness;
        float _VertexSize;
        float _VertexIntensity;
        float _Brightness;
        float _Opacity;
        float _ScanIntensity;
        float _ScanDensity;
        float _ScanSpeed;
        float _FlickerAmount;
    CBUFFER_END

    struct Attributes
    {
        float4 positionOS : POSITION;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct VertexToGeometry
    {
        float4 positionHCS : SV_POSITION;
        float3 positionWS : TEXCOORD0;
    };

    VertexToGeometry Vert(Attributes input)
    {
        // Resolve the instance transform before converting to world/clip space.
        UNITY_SETUP_INSTANCE_ID(input);
        VertexToGeometry output;
        VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
        output.positionHCS = positionInputs.positionCS;
        output.positionWS = positionInputs.positionWS;
        return output;
    }

    float GetFlicker()
    {
        return 1.0 - _FlickerAmount * (0.5 + 0.5 * sin(_Time.y * 23.17));
    }

    float GetMovingGlow(float worldY)
    {
        float wave = 0.5 + 0.5 * sin((worldY * _ScanDensity - _Time.y * _ScanSpeed) * TWO_PI);
        return pow(saturate(wave), 12.0) * _ScanIntensity;
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "TriangleWireframe"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend SrcAlpha One
            Cull Off
            ZWrite Off
            ZTest [_ZTest]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma require geometry
            #pragma only_renderers d3d11 vulkan
            #pragma vertex Vert
            #pragma geometry HologramGeometry
            #pragma fragment HologramFragment

            struct WireVaryings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                noperspective float3 barycentric : TEXCOORD1;
                noperspective float2 pointCoordinate : TEXCOORD2;
                nointerpolation float isPoint : TEXCOORD3;
            };

            void AppendPointVertex(
                VertexToGeometry source,
                float2 corner,
                inout TriangleStream<WireVaryings> stream)
            {
                WireVaryings output;
                float2 pixelOffset = corner * _VertexSize;
                float2 clipOffset = pixelOffset * (2.0 / _ScreenParams.xy) * source.positionHCS.w;

                output.positionHCS = source.positionHCS;
                output.positionHCS.xy += clipOffset;
                output.positionWS = source.positionWS;
                output.barycentric = float3(1.0, 1.0, 1.0);
                output.pointCoordinate = corner;
                output.isPoint = 1.0;
                stream.Append(output);
            }

            [maxvertexcount(21)]
            void HologramGeometry(
                triangle VertexToGeometry input[3],
                inout TriangleStream<WireVaryings> stream)
            {
                WireVaryings output;

                // Emit the original triangle once for the wireframe.
                [unroll]
                for (uint index = 0; index < 3; index++)
                {
                    output.positionHCS = input[index].positionHCS;
                    output.positionWS = input[index].positionWS;
                    output.barycentric = index == 0
                        ? float3(1.0, 0.0, 0.0)
                        : (index == 1
                            ? float3(0.0, 1.0, 0.0)
                            : float3(0.0, 0.0, 1.0));
                    output.pointCoordinate = float2(2.0, 2.0);
                    output.isPoint = 0.0;
                    stream.Append(output);
                }
                stream.RestartStrip();

                // Emit a screen-space disc at each triangle corner.
                [unroll]
                for (uint pointIndex = 0; pointIndex < 3; pointIndex++)
                {
                    AppendPointVertex(input[pointIndex], float2(-1.0, -1.0), stream);
                    AppendPointVertex(input[pointIndex], float2(-1.0,  1.0), stream);
                    AppendPointVertex(input[pointIndex], float2( 1.0, -1.0), stream);
                    stream.RestartStrip();

                    AppendPointVertex(input[pointIndex], float2( 1.0, -1.0), stream);
                    AppendPointVertex(input[pointIndex], float2(-1.0,  1.0), stream);
                    AppendPointVertex(input[pointIndex], float2( 1.0,  1.0), stream);
                    stream.RestartStrip();
                }
            }

            float GetWireMask(float3 barycentric)
            {
                float3 pixelWidth = max(fwidth(barycentric), 0.00001);
                float3 inner = pixelWidth * _LineWidth;
                float3 outer = inner + pixelWidth * _LineSmoothness;
                float3 centerMask = smoothstep(inner, outer, barycentric);
                return 1.0 - min(centerMask.x, min(centerMask.y, centerMask.z));
            }

            half4 HologramFragment(WireVaryings input) : SV_Target
            {
                if (input.isPoint > 0.5)
                {
                    float radius = length(input.pointCoordinate);
                    float edgeWidth = max(fwidth(radius), 0.00001);
                    float pointMask = 1.0 - smoothstep(1.0 - edgeWidth, 1.0, radius);
                    pointMask *= step(0.001, _VertexSize) * step(0.001, _VertexIntensity);
                    clip(pointMask - 0.001);

                    half3 pointColor = _VertexColor.rgb * _VertexIntensity
                        * (_Brightness + GetMovingGlow(input.positionWS.y));
                    half pointAlpha = saturate(
                        _VertexColor.a * _Opacity * pointMask * GetFlicker());
                    return half4(pointColor, pointAlpha);
                }

                float wireMask = GetWireMask(input.barycentric);
                clip(wireMask - 0.001);

                half3 color = _LineColor.rgb * (_Brightness + GetMovingGlow(input.positionWS.y));
                half alpha = saturate(_LineColor.a * _Opacity * wireMask * GetFlicker());
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
