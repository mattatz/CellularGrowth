Shader "CellularGrowth/2D/Cell"
{

	Properties
	{
		_Color ("Color", Color) = (1, 1, 1, 1)
		_Gradient ("Gradient", 2D) = "white" {}
    _Size ("Size", Range(0.0, 1.0)) = 0.75
    _Intensity ("Intensity", Range(1.0, 30.0)) = 5.0
    [Toggle] _Border ("Border", Int) = 0
	}

	SubShader
	{
		Tags { "RenderType"="Opaque" "Queue"="Transparent" }
		LOD 100

		Pass
		{
      Blend SrcAlpha One
      ZTest Always

      Cull Off

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#pragma instancing_options procedural:setup
			
			#include "UnityCG.cginc"
			#include "../Common/Random.cginc"
			#include "../Common/Cell.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 position : SV_POSITION;
        float2 uv : TEXCOORD0;
        float4 color : COLOR;
        UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			StructuredBuffer<Cell> _Cells;
			float4 _Color;
      float _Size, _Intensity;
      float _Border;

      float4x4 _World2Local, _Local2World;

      sampler2D _Gradient;

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
        float4 vertex = IN.vertex * cell.alive * cell.radius * 2.0 * _Size + float4(cell.position.xy, 0, 0);
        OUT.position = UnityObjectToClipPos(vertex);
        OUT.uv = IN.uv - 0.5;

        // float u = saturate(cell.links * 0.25);
        float u = saturate(nrand(float2(iid, 0)));
        float vl = saturate(length(cell.velocity));
        float4 grad = tex2Dlod(_Gradient, float4(u, 0.5, 0, 0));
        OUT.color = lerp(grad, _Color, cell.stress) * lerp(0.5, 1.0, vl);
				return OUT;
			}

      // square root of 2 * 0.25
      static const float SQ = 0.35355339059;
      static const float INVSQ = 1.0 / 0.35355339059;

			fixed4 frag (v2f IN) : SV_Target
			{
        float d = length(IN.uv);

        float alpha = saturate(1.0 - abs(SQ - d) * INVSQ);
        alpha = saturate(alpha * alpha * alpha - 0.1);

        float4 color = IN.color;
        color.a *= lerp(saturate(saturate(0.5 - d) * _Intensity), alpha, _Border);
				return color;
			}

			ENDCG
		}
	}
}
