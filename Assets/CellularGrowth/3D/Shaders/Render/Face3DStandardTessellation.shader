Shader "CellularGrowth/3D/FaceStandardTessellation"
{

	Properties
	{
		_Color ("Color", Color) = (1, 1, 1, 1)
		_Emission ("Emission", Color) = (0, 0, 0, 0)

    [Space] _Glossiness ("Smoothness", Range(0, 1)) = 0.5
    [Gamma] _Metallic ("Metallic", Range(0, 1)) = 0

    [KeywordEnum(Vertex, Flat)] _Normal ("Normal", Int) = 0
    [KeywordEnum(None, Front, Back)] _Cull ("Cull", Int) = 2
    [KeywordEnum(Normal, Debug)] _Debug ("Debug", Int) = 0

    _TessEdgeMinLength ("Tessellation edge min", Range(0.1, 5.0)) = 0.1
    _TessEdgeMaxLength ("Tessellation edge max", Range(0.1, 20.0)) = 5
    _TessFactor ("Tessellation Factor", Range(1.0, 10.0)) = 5
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

			#pragma vertex vert
			#pragma fragment frag
			#pragma hull hull
			#pragma domain domain
			#pragma multi_compile_instancing
			#pragma instancing_options procedural:setup
      #pragma multi_compile_prepassfinal noshadowmask nodynlightmap nodirlightmap nolightmap
			#include "./Face3DStandardTessellation.cginc"

			ENDCG
		}

		Pass
		{
      Cull [_Cull]
      Tags { "LightMode" = "ShadowCaster" }

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma hull hull
			#pragma domain domain
			#pragma multi_compile_instancing
			#pragma instancing_options procedural:setup
      #pragma multi_compile_shadowcaster noshadowmask nodylightmap nodirlightmap nolightmap
      #define UNITY_PASS_SHADOWCASTER
			#include "./Face3DStandardTessellation.cginc"

			ENDCG
		}



	}
}
