// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced '_World2Object' with 'unity_WorldToObject'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/HeightFieldRender" {
	Properties{
		g_Color("Color", Color) = (1,1,1,1)
	}
		SubShader{
		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
		LOD 200
		Pass{
		CGPROGRAM

		#pragma target 5.0
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma vertex vert
		#pragma geometry geom
		#pragma fragment frag
		//#pragma surface surf Standard fullforwardshadows
		// Use shader model 3.0 target, to get nicer looking lighting
		#include "UnityCG.cginc"

		float g_fQuadSize;
		int g_iDepth;
		int g_iWidth;

		half g_Glossiness;
		half g_Metallic;
		fixed4 g_Color;
		fixed4 g_SunDir;
		fixed4 g_SunColor;


		#ifdef SHADER_API_D3D11

		StructuredBuffer<float2> g_HeightField : register(t1);
		StructuredBuffer<float2> g_RandomDisplacement : register(t2);

		#endif

		struct appdata {
			float4 vertex : POSITION;
			float2 uv : TEXCOORD0;
			float3 normal : NORMAL;
			float4 color : COLOR;
		};

		struct v2g
		{
			float2 uv : TEXCOORD0; // texture coordinate
			float4 vertex : SV_POSITION; // clip space position
			float3 normal : NORMAL;
			float4 color : COLOR;
		};

		v2g vert(appdata v)
		{
			v2g o;
			o.vertex = (v.vertex);
			o.color = g_Color;
			o.uv = v.uv;
			o.normal = v.normal;
			return o;
		}

		struct g2f {
			float3 normal : NORMAL;
			float4 vertex : SV_POSITION;
			float4 color : COLOR;
		};

		float4 lighting(float3 centerPos, float3 normal) {
			float3 vn = normal;
			float4x4 modelMatrix = unity_ObjectToWorld;
			float4x4 modelMatrixInverse = unity_WorldToObject;

			float3 normalDirection = normalize(mul(float4(vn, 0.0f), modelMatrixInverse).xyz);
			float3 viewDirection = normalize(_WorldSpaceCameraPos - mul(modelMatrix, float4(centerPos, 0.0)).xyz);
			float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
			float attenuation = 1.0;

			float3 ambientLighting =
				UNITY_LIGHTMODEL_AMBIENT.rgb * g_Color.rgb;

			float3 diffuseReflection =
				attenuation * g_SunColor.rgb * g_Color.rgb
				* max(0.0, dot(normalDirection, lightDirection));

			float3 specularReflection;

				specularReflection = attenuation * g_SunColor.rgb
					* g_SunColor.rgb * pow(max(0.0, dot(
						reflect(-lightDirection, normalDirection),
						viewDirection)), 1.0f);


			return float4(ambientLighting + specularReflection + diffuseReflection,1.0f);
		}

		[maxvertexcount(12)]
		void geom(point v2g p[1], inout TriangleStream<g2f> tristream)
		{

#ifdef SHADER_API_D3D11
			float3 pos = p[0].vertex.xyz;
			int k, m = 0;
			k = round(pos.x / g_fQuadSize);
			m = round(pos.z / g_fQuadSize);
			if (k != g_iWidth - 1 && m != g_iDepth) {
				pos.x = k * g_fQuadSize;
				pos.y = g_HeightField[k * g_iDepth + m];
				pos.z = m * g_fQuadSize;
				pos.x += g_RandomDisplacement[(k)* g_iDepth + m].x;
				pos.z += g_RandomDisplacement[(k)* g_iDepth + m].y;

				g2f o;
				g2f o1;
				g2f o2;

				float3 pos1 = pos;
				pos1.z = (m + 1) * g_fQuadSize;
				pos1.x = (k)* g_fQuadSize;
				pos1.y = g_HeightField[k * g_iDepth + (m + 1)];
				pos1.x += g_RandomDisplacement[k * g_iDepth + m + 1].x;
				pos1.z += g_RandomDisplacement[k * g_iDepth + m + 1].y;

				float3 pos2 = pos;
				pos2.z = (m + 1) * g_fQuadSize;
				pos2.x = (k + 1) * g_fQuadSize;
				pos2.y = g_HeightField[(k + 1) * g_iDepth + (m + 1)];
				pos2.x += g_RandomDisplacement[(k + 1) * g_iDepth + m + 1].x;
				pos2.z += g_RandomDisplacement[(k + 1) * g_iDepth + m + 1].y;

				float3 n = cross(pos1 - pos, pos2 - pos);

				float4 color = lighting((pos + pos1 + pos2) / 3.0f, n);

				o.normal = n;
				o.vertex = UnityObjectToClipPos(float4(pos, 1.0f));
				o.color = color;

				o1.normal = n;
				o1.vertex = UnityObjectToClipPos(float4(pos1, 1.0f));
				o1.color = color;

				o2.normal = n;
				o2.vertex = UnityObjectToClipPos(float4(pos2, 1.0f));
				o2.color = color;

				tristream.Append(o);
				tristream.Append(o1);
				tristream.Append(o2);

				tristream.RestartStrip();

				o.normal = -o.normal;
				o1.normal = -o.normal;
				o2.normal = -o.normal;

				tristream.Append(o);
				tristream.Append(o2);
				tristream.Append(o1);

				tristream.RestartStrip();

				pos1.x = (k + 1) * g_fQuadSize;
				pos1.z = m * g_fQuadSize;
				pos1.y = g_HeightField[(k + 1) * g_iDepth + m];
				pos1.x += g_RandomDisplacement[(k + 1) * g_iDepth + m].x;
				pos1.z += g_RandomDisplacement[(k + 1) * g_iDepth + m].y;

				n = cross(pos2 - pos, pos1 - pos);

				color = lighting((pos + pos1 + pos2) / 3.0f, n);

				o.normal = n;
				o.vertex = UnityObjectToClipPos(float4(pos, 1.0f));
				o.color = color;

				o1.normal = n;
				o1.vertex = UnityObjectToClipPos(float4(pos1, 1.0f));
				o1.color = color;

				o2.normal = n;
				o1.color = color;

				tristream.Append(o);
				tristream.Append(o2);
				tristream.Append(o1);

				tristream.RestartStrip();
				o.normal = -o.normal;
				o1.normal = -o.normal;
				o2.normal = -o.normal;
				tristream.Append(o);
				tristream.Append(o1);
				tristream.Append(o2);

				tristream.RestartStrip();
			}
#endif
		}

		fixed4 frag(g2f i) : SV_Target
		{
			return i.color;
		}
		ENDCG
	}
	}
		Fallback "Specular"
}
