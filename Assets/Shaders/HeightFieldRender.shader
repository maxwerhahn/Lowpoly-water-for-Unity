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
		g_Reflection("Reflection", Range(0.0,1.0)) = 1.0
		g_Shininess("Shininess", Range(0.0, 2000.0)) = 20.0
		g_DepthVisible("maximum Depth", Range(1.0, 1000.0)) = 1000.0
		g_FoamDepth("maximum Foam-Depth", Range(0.0, 1.0)) = 0.1
		g_DistortionFactor("Distortion", Range(0.0,150.0)) = 80.0
		[HideInInspector] _ReflectionTex("Internal Reflection", 2D) = "" {}
		[Toggle] g_directionalLight("use directional light", Float) = 0
	}
		SubShader{
		Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderMode" = "Transparent" }

		ZWrite On
		Cull Off
		Blend SrcAlpha OneMinusSrcAlpha

		CGINCLUDE

		#include "UnityCG.cginc"
		#include "AutoLight.cginc"
		#include "Lighting.cginc"
			
		float g_directionalLight;
		float g_Attenuation;
		float g_Reflection;
		float g_Shininess;
		float g_DepthVisible;
		float g_FoamDepth;
		float g_DistortionFactor;
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

		ENDCG

			Pass{

			CGPROGRAM

			// Physically based Standard lighting model, and enable shadows on all light types
#pragma vertex vert
#pragma geometry geom
#pragma fragment frag

		struct appdata {
			float4 vertex : POSITION;
			float3 normal : NORMAL;
			float4 color : COLOR;
		};

		struct v2g
		{
			float3 normal : NORMAL;
			float4 vertex : SV_POSITION;
			float4 lightingColor : COLOR0;
			float4 projPos : TEXCOORD0;
			float4 refl : TEXCOORD1;
			//SHADOW_COORDS(3)
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

			if (g_directionalLight >= 0.5f)
				lightDirection = normalize(_WorldSpaceLightPos0.xyz);
			else {
				lightDirection = normalize(g_SunPos.xyz - pos);
				attenuation = 1.0f / distance(pos, g_SunPos.xyz);
			}
			attenuation *= g_SunIntensity;

			float3 diffuseReflection = attenuation * _LightColor0.rgb * g_Color.rgb * max(0.0, dot(normalDirection, lightDirection));

			float3 specularReflection;
			if (dot(normalDirection, lightDirection) > 0.0f)
				specularReflection = attenuation * g_SpecColor.rgb * _LightColor0.rgb * pow(max(0.0, dot(reflect(-lightDirection, normalDirection), viewDirection)), g_Shininess);
			else
				specularReflection = float3(0.0f, 0.0f, 0.0f);

			return float4(specularReflection + diffuseReflection + UNITY_LIGHTMODEL_AMBIENT.rgb, g_Color.w);
		}
		
		v2g vert(appdata v)
		{
			v2g o;
			float3 pos = (v.vertex);
			o.vertex = v.vertex;
			o.lightingColor = g_Color;
			o.normal = v.normal;
			o.projPos = ComputeScreenPos(UnityObjectToClipPos(pos));
			o.projPos.z = -UnityObjectToViewPos(pos).z;
			//TRANSFER_SHADOW(o);
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
			float3 pos = (p[0].vertex);
			float3 pos1 = (p[1].vertex);
			float3 pos2 = (p[2].vertex);

			v2g o = p[0];
			v2g o1 = p[1];
			v2g o2 = p[2];

			float3 n = normalize(cross(pos1 - pos, pos2 - pos));
			float3 avgPos = (pos + pos1 + pos2) / 3.0f;
			float4 color = lighting(avgPos, n);
			float4 reflPos = ComputeNonStereoScreenPos(UnityObjectToClipPos(avgPos));

			o.vertex = UnityObjectToClipPos(pos);
			o.lightingColor = color;
			o.normal = n;
			o.refl = ComputeNonStereoScreenPos(o.vertex);

			o1.vertex = UnityObjectToClipPos(pos1);
			o1.lightingColor = color;
			o1.normal = n;
			o1.refl = ComputeNonStereoScreenPos(o1.vertex);

			o2.vertex = UnityObjectToClipPos(pos2);
			o2.lightingColor = color;
			o2.normal = n;
			o2.refl = ComputeNonStereoScreenPos(o2.vertex);

			//TRANSFER_SHADOW(o);
			//TRANSFER_SHADOW(o1);
			//TRANSFER_SHADOW(o2);
			tristream.Append(o);
			tristream.Append(o1);
			tristream.Append(o2);
			tristream.RestartStrip();
		}

		fixed4 frag(v2g i) : SV_Target
		{
			//fixed shadow = SHADOW_ATTENUATION(i);
			//	load stored z-value
			float depth = i.projPos.z;
			float sceneZ = LinearEyeDepth(tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)));
			float diff = (abs(sceneZ - depth));
			float4 uv1 = i.refl;
			uv1.xy += i.normal.xz * g_DistortionFactor;
			float4 refl = tex2Dproj(_ReflectionTex, UNITY_PROJ_COORD(uv1));

			//	if an object is close -> change color
				if (diff < g_DepthVisible) {
					diff /= g_DepthVisible;
					if (diff < g_FoamDepth)
						return float4(1.0f, 1.0f, 1.0f, 1.0f);
					return lerp((lerp(g_DepthColor, i.lightingColor, float4(diff, diff, diff, diff))), refl, float4(g_Reflection, g_Reflection, g_Reflection, 0.0f));
				}
				return lerp(i.lightingColor, refl, float4(g_Reflection, g_Reflection, g_Reflection, 0.0f));
			}
				ENDCG
			}
		}
		Fallback "VertexLit"
}
