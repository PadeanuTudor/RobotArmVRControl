Shader "Custom/PointCloudShader"
{
    // Renders a point cloud from a ComputeBuffer filled by ZedPointCloud.cs
    // Each point: float3 xyz (millimeters) + float packed_color (BGRA bytes)
    // The shader converts mm to Unity units and positions each point as a
    // screen-space quad (billboard) for visibility at any viewing angle.

    Properties
    {
        _PointSize  ("Point Size (world units)", Float) = 0.005
        _MmToUnits  ("MM to Unity Units scale", Float)  = 0.001
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue"      = "Geometry"
        }

        Pass
        {
            Cull Off

            CGPROGRAM
            #pragma vertex   vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma target   4.0

            #include "UnityCG.cginc"

            // ----------------------------------------------------------------
            // Data from ZedPointCloud.cs
            // ----------------------------------------------------------------

            struct PointData
            {
                float x;
                float y;
                float z;
                float color; // packed BGRA as float bytes
            };

            StructuredBuffer<PointData> _PointBuffer;
            float  _PointSize;
            float  _MmToUnits;
            float4x4 _LocalToWorld;

            // ----------------------------------------------------------------
            // Vertex stage: read from buffer, convert to world space
            // ----------------------------------------------------------------

            struct v2g
            {
                float4 worldPos : TEXCOORD0;
                float4 color    : COLOR;
            };

            v2g vert(uint id : SV_VertexID)
            {
                v2g o;

                PointData p = _PointBuffer[id];

                // Convert mm → Unity units and apply coordinate transform
                // ZED coordinate system: X right, Y up, Z forward (right-handed Y-up)
                // Unity coordinate system: X right, Y up, Z forward — matches directly
                float3 localPos = float3(p.x, p.y, p.z) * _MmToUnits;

                // Apply the object's local-to-world transform so you can
                // position/rotate the point cloud GameObject in the scene
                o.worldPos = mul(_LocalToWorld, float4(localPos, 1.0));

                // Unpack color from float bytes (packed as BGRA)
                uint colorBits = asuint(p.color);
                float b = ((colorBits >>  0) & 0xFF) / 255.0;
                float g = ((colorBits >>  8) & 0xFF) / 255.0;
                float r = ((colorBits >> 16) & 0xFF) / 255.0;
                o.color = float4(r, g, b, 1.0);

                return o;
            }

            // ----------------------------------------------------------------
            // Geometry stage: expand each point to a screen-space quad
            // so points are visible from any angle without z-fighting
            // ----------------------------------------------------------------

            struct g2f
            {
                float4 pos   : SV_POSITION;
                float4 color : COLOR;
                float2 uv    : TEXCOORD0;
            };

            [maxvertexcount(4)]
            void geom(point v2g input[1], inout TriangleStream<g2f> stream)
            {
                float4 worldPos = input[0].worldPos;
                float4 color    = input[0].color;

                // Get camera right and up vectors for billboarding
                float3 camRight = normalize(float3(
                    UNITY_MATRIX_V[0][0],
                    UNITY_MATRIX_V[1][0],
                    UNITY_MATRIX_V[2][0]
                ));
                float3 camUp = normalize(float3(
                    UNITY_MATRIX_V[0][1],
                    UNITY_MATRIX_V[1][1],
                    UNITY_MATRIX_V[2][1]
                ));

                float half = _PointSize * 0.5;

                // Four corners of the billboard quad
                float3 corners[4];
                corners[0] = worldPos.xyz + (-camRight - camUp) * half;
                corners[1] = worldPos.xyz + ( camRight - camUp) * half;
                corners[2] = worldPos.xyz + (-camRight + camUp) * half;
                corners[3] = worldPos.xyz + ( camRight + camUp) * half;

                float2 uvs[4] = {
                    float2(0, 0),
                    float2(1, 0),
                    float2(0, 1),
                    float2(1, 1)
                };

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    g2f o;
                    o.pos   = mul(UNITY_MATRIX_VP, float4(corners[i], 1.0));
                    o.color = color;
                    o.uv    = uvs[i];
                    stream.Append(o);
                }
            }

            // ----------------------------------------------------------------
            // Fragment stage: circular point shape with soft edge
            // ----------------------------------------------------------------

            fixed4 frag(g2f i) : SV_Target
            {
                // Make points circular with soft falloff
                float2 uv  = i.uv * 2.0 - 1.0; // -1 to 1
                float  dist = dot(uv, uv);       // distance from center squared
                clip(1.0 - dist);                // discard outside circle
                return i.color;
            }

            ENDCG
        }
    }
}
