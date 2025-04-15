// Basic Unlit shader that displays vertex colors
Shader "Custom/UnlitVertexColor"
{
    Properties
    {
        // No properties needed for basic vertex color display
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // Include standard library
            #include "UnityCG.cginc"

            // Input structure for vertex shader
            struct appdata
            {
                float4 vertex : POSITION; // Vertex position
                float4 color : COLOR;    // Vertex color
            };

            // Output structure for vertex shader (input for fragment shader)
            struct v2f
            {
                float4 vertex : SV_POSITION; // Clip space position
                fixed4 color : COLOR;       // Vertex color passed to fragment shader
            };

            // Vertex Shader
            v2f vert (appdata v)
            {
                v2f o;
                // Transform vertex position to clip space
                o.vertex = UnityObjectToClipPos(v.vertex);
                // Pass vertex color directly to fragment shader
                o.color = v.color;
                return o;
            }

            // Fragment Shader
            fixed4 frag (v2f i) : SV_Target
            {
                // Output the interpolated vertex color
                return i.color;
            }
            ENDCG
        }
    }
}