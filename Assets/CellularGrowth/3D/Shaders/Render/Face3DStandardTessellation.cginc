
#include "UnityCG.cginc"
#include "UnityGBuffer.cginc"
#include "UnityStandardUtils.cginc"
#include "Tessellation.cginc"

// Tessellation shader reference:
//  http://alex.vlachos.com/graphics/CurvedPNTriangles.pdf

#define INPUT_PATCH_SIZE 3
#define OUTPUT_PATCH_SIZE 3

#include "../Common/Cell.cginc"
#include "../Common/Edge.cginc"
#include "../Common/Face.cginc"

half4 _Color, _Emission;
sampler2D _MainTex;
float4 _MainTex_ST;

half _Glossiness;
half _Metallic;

float _TessEdgeMinLength, _TessEdgeMaxLength;
float _TessFactor;

struct appdata
{
  float4 vertex : POSITION;
  float2 uv : TEXCOORD0;
  uint vid : SV_VertexID;
  UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2h
{
  float4 position : POSITION;
  float3 normal : NORMAL;
  UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct h2d_main
{
  float4 position : POSITION;
  float3 normal : NORMAL;
};

struct h2d_const
{
  float EdgeTessFactor[3] : SV_TessFactor;
  float InsideTessFactor : SV_InsideTessFactor;

  float3 B210 : POSITION3;
  float3 B120 : POSITION4;
  float3 B021 : POSITION5;
  float3 B012 : POSITION6;
  float3 B102 : POSITION7;
  float3 B201 : POSITION8;
  float3 B111 : CENTER;
    
  float3 N110 : NORMAL3;
  float3 N011 : NORMAL4;
  float3 N101 : NORMAL5;
};

struct d2f
{
  float4 position : SV_POSITION;
#if defined(PASS_CUBE_SHADOWCASTER)
  // Cube map shadow caster pass
  float3 shadow : TEXCOORD0;
#elif defined(UNITY_PASS_SHADOWCASTER)
  // Default shadow caster pass
#else
  // GBuffer construction pass
  float3 normal : NORMAL;
  half3 ambient : TEXCOORD0;
  float3 wpos : TEXCOORD1;
#endif
};

StructuredBuffer<Cell> _Cells;
StructuredBuffer<Edge> _Edges;
StructuredBuffer<Face> _Faces;

float4x4 _World2Local, _Local2World;
float _Normal, _Debug;

void setup() {
  unity_ObjectToWorld = _Local2World;
  unity_WorldToObject = _World2Local;
}

v2h vert (appdata IN, uint iid : SV_InstanceID)
{
  v2h OUT;
  UNITY_SETUP_INSTANCE_ID(IN);
  UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

  Face f = _Faces[iid];
  Cell c0 = _Cells[f.c0];
  Cell c1 = _Cells[f.c1];
  Cell c2 = _Cells[f.c2];
  float3 position = lerp(c0.position, lerp(c1.position, c2.position, saturate(IN.vid - 1)), saturate(IN.vid));
  position *= lerp(f.alive, 1, _Debug);
  float4 vertex = float4(position, 1);

  float3 normal = lerp(c0.normal, lerp(c1.normal, c2.normal, saturate(IN.vid - 1)), saturate(IN.vid));
  normal = lerp(normal, normalize(cross(c1.position - c0.position, c2.position - c0.position)), _Normal);

  OUT.position = vertex;
  OUT.normal = normal;

  return OUT;
}

h2d_const hs_const(InputPatch<v2h, INPUT_PATCH_SIZE> i, uint id : SV_PrimitiveID)
{
  h2d_const o = (h2d_const) 0;

  float3 p1 = i[0].position.xyz;
  float3 p2 = i[1].position.xyz;
  float3 p3 = i[2].position.xyz;

  float l0 = distance(p1, p2);
  float l1 = distance(p2, p3);
  float l2 = distance(p3, p1);

  float inv_edge_scale = 1 / (_TessEdgeMaxLength - _TessEdgeMinLength);
  o.EdgeTessFactor[0] = lerp(1, _TessFactor, saturate((l0 - _TessEdgeMinLength) * inv_edge_scale));
  o.EdgeTessFactor[1] = lerp(1, _TessFactor, saturate((l1 - _TessEdgeMinLength) * inv_edge_scale));
  o.EdgeTessFactor[2] = lerp(1, _TessFactor, saturate((l2 - _TessEdgeMinLength) * inv_edge_scale));
  o.InsideTessFactor = _TessFactor;

  float3 b300 = p1;
  float3 b030 = p2;
  float3 b003 = p3;
    
  float3 n1 = i[0].normal;
  float3 n2 = i[1].normal;
  float3 n3 = i[2].normal;
    
  float3 n200 = n1;
  float3 n020 = n2;
  float3 n002 = n3;

  float w12 = dot((p2 - p1), n1);
  o.B210 = (2.0 * p1 + p2 - w12 * n1) / 3.0;

  float w21 = dot((p1 - p2), n2);
  o.B120 = (2.0 * p2 + p1 - w21 * n2) / 3.0;

  float w23 = dot((p3 - p2), n2);
  o.B021 = (2.0 * p2 + p3 - w23 * n2) / 3.0;
    
  float w32 = dot((p2 - p3), n3);
  o.B012 = (2.0 * p3 + p2 - w32 * n3) / 3.0;

  float w31 = dot((p1 - p3), n3);
  o.B102 = (2.0 * p3 + p1 - w31 * n3) / 3.0;
    
  float w13 = dot((p3 - p1), n1);
  o.B201 = (2.0 * p1 + p3 - w13 * n1) / 3.0;
    
  float3 e = (o.B210 + o.B120 + o.B021 + o.B012 + o.B102 + o.B201) / 6.0;
  float3 v = (p1 + p2 + p3) / 3.0;
  o.B111 = e + ((e - v) / 2.0);
    
  float v12 = 2.0f * dot((p2 - p1), (n1 + n2)) / dot((p2 - p1), (p2 - p1));
  o.N110 = normalize((n1 + n2 - v12 * (p2 - p1)));

  float v23 = 2.0f * dot((p3 - p2), (n2 + n3)) / dot((p3 - p2), (p3 - p2));
  o.N011 = normalize((n2 + n3 - v23 * (p3 - p2)));

  float v31 = 2.0f * dot((p1 - p3), (n3 + n1)) / dot((p1 - p3), (p1 - p3));
  o.N101 = normalize((n3 + n1 - v31 * (p1 - p3)));

  return o;
}

[domain("tri")]
[partitioning("integer")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(OUTPUT_PATCH_SIZE)]
[patchconstantfunc("hs_const")]
h2d_main hull(InputPatch<v2h, INPUT_PATCH_SIZE> i, uint id : SV_OutputControlPointID)
{
  h2d_main o = (h2d_main) 0;
  o.position = i[id].position;
  o.normal = i[id].normal;
  return o;
}

[domain("tri")]
d2f domain(h2d_const hs_const_data, const OutputPatch<h2d_main, OUTPUT_PATCH_SIZE> i, float3 uvw : SV_DomainLocation)
{
  float u = uvw.x;
  float v = uvw.y;
  float w = uvw.z;
  float uu = u * u;
  float vv = v * v;
  float ww = w * w;
  float uu3 = 3.0 * uu;
  float vv3 = 3.0 * vv;
  float ww3 = 3.0 * ww;

  float3 interpPosition =
        i[0].position.xyz * w * ww +
        i[1].position.xyz * u * uu +
        i[2].position.xyz * v * vv +
        hs_const_data.B210 * ww3 * u +
        hs_const_data.B120 * uu3 * w +
        hs_const_data.B201 * ww3 * v +
        hs_const_data.B021 * uu3 * v +
        hs_const_data.B102 * vv3 * w +
        hs_const_data.B012 * vv3 * u +
        hs_const_data.B111 * 6.0 * w * u * v;

  float3 interpNormal =
        i[0].normal * ww +
        i[1].normal * uu +
        i[2].normal * vv +
        hs_const_data.N110 * w * u +
        hs_const_data.N011 * u * v +
        hs_const_data.N101 * w * v;

  d2f OUT = (d2f) 0;
  float3 wpos = mul(_Local2World, float4(interpPosition, 1)).xyz;

  float3 normal = normalize(interpNormal);
  float3 wnrm = normalize(mul(_Local2World, float4(normal, 0)).xyz);

#if defined(PASS_CUBE_SHADOWCASTER)
  // Cube map shadow caster pass: Transfer the shadow vector.
  OUT.position = UnityWorldToClipPos(float4(wpos.xyz, 1));
  OUT.shadow = wpos.xyz - _LightPositionRange.xyz;
#elif defined(UNITY_PASS_SHADOWCASTER)
  // Default shadow caster pass: Apply the shadow bias.
  float scos = dot(wnrm, normalize(UnityWorldSpaceLightDir(wpos.xyz)));
  wpos.xyz -= wnrm * unity_LightShadowBias.z * sqrt(1 - scos * scos);
  OUT.position = UnityApplyLinearShadowBias(UnityWorldToClipPos(float4(wpos.xyz, 1)));
#else
  // GBuffer construction pass
  OUT.position = UnityWorldToClipPos(float4(wpos.xyz, 1));
  OUT.normal = wnrm;
  OUT.ambient = ShadeSHPerVertex(wnrm, 0);
  OUT.wpos = wpos.xyz;
#endif

  return OUT;
}

#if defined(PASS_CUBE_SHADOWCASTER)

// Cube map shadow caster pass
half4 frag(d2f IN) : SV_Target
{
  float depth = length(IN.shadow) + unity_LightShadowBias.x;
  return UnityEncodeCubeShadowDepth(depth * _LightPositionRange.w);
}

#elif defined(UNITY_PASS_SHADOWCASTER)

// Default shadow caster pass
half4 frag() : SV_Target 
{
  return 0; 
}

#else

// GBuffer construction pass
void frag(d2f IN, out half4 outGBuffer0 : SV_Target0, out half4 outGBuffer1 : SV_Target1, out half4 outGBuffer2 : SV_Target2, out half4 outEmission : SV_Target3) 
{
  half3 albedo = _Color.rgb;

  // PBS workflow conversion (metallic -> specular)
  half3 c_diff, c_spec;
  half refl10;
  c_diff = DiffuseAndSpecularFromMetallic(
    albedo, _Metallic, // input
    c_spec, refl10 // output
  );

  // Update the GBuffer.
  UnityStandardData data;
  data.diffuseColor = c_diff;
  data.occlusion = 1.0;
  data.specularColor = c_spec;
  data.smoothness = _Glossiness;
  data.normalWorld = normalize(IN.normal);
  UnityStandardDataToGbuffer(data, outGBuffer0, outGBuffer1, outGBuffer2);

  // Calculate ambient lighting and output to the emission buffer.
  half3 sh = ShadeSHPerPixel(data.normalWorld, IN.ambient, IN.wpos);
  outEmission = _Emission + half4(sh * c_diff, 1);
}

#endif
