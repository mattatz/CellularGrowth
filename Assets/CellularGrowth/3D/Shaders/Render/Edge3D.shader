Shader "CellularGrowth/3D/Edge"
{

	Properties
	{
		_Color ("Color", Color) = (1, 1, 1, 1)
	}

	SubShader
	{
		Tags { "RenderType"="Opaque" "Queue"="Transparent" }
		LOD 100

		Pass
		{
			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#pragma instancing_options procedural:setup
			
			#include "UnityCG.cginc"
			#include "../Common/Cell.cginc"
			#include "../Common/Edge.cginc"

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
        UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			StructuredBuffer<Cell> _Cells;
			StructuredBuffer<Edge> _Edges;
			float4 _Color;

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

        Edge e = _Edges[iid];
        Cell ca = _Cells[e.a];
        Cell cb = _Cells[e.b];

        float3 position = lerp(ca.position, cb.position, IN.vid);
        position *= lerp(0, 1, e.alive);

        float4 vertex = float4(position, 1);
        OUT.position = UnityObjectToClipPos(vertex);
				return OUT;
			}

			fixed4 frag (v2f IN) : SV_Target
			{
				return _Color;
			}

			ENDCG
		}
	}
}
