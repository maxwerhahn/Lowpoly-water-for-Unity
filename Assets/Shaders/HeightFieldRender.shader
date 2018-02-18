// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced '_World2Object' with 'unity_WorldToObject'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/HeightFieldRender" {
	Properties{
		g_Color("Color", Color) = (1,1,1,1)
		g_SpecColor("Specular Color", Color) = (1,1,1,1)
		g_DepthColor("Depth Color", Color) = (1,1,1,1)
		g_Attenuation("Attenuation", Range(0.0, 1.0)) = 1.0
		g_Shininess("Shininess", Range(0.0, 2000.0)) = 20.0
		g_DepthVisible("maximum Depth", Range(50.0, 1000.0)) = 1000.0
		_ReflectionTex("Internal Reflection", 2D) = "" {}
		[Toggle] g_directionalLight("use directional light", Float) = 0
	}
		SubShader{
		Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout" }

		ZWrite On
		Cull Off
		Blend SrcAlpha OneMinusSrcAlpha

		CGINCLUDE

		#include "UnityCG.cginc"
		#include "AutoLight.cginc"
		#include "Lighting.cginc"


		float g_directionalLight;
		float g_Attenuation;
		float g_Shininess;
		float g_DepthVisible;
		fixed4 g_Color;
		fixed4 g_SpecColor;
		fixed4 g_DepthColor;

		float g_fQuadSize;
		int g_iDepth;
		int g_iWidth;

		float g_SunIntensity;
		fixed4 g_SunDir;
		fixed4 g_SunColor;
		fixed4 g_SunPos;

		uniform sampler2D _CameraDepthTexture;
		sampler2D _ReflectionTex;

		StructuredBuffer<float2> g_HeightField : register(t1);
		StructuredBuffer<float2> g_RandomDisplacement : register(t2);

		ENDCG

			Pass{
			Name "Forward"
			Lighting On

			Tags{ "LightMode" = "ForwardBase" }

			CGPROGRAM

			// Physically based Standard lighting model, and enable shadows on all light types
#pragma vertex vert
#pragma geometry geom
#pragma fragment frag
#pragma multi_compile_fwdbase 
#pragma multi_compile_fwdadd_fullshadows

		struct appdata {
			float4 vertex : POSITION;
			float3 normal : NORMAL;
			float4 color : COLOR;
			float4 uv : TEXCOORD0;
		};

		struct v2g
		{
			float3 normal : NORMAL;
			float4 vertex : SV_POSITION;
			float4 lightingColor : COLOR0;
			float4 ambientColor : COLOR1;
			float4 uv : TEXCOORD0;
			float4 projPos : TEXCOORD1;
			float4 refl : TEXCOORD2;
			SHADOW_COORDS(4)
		};

		//	specular lighting model
		float4 lighting(float3 centerPos, float3 normal) {
			float4x4 modelMatrix = unity_ObjectToWorld;
			float4x4 modelMatrixInverse = unity_WorldToObject;
			float3 pos = mul(modelMatrix, float4(centerPos, 1.0f)).xyz;

			float3 normalDirection = normalize(mul(float4(normal, 1.0f), modelMatrixInverse).xyz);
			float3 viewDirection = normalize(_WorldSpaceCameraPos - pos);
			float3 lightDirection;
			float attenuation = g_Attenuation;

			if (g_directionalLight <= 0.5f)
				lightDirection = normalize(_WorldSpaceLightPos0.xyz);
			else {
				lightDirection = normalize(-pos + g_SunPos.xyz);
				attenuation = 1.0f / distance(pos, g_SunPos.xyz) * g_SunIntensity;
			}

			float3 diffuseReflection = attenuation * _LightColor0.rgb * g_Color.rgb * max(0.0, dot(normalDirection, lightDirection));

			float3 specularReflection;
			if (dot(normalDirection, lightDirection) >= 0.0f)
				specularReflection = attenuation * g_SpecColor.rgb * _LightColor0.rgb * pow(max(0.0, dot(reflect(-lightDirection, normalDirection), viewDirection)), g_Shininess);
			else
				specularReflection = float3(0.0f, 0.0f, 0.0f);

			return float4(specularReflection + diffuseReflection, g_Color.w);
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

		float3 calculatePosition(float4 posIn) {
			float3 pos = posIn.xyz;
			int k, m = 0;
			k = round(pos.x / g_fQuadSize);
			m = round(pos.z / g_fQuadSize);
			pos.x = k * g_fQuadSize;
			pos.z = m * g_fQuadSize;
			pos.x += g_RandomDisplacement[(k)* g_iDepth + m].x;
			pos.z += g_RandomDisplacement[(k)* g_iDepth + m].y;
			pos.y = interpolateHeight(pos, k, m);
			return pos;
		}

		v2g vert(appdata v)
		{
			v2g o;
			float3 pos = calculatePosition(v.vertex);
			o.vertex = v.vertex;
			o.lightingColor = g_Color;
			o.normal = v.normal;
			o.uv = v.uv;
			o.projPos = ComputeScreenPos(UnityObjectToClipPos(pos));
			o.projPos.z = -mul(UNITY_MATRIX_MV, float4(pos, 1.0f)).z;
			TRANSFER_SHADOW(o);
			return o;
		}

		float3 calculateReflectionVector(float4 pos, float3 normal) {
			float3 worldPos = mul(unity_ObjectToWorld, pos).xyz;
			float3 worldViewDir = normalize(UnityWorldSpaceViewDir(worldPos));
			float3 worldNormal = UnityObjectToWorldNormal(normal);
			return reflect(-worldViewDir, worldNormal);
		}

		[maxvertexcount(3)]
		void geom(triangle v2g p[3], inout TriangleStream<v2g> tristream)
		{
			//	create two triangles, using 6 vertices and calulating normals, color, clip and projected positions.
			float3 pos = calculatePosition(p[0].vertex);
			float3 pos1 = calculatePosition(p[1].vertex);
			float3 pos2 = calculatePosition(p[2].vertex);

			v2g o = p[0];
			v2g o1 = p[1];
			v2g o2 = p[2];

			float3 n = cross(pos1 - pos, pos2 - pos);
			float4 color = lighting((pos + pos1 + pos2) / 3.0f, n);
			float4 ambientLighting = float4(UNITY_LIGHTMODEL_AMBIENT.rgb * g_Color.rgb,1.0f);

			o.normal = n;
			o.vertex = UnityObjectToClipPos(pos);
			o.lightingColor = color;
			o.ambientColor = ambientLighting;
			o.projPos = ComputeScreenPos(o.vertex);
			o.projPos.z = -mul(UNITY_MATRIX_MV, float4(pos, 1.0f)).z;
			o.refl = ComputeNonStereoScreenPos(o.vertex);

			o1.normal = n;
			o1.vertex = UnityObjectToClipPos(pos1);
			o1.lightingColor = color;
			o1.ambientColor = ambientLighting;
			o1.projPos = ComputeScreenPos(o1.vertex);
			o1.projPos.z = -mul(UNITY_MATRIX_MV, float4(pos1,1.0f)).z;
			o1.refl = ComputeNonStereoScreenPos(o1.vertex);

			o2.normal = n;
			o2.vertex = UnityObjectToClipPos(pos2);
			o2.lightingColor = color;
			o2.ambientColor = ambientLighting;
			o2.projPos = ComputeScreenPos(o2.vertex);
			o2.projPos.z = -mul(UNITY_MATRIX_MV, float4(pos2,1.0f)).z;
			o2.refl = ComputeNonStereoScreenPos(o2.vertex);

			TRANSFER_SHADOW(o);
			TRANSFER_SHADOW(o1);
			TRANSFER_SHADOW(o2);
			tristream.Append(o);
			tristream.Append(o1);
			tristream.Append(o2);
			tristream.RestartStrip();
		}

		fixed4 frag(v2g i) : SV_Target
		{
			//	load stored z-value
			fixed shadow = SHADOW_ATTENUATION(i);
			float depth = i.projPos.z;
			float sceneZ = LinearEyeDepth(tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)));
			float diff = (abs(sceneZ - depth));

			//	if an object is close -> change color
				if (diff < 20)
					return float4(1.0f, 1.0f, 1.0f, 1.0f) + i.ambientColor;
				if (diff < g_DepthVisible) {
					diff /= g_DepthVisible;
					return lerp(g_DepthColor, i.lightingColor,  float4(diff, diff, diff, 1.0f)) + i.ambientColor;
				}

				float4 uv1 = i.refl;
				float4 refl = tex2Dproj(_ReflectionTex, UNITY_PROJ_COORD(uv1));
				return refl * 0.2f + 0.8f * i.lightingColor + i.ambientColor;
			}
				ENDCG
			}
		}
			Fallback "VertexLit"
}
