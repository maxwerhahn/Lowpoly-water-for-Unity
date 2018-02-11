// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced '_World2Object' with 'unity_WorldToObject'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/HeightFieldRender" {
	Properties{
		g_Color("Color", Color) = (1,1,1,1)
		g_Attenuation("Attenuation", Range(0.0, 1.0)) = 1.0
		g_Shininess("Shininess", Range(0.0, 2000.0)) = 20.0
		[Toggle] g_directionalLight("use directional light", Float) = 0
	}
		SubShader{
		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }

		LOD 200
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

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

		float g_directionalLight;
		float g_Attenuation;
		float g_Shininess;
		fixed4 g_Color;
		fixed4 g_SunDir;
		fixed4 g_SunColor;
		fixed4 g_SunPos;

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
			float4x4 modelMatrix = unity_ObjectToWorld;
			float4x4 modelMatrixInverse = unity_WorldToObject;
			float3 pos = mul(modelMatrix, float4(centerPos, 1.0f)).xyz;

			float3 normalDirection = normalize(mul(float4(normal, 1.0f), modelMatrixInverse).xyz);
			float3 viewDirection = normalize(_WorldSpaceCameraPos - pos);
			float3 lightDirection; 
			if(g_directionalLight > 0.5f)
				lightDirection = -normalize(g_SunDir.xyz);
			else
				lightDirection = normalize(-pos + g_SunPos.xyz);
			float3 ambientLighting = UNITY_LIGHTMODEL_AMBIENT.rgb * g_Color.rgb;

			float3 diffuseReflection = g_Attenuation * g_Color.rgb * max(0.0, dot(normalDirection, lightDirection));

			float3 specularReflection = g_Attenuation * g_SunColor.rgb * pow(max(0.0, dot(reflect(-lightDirection, normalDirection), viewDirection)), g_Shininess);

			return float4(ambientLighting + specularReflection + diffuseReflection , g_Color.w);
		}

		float interpolateHeight(float3 pos, float k, float m) {

			//	get surrounding height values at the vertex position (can be randomly displaced)
			float x1 = g_HeightField[k * g_iDepth + m].x;
			float x2 = g_HeightField[min((k + 1), g_iWidth - 1) * g_iDepth + min(m + 1, g_iDepth - 1)].x;
			float x3 = g_HeightField[k * g_iDepth + min(m + 1, g_iDepth - 1)].x;
			float x4 = g_HeightField[min((k + 1), g_iWidth - 1) * g_iDepth + m].x;

			//	get x and y value between 0 and 1 for interpolation
			float x = (pos.x / g_fQuadSize - k);
			float y = (pos.z / g_fQuadSize - m);

			//	bilinear interpolation to get height at vertex i
			//	note if x == 0 and y == 0 vertex position is at heightfield position.
			float resultingHeight = (x1 * (1 - x) + x4 * (x)) * (1 - y) + (x3 * (1 - x) + x2 * (x)) * (y);
			return resultingHeight;
		}

		[maxvertexcount(12)]
		void geom(point v2g p[1], inout TriangleStream<g2f> tristream)
		{

#ifdef SHADER_API_D3D11
			float3 pos = p[0].vertex.xyz;
			int k, m = 0;
			k = round(pos.x / g_fQuadSize);
			m = round(pos.z / g_fQuadSize);
				pos.x = k * g_fQuadSize;
				pos.z = m * g_fQuadSize;
				pos.x += g_RandomDisplacement[(k)* g_iDepth + m].x;
				pos.z += g_RandomDisplacement[(k)* g_iDepth + m].y;
				pos.y = interpolateHeight(pos, k, m);

				g2f o;
				g2f o1;
				g2f o2;

				float3 pos1 = pos;
				pos1.z = (m + 1) * g_fQuadSize;
				pos1.x = (k)* g_fQuadSize;
				pos1.x += g_RandomDisplacement[k * g_iDepth + m + 1].x;
				pos1.z += g_RandomDisplacement[k * g_iDepth + m + 1].y;
				pos1.y = interpolateHeight(pos1, k, m + 1);

				float3 pos2 = pos;
				pos2.z = (m + 1) * g_fQuadSize;
				pos2.x = (k + 1) * g_fQuadSize;
				pos2.x += g_RandomDisplacement[(k + 1) * g_iDepth + m + 1].x;
				pos2.z += g_RandomDisplacement[(k + 1) * g_iDepth + m + 1].y;
				pos2.y = interpolateHeight(pos2, k + 1, m + 1);

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

				color = lighting((pos + pos1 + pos2) / 3.0f, -n);
				o.normal = -n;
				o.color = color;
				o1.normal = o.normal;
				o1.color = color;
				o2.normal = o.normal;
				o2.color = color;
				tristream.Append(o);
				tristream.Append(o2);
				tristream.Append(o1);
				tristream.RestartStrip();

				pos1.x = (k + 1) * g_fQuadSize;
				pos1.z = m * g_fQuadSize;
				pos1.x += g_RandomDisplacement[(k + 1) * g_iDepth + m].x;
				pos1.z += g_RandomDisplacement[(k + 1) * g_iDepth + m].y;
				pos1.y = interpolateHeight(pos1, k + 1, m);

				n = cross(pos2 - pos, pos1 - pos);

				color = lighting((pos + pos1 + pos2) / 3.0f, n);

				o.normal = n;
				o.vertex = UnityObjectToClipPos(float4(pos, 1.0f));
				o.color = color;

				o1.normal = n;
				o1.vertex = UnityObjectToClipPos(float4(pos1, 1.0f));
				o1.color = color;

				o2.normal = n;
				o2.color = color;

				tristream.Append(o);
				tristream.Append(o2);
				tristream.Append(o1);
				tristream.RestartStrip();

				color = lighting((pos + pos1 + pos2) / 3.0f, -n);
				o.normal = -n;
				o.color = color;
				o1.normal = o.normal;
				o1.color = color;
				o2.normal = o.normal;
				o2.color = color;
				tristream.Append(o);
				tristream.Append(o1);
				tristream.Append(o2);
				tristream.RestartStrip();

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
