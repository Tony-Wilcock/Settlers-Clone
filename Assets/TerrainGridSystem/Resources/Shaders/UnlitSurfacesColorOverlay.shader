Shader "Terrain Grid System/Unlit Surface Color Overlay" {
 
Properties {
    _MainTex ("Texture", 2D) = "black" {}
    _Color ("Color", Color) = (1,1,1,1)
    _Offset ("Depth Offset", Int) = -1
    _ZWrite("ZWrite", Int) = 0
	[HideInInspector] _ZTest("ZTest", Int) = 8 // always; needed by CellGetOverlayMode API
}
 
SubShader {
    Tags {
      "Queue"="Geometry+201"
      "RenderType"="Transparent"
  	}
  	Offset [_Offset], [_Offset]
  	Blend SrcAlpha OneMinusSrcAlpha
  	ZWrite [_ZWrite]
  	ZTest Always
    Pass {
    	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag	
		#include "UnityCG.cginc"			

		fixed4 _Color;

		struct AppData {
			float4 vertex : POSITION;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		struct v2f {
			float4 pos : SV_POSITION;	
			UNITY_VERTEX_OUTPUT_STEREO
		};

		//Vertex shader
		v2f vert(AppData v) {
			v2f o;							
			UNITY_SETUP_INSTANCE_ID(v);
			UNITY_INITIALIZE_OUTPUT(v2f, o);
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
			o.pos = UnityObjectToClipPos(v.vertex);
			return o;									
		}
		
		fixed4 frag(v2f i) : SV_Target {
			return _Color;
		}
			
		ENDCG
    }
    }
}
