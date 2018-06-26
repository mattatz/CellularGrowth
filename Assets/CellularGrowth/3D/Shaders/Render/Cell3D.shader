Shader "CellularGrowth/3D/Cell"
{

	Properties
	{
    _Base ("Base", Color) = (0.5, 0.5, 0.5, 1)
		_Highlight ("Highlight", Color) = (1, 1, 1, 1)
    _Size ("Size", Range(0.0, 1.0)) = 0.75
	}

	SubShader
	{
		Tags { "RenderType"="Opaque" "Queue"="Geometry" }
		LOD 100

		Pass
		{
      Cull Back

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#pragma instancing_options procedural:setup
			
			#include "UnityCG.cginc"
			#include "Assets/Common/Shaders/PhotoshopMath.cginc"
			#include "../Common/Random.cginc"
			#include "../Common/Cell.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
        float3 normal : NORMAL;
        UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 position : SV_POSITION;
        float2 uv : TEXCOORD0;
        float3 normal : NORMAL;
        float4 color : COLOR;
        UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			StructuredBuffer<Cell> _Cells;
			float4 _Base, _Highlight;
      float _Size;

      float4x4 _World2Local, _Local2World;

      void setup() {
        unity_WorldToObject = _World2Local;
        unity_ObjectToWorld = _Local2World;
      }

			v2f vert (appdata IN, uint iid : SV_InstanceID)
			{
				v2f OUT;
        UNITY_SETUP_INSTANCE_ID(IN);
        UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

        Cell cell = _Cells[iid];
        float4 vertex = IN.vertex * cell.alive * cell.radius * 2.0 * _Size + float4(cell.position.xyz, 0);
        OUT.position = UnityObjectToClipPos(vertex);
        OUT.uv = IN.uv - 0.5;
        OUT.normal = IN.normal;

        // float u = (cell.links * 0.2);
        // float4 grad = float4(hsv2rgb(float3(fmod(u, 1.0), 1, 1)), 1);
        // OUT.color = grad;
        // float vl = saturate(length(cell.velocity));
        OUT.color = lerp(_Base, _Highlight, saturate(cell.stress));

				return OUT;
			}

			fixed4 frag (v2f IN) : SV_Target
			{
        // float4 color = float4((IN.normal + 1.0) * 0.5, 1.0);
				return IN.color;
			}

			ENDCG
		}
	}
}
