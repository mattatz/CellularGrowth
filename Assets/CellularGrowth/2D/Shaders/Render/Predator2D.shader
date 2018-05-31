Shader "CellularGrowth/2D/Predator"
{

	Properties
	{
		_Color ("Color", Color) = (1, 1, 1, 1)
    _Size ("Size", Range(0.0, 1.0)) = 0.75
    _Intensity ("Intensity", Range(1.0, 30.0)) = 5.0
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
			#include "../Common/Predator.cginc"

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

			StructuredBuffer<Predator> _Predators;
			float4 _Color;
      float _Size, _Intensity;

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

        Predator p = _Predators[iid];
        float4 vertex = IN.vertex * p.alive * p.radius * 2.0 * _Size + float4(p.position.xy, 0, 0);
        OUT.position = UnityObjectToClipPos(vertex);
        OUT.uv = IN.uv - 0.5;
        OUT.color = _Color;
				return OUT;
			}

			fixed4 frag (v2f IN) : SV_Target
			{
        float d = length(IN.uv);
        float alpha = saturate(saturate(0.5 - d) * _Intensity);
				return alpha * IN.color;
			}

			ENDCG
		}
	}
}
