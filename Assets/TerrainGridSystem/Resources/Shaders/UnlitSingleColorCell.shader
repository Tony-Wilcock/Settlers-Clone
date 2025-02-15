Shader "Terrain Grid System/Unlit Single Color Cell Thin Line" {
 
Properties {
    _Color ("Color", Color) = (1,1,1,1)
    _Offset ("Depth Offset", float) = -0.01  
    _NearClip ("Near Clip", Range(0, 1000.0)) = 25.0
    _FallOff ("FallOff", Range(1, 1000.0)) = 50.0
    _FarFadeDistance ("Far Fade Distance", Float) = 10000
    _FarFadeFallOff ("Far Fade FallOff", Range(1, 1000.0)) = 50.0
    _CircularFadeDistance ("Circular Fade Distance", Float) = 250000
    _CircularFadeFallOff ("Circular Fade FallOff", Float) = 50.0
    _ZWrite("ZWrite", Int) = 0
    _SrcBlend("Src Blend", Int) = 5
    _DstBlend("Dst Blend", Int) = 10
	_StencilComp("Stencil Comp", Int) = 6 // not equal
	_StencilOp("Stencil Op", Int) = 2 // replace
}
 
SubShader {
    Tags {
      "Queue"="Geometry+136" // microsplat renders in Geometry+100 so we force the grid to render afterwards
      "RenderType"="Opaque"
  	}
    Blend [_SrcBlend] [_DstBlend]
  	ZWrite [_ZWrite]
    Stencil {
        Ref 2
        Comp [_StencilComp]
        Pass [_StencilOp]
    }
    Pass {
    	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag	
		#pragma multi_compile __ TGS_NEAR_CLIP_FADE
		#pragma multi_compile __ TGS_FAR_FADE
		#pragma multi_compile __ TGS_CIRCULAR_FADE
		#include "UnityCG.cginc"			
		#include "TGSCommon.cginc"

		struct AppData {
			float4 vertex : POSITION;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		struct v2f {
			fixed4 pos : SV_POSITION;	
			fixed4 color: COLOR;
			UNITY_VERTEX_OUTPUT_STEREO
		};
		
		v2f vert(AppData v) {
			v2f o;
			UNITY_SETUP_INSTANCE_ID(v);
			UNITY_INITIALIZE_OUTPUT(v2f, o);
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
			
			o.pos = UnityObjectToClipPos(v.vertex);
			#if UNITY_REVERSED_Z
				o.pos.z -= _Offset;
			#else
				o.pos.z += _Offset;
			#endif
			APPLY_FADE(_Color, v.vertex, o.pos, o.color)
			return o;									
		}
		
		fixed4 frag(v2f i) : SV_Target {
			return i.color;
		}
			
		ENDCG
    }
    
}
}
