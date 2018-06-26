Shader "CellularGrowth/3D/DebugWorldNormal"
{

	Properties
	{
	}

	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float3 normal : NORMAL;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.normal = mul(unity_ObjectToWorld, float4(v.normal, 0)).xyz;
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
        float3 normal = i.normal;
				return float4((normal + 0.5) * 0.5, 1.0);
			}
			ENDCG
		}
	}
}
