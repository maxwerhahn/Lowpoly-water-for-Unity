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
		g_DistortionFactor("Distortion", Range(0.0, 500.0)) = 50.0
		g_PointLightIntensity("PointLightIntensity", Range(0.0, 1000.0)) = 10.0
		[HideInInspector] _ReflectionTex("Internal Reflection", 2D) = "" {}
	}
		SubShader{
		Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderMode" = "Transparent" }
		
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
		float g_PointLightIntensity;
		fixed4 g_Color;
		fixed4 g_SpecColor;
		fixed4 g_DepthColor;

		float g_fQuadSize;
		int g_iDepth;
		int g_iWidth;

		uniform sampler2D _CameraDepthTexture;
		sampler2D _ReflectionTex;

		StructuredBuffer<float2> g_HeightField : register(t1);

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
				
		fixed4 frag(v2g i) : SV_Target
		{
			//fixed shadow = SHADOW_ATTENUATION(i);
			//	load stored z-value
			float depth = i.projPos.z;
			float sceneZ = LinearEyeDepth(tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)));
			float diff = (abs(sceneZ - depth));
			float4 uv1 = i.refl;
			uv1.xy -= i.normal.zx * g_DistortionFactor;
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

			//	pass for directional lights
		Pass {
				ZWrite On
				Cull Off
				Blend SrcAlpha OneMinusSrcAlpha

				Tags{ "LightMode" = "ForwardBase" }
				CGPROGRAM

				#pragma multi_compile_fwdbase 
				#pragma vertex vert
				#pragma geometry geom
				#pragma fragment frag

				//	specular lighting model
				float4 lighting(float3 centerPos, float3 normal) {
				float4x4 modelMatrix = unity_ObjectToWorld;
				float4x4 modelMatrixInverse = unity_WorldToObject;
				float3 pos = mul(modelMatrix, float4(centerPos, 1.0f)).xyz;

				float3 normalDirection = normalize(mul(float4(normal, 1.0f), modelMatrixInverse).xyz);
				float3 viewDirection = normalize(_WorldSpaceCameraPos - pos);
				float3 lightDirection;
				float attenuation = g_Attenuation;

				if (_WorldSpaceLightPos0.w == 0.0f)
					lightDirection = normalize(_WorldSpaceLightPos0.xyz);
				else {
					float3 direction = _WorldSpaceLightPos0.xyz - pos;
					lightDirection = normalize(direction);
					attenuation /= length(direction);
				}

				float3 diffuseReflection;
				float3 specularReflection;

				if (dot(normalDirection, lightDirection) > 0.0f) {
					specularReflection = attenuation * g_SpecColor.rgb * _LightColor0.rgb * pow(max(0.0, dot(reflect(-lightDirection, normalDirection), viewDirection)), g_Shininess);
					diffuseReflection = attenuation * _LightColor0.rgb * g_Color.rgb * max(0.0, dot(normalDirection, lightDirection));
				}
				//	directional light under water
				else {
					diffuseReflection = 0.5f * attenuation * _LightColor0.rgb * g_Color.rgb * max(0.0, dot(-normalDirection, lightDirection));
					specularReflection = float3(0.0f, 0.0f, 0.0f);
				}

				//	add vertex lighting (4 non-important vertex lights)
				for (int i = 0; i < 4; i++)
				{
					float4 lightPosition = float4(unity_4LightPosX0[i],	unity_4LightPosY0[i], unity_4LightPosZ0[i], 1.0f);

					float3 dist = lightPosition.xyz - pos;
					float3 dir = normalize(dist);
					float squaredDistance =	dot(dist, dist);
					float att = g_Attenuation / (1.0f + unity_4LightAtten0[i] * squaredDistance);
					
					if (dot(normalDirection, dir) < 0.0f) {
						diffuseReflection = (diffuseReflection + 0.5f * att * unity_LightColor[i].rgb * g_Color.rgb * max(0.0, dot(-normalDirection, dir)));
					}
					else {
						diffuseReflection = (diffuseReflection + att * unity_LightColor[i].rgb * g_Color.rgb * max(0.0, dot(normalDirection, dir)));
						specularReflection = (specularReflection + att * g_SpecColor.rgb * unity_LightColor[i].rgb * pow(max(0.0, dot(reflect(-dir, normalDirection), viewDirection)), g_Shininess));
					}
				}
				return float4(specularReflection + diffuseReflection + UNITY_LIGHTMODEL_AMBIENT * g_Color, g_Color.w);
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
			ENDCG
			}			
		}
		Fallback "VertexLit"
}
