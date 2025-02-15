Shader "Terrain Grid System/Unlit Surface Texture Overlay" {
 
Properties {
    _Color ("Color", Color) = (1,1,1)
    _MainTex ("Texture", 2D) = "black" {}
    _Offset ("Depth Offset", Int) = -1
    _ZWrite("ZWrite", Int) = 0
	_AlphaTestThreshold("Alpha Test", Range(0,1)) = 0.5
	[HideInInspector] _ZTest("ZTest", Int) = 8 // always; needed by CellGetOverlayMode API
}
 
SubShader {
    Tags {
        "Queue"="Geometry+201"
        "RenderType"="Opaque"
    }
    Blend SrcAlpha OneMinusSrcAlpha
   	ZTest Always
	ZWrite [_ZWrite]
   	Offset [_Offset], [_Offset]
    Pass {
    	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag	
		#pragma multi_compile_local _ TGS_ALPHA_CLIPPING
		#include "UnityCG.cginc"			

		sampler2D _MainTex;
		fixed4 _Color;
		fixed _AlphaTestThreshold;

		struct AppData {
			float4 vertex : POSITION;
			float2 uv     : TEXCOORD0;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		struct v2f {
			float4 pos : SV_POSITION;	
			float2 uv  : TEXCOORD0;
			UNITY_VERTEX_OUTPUT_STEREO
		};
		
		//Vertex shader
		v2f vert(AppData v) {
			v2f o;							
			UNITY_SETUP_INSTANCE_ID(v);
			UNITY_INITIALIZE_OUTPUT(v2f, o);
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

			o.pos = UnityObjectToClipPos(v.vertex);
			o.uv  = v.uv;
			return o;									
		}
		
		fixed4 frag(v2f i) : SV_Target {
			fixed4 color = tex2D(_MainTex, i.uv);
			#if TGS_ALPHA_CLIPPING
				clip(color.a - _AlphaTestThreshold);
			#endif
			return color * _Color;
		}
			
		ENDCG
    }
}
}