Shader "CellularGrowth/3D/FaceFlatNormal"
{

	Properties
	{
    [KeywordEnum(None, Front, Back)] _Cull ("Cull", Int) = 2
	}

	SubShader
	{
		Tags { "RenderType"="Opaque" "Queue"="Geometry" }
		LOD 100

		Pass
		{
      Cull [_Cull]

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#pragma instancing_options procedural:setup
			
			#include "UnityCG.cginc"
			#include "../Common/Cell.cginc"
			#include "../Common/Edge.cginc"
			#include "../Common/Face.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
        uint vid : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 position : SV_POSITION;
        float3 normal : NORMAL;
        UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			StructuredBuffer<Cell> _Cells;
			StructuredBuffer<Edge> _Edges;
			StructuredBuffer<Face> _Faces;

      float4x4 _World2Local, _Local2World;

      void setup() {
        unity_ObjectToWorld = _Local2World;
        unity_WorldToObject = _World2Local;
      }

			v2f vert (appdata IN, uint iid : SV_InstanceID)
			{
				v2f OUT;
        UNITY_SETUP_INSTANCE_ID(IN);
        UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

        Face f = _Faces[iid];
        Cell c0 = _Cells[f.c0];
        Cell c1 = _Cells[f.c1];
        Cell c2 = _Cells[f.c2];
        float3 position = lerp(c0.position, lerp(c1.position, c2.position, saturate(IN.vid - 1)), saturate(IN.vid));
        position *= f.alive;
        float4 vertex = float4(position, 1);
        OUT.position = UnityObjectToClipPos(vertex);

        float3 d0 = normalize(c1.position - c0.position);
        float3 d1 = normalize(c2.position - c1.position);
        OUT.normal = mul(unity_ObjectToWorld, float4(normalize(cross(d0, d1)), 0)).xyz;
				return OUT;
			}

			fixed4 frag (v2f IN, fixed facing : VFACE) : SV_Target
			{
        float3 normal = normalize(IN.normal * lerp(-1, 1, facing));
				return float4((normal.xyz + 1.0) * 0.5, 1);
			}

			ENDCG
		}
	}
}
