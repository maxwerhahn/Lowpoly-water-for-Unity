// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced '_World2Object' with 'unity_WorldToObject'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/HeightFieldRender" {
	Properties{
		g_Color("Color", Color) = (1,1,1,1)
		g_SpecColor("Specular Color", Color) = (1,1,1,1)
		g_Attenuation("Attenuation", Range(0.0, 1.0)) = 1.0
		g_Shininess("Shininess", Range(0.0, 2000.0)) = 20.0
		g_DepthVisible("maximum Depth", Range(50.0, 1000.0)) = 1000.0
		[Toggle] g_directionalLight("use directional light", Float) = 0
	}
		SubShader{
		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }

		ZWrite On
		Cull Off
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
		float g_DepthVisible;
		fixed4 g_Color;
		fixed4 g_SpecColor;
		fixed4 g_SunDir;
		fixed4 g_SunColor;
		fixed4 g_SunPos;
		uniform sampler2D _CameraDepthTexture;

		StructuredBuffer<float2> g_HeightField : register(t1);
		StructuredBuffer<float2> g_RandomDisplacement : register(t2);

		struct appdata {
			float4 vertex : POSITION;
			float3 normal : NORMAL;
			float4 color : COLOR;
		};

		struct v2g
		{
			float4 vertex : SV_POSITION;
			float3 normal : NORMAL;
			float4 color : COLOR;
		};

		v2g vert(appdata v)
		{
			v2g o;
			o.vertex = v.vertex;
			o.color = g_Color;
			o.normal = v.normal;
			return o;
		}

		struct g2f {
			float3 normal : NORMAL;
			float4 vertex : SV_POSITION;
			float4 color : COLOR;
			float4 projPos : TEXCOORD0;
		};

		//	specular lighting model
		float4 lighting(float3 centerPos, float3 normal) {
			float4x4 modelMatrix = unity_ObjectToWorld;
			float4x4 modelMatrixInverse = unity_WorldToObject;
			float3 pos = mul(modelMatrix, float4(centerPos, 1.0f)).xyz;

			float3 normalDirection = normalize(mul(float4(normal, 1.0f), modelMatrixInverse).xyz);
			float3 viewDirection = normalize(_WorldSpaceCameraPos - pos);
			float3 lightDirection;

			if (g_directionalLight > 0.5f)
				lightDirection = -normalize(g_SunDir.xyz);
			else
				lightDirection = normalize(-pos + g_SunPos.xyz);

			float3 ambientLighting = UNITY_LIGHTMODEL_AMBIENT.rgb * g_Color.rgb;

			float3 diffuseReflection = g_Attenuation * g_SunColor.rgb * g_Color.rgb * max(0.0, dot(normalDirection, lightDirection));

			float3 specularReflection;
			if (dot(normalDirection, lightDirection) >= 0.0f)
				specularReflection = g_Attenuation * g_SpecColor.rgb * g_SunColor.rgb * pow(max(0.0, dot(reflect(-lightDirection, normalDirection), viewDirection)), g_Shininess);
			else
				specularReflection = float3(0.0f, 0.0f, 0.0f);

			return float4(ambientLighting + specularReflection + diffuseReflection, g_Color.w);
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

		[maxvertexcount(6)]
		void geom(point v2g p[1], inout TriangleStream<g2f> tristream)
		{
			//	create two triangles, using 6 vertices and calulating normals, color, clip and projected positions.
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
			o.vertex = UnityObjectToClipPos(pos);
			o.color = color;
			o.projPos = ComputeScreenPos(o.vertex);
			o.projPos.z = -mul(UNITY_MATRIX_MV, float4(pos,1.0f)).z;

			o1.normal = n;
			o1.vertex = UnityObjectToClipPos(pos1);
			o1.color = color;
			o1.projPos = ComputeScreenPos(o1.vertex);
			o1.projPos.z = -mul(UNITY_MATRIX_MV, float4(pos1, 1.0f)).z;

			o2.normal = n;
			o2.vertex = UnityObjectToClipPos(pos2);
			o2.color = color;
			o2.projPos = ComputeScreenPos(o2.vertex);
			o2.projPos.z = -mul(UNITY_MATRIX_MV, float4(pos2, 1.0f)).z;

			tristream.Append(o);
			tristream.Append(o1);
			tristream.Append(o2);
			tristream.RestartStrip();

			pos1.x = (k + 1) * g_fQuadSize;
			pos1.z = m * g_fQuadSize;
			pos1.x += g_RandomDisplacement[(k + 1) * g_iDepth + m].x;
			pos1.z += g_RandomDisplacement[(k + 1) * g_iDepth + m].y;
			pos1.y = interpolateHeight(pos1, k + 1, m);

			n = cross(pos2 - pos, pos1 - pos);
			color = lighting((pos + pos1 + pos2) / 3.0f, n);

			o.normal = n;
			o.color = color;

			o1.normal = n;
			o1.vertex = UnityObjectToClipPos(pos1);
			o1.color = color;
			o1.projPos = ComputeScreenPos(o1.vertex);
			o1.projPos.z = -mul(UNITY_MATRIX_MV, float4(pos1, 1.0f)).z;

			o2.normal = n;
			o2.color = color;

			tristream.Append(o);
			tristream.Append(o2);
			tristream.Append(o1);
			tristream.RestartStrip();
		}

		fixed4 frag(g2f i) : SV_Target
		{
			//	load stored z-value
			float depth = i.projPos.z;
			float sceneZ = LinearEyeDepth(tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)));
			float diff = (abs(sceneZ - depth));

			//	if an object is close -> change color
			if (diff < g_DepthVisible && abs(normalize(i.normal).y) > 0.8f) {
				diff /= g_DepthVisible;
				return lerp(float4(max(i.color.rgb + float3(-0.3f, 0.25f, -0.05f), float3(0.0f, 0.0f, 0.0f)), i.color.w), i.color,  float4(diff, diff, diff, 1.0f));
			}
			else
				return i.color;
	}
	ENDCG
}
	}
		Fallback "Specular"
}
