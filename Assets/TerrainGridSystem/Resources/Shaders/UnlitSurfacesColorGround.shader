Shader "Terrain Grid System/Unlit Surface Color Ground" {
 
Properties {
    _MainTex ("Texture", 2D) = "black" {}
    _Color ("Color", Color) = (1,1,1,1)
    _Offset ("Depth Offset", Int) = -1
    _ZWrite("ZWrite", Int) = 0
    _SrcBlend("Src Blend", Int) = 5
    _DstBlend("Dst Blend", Int) = 10
    _StencilComp("Stencil Comp", Int) = 8
	_StencilRef("Stencil Ref", Int) = 8
    _StencilOp("Stencil Op", Int) = 0
}
 
SubShader {
    Tags {
      "Queue"="Geometry+101"
      "RenderType"="Transparent"
  	}
  	Offset [_Offset], [_Offset]
  	Blend [_SrcBlend] [_DstBlend]
  	ZWrite [_ZWrite]
	Stencil {
		Ref [_StencilRef]
		Comp [_StencilComp]
		Pass [_StencilOp]
		ReadMask [_StencilRef]
		WriteMask [_StencilRef]
    }

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
