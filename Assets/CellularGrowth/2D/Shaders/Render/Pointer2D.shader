Shader "CellularGrowth/2D/Pointer2D"
{
  Properties
  {
    _Color ("Color", Color) = (1, 1, 1, 0.5)
    _T ("T", Range(0.0, 1.0)) = 1.0
  }

  SubShader
  {
    Tags { "RenderType" = "Opaque" }
    LOD 100

    Pass
    {
      Blend SrcAlpha OneMinusSrcAlpha

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag

      #include "UnityCG.cginc"

      struct appdata
      {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
      };

      struct v2f
      {
        float4 vertex : SV_POSITION;
        float2 uv : TEXCOORD0;
      };

      v2f vert(appdata v)
      {
        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv - 0.5;
        return o;
      }

      float4 _Color;
      float _T;

      fixed4 frag(v2f i) : SV_Target
      {
        fixed4 col = _Color;
        float l = saturate(0.5 - length(i.uv)) * 5.0;
        col *= l * saturate(_T);
        return col;
      }
      ENDCG
    }
  }
}
