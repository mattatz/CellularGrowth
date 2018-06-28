Shader "CellularGrowth/3D/FaceStandard"
{

	Properties
	{
		_Color ("Color", Color) = (1, 1, 1, 1)
		_Emission ("Emission", Color) = (0, 0, 0, 0)

    [Space] _Glossiness ("Smoothness", Range(0, 1)) = 0.5
    [Gamma] _Metallic ("Metallic", Range(0, 1)) = 0

    [KeywordEnum(None, Front, Back)] _Cull ("Cull", Int) = 2
    [KeywordEnum(Normal, Debug)] _Debug ("Debug", Int) = 0
	}

  CGINCLUDE

  ENDCG

	SubShader
	{
		Tags { "RenderType"="Opaque" "Queue"="Geometry" }
		LOD 100

		Pass
		{
      Cull [_Cull]
      Tags { "LightMode" = "Deferred" }

			CGPROGRAM

      #pragma target 4.0
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#pragma instancing_options procedural:setup
      #pragma multi_compile_prepassfinal noshadowmask nodynlightmap nodirlightmap nolightmap
			#include "./Face3DStandard.cginc"

			ENDCG
		}

		Pass
		{
      Cull [_Cull]
      Tags { "LightMode" = "ShadowCaster" }

			CGPROGRAM

      #pragma target 4.0
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#pragma instancing_options procedural:setup
      #pragma multi_compile_shadowcaster noshadowmask nodylightmap nodirlightmap nolightmap
      #define UNITY_PASS_SHADOWCASTER
			#include "./Face3DStandard.cginc"

			ENDCG
		}

	}
}
