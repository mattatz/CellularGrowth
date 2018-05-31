Shader "CellularGrowth/2D/Membrane"
{

	Properties
	{
		_Color ("Color", Color) = (1, 1, 1, 1)
	}

	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
      Blend SrcAlpha OneMinusSrcAlpha
      Cull Off

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#pragma instancing_options procedural:setup
			
			#include "UnityCG.cginc"
			#include "../Common/MembraneNode.cginc"
			#include "../Common/MembraneEdge.cginc"

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
        float alpha : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			StructuredBuffer<MembraneNode> _Nodes;
			StructuredBuffer<MembraneEdge> _Edges;
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

        MembraneEdge e = _Edges[iid];
        MembraneNode ca = _Nodes[e.a];
        MembraneNode cb = _Nodes[e.b];
        float3 position = lerp(float3(ca.position, 0), float3(cb.position, 0), IN.vid);
        float4 vertex = float4(position, 1);
        OUT.position = UnityObjectToClipPos(vertex);

        OUT.alpha = e.alive;
				return OUT;
			}

			fixed4 frag (v2f IN) : SV_Target
			{
				return _Color * IN.alpha;
			}

			ENDCG
		}
	}
}
