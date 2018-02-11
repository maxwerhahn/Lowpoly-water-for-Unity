Shader "Unlit/Display"
{
	Properties
	{
		_texArray("Emoji Textures", 2DArray) = "" {}
		_screenTint("Screen tint (RGB)", 2D) = "black" {}
		_screenTintIntensity("Screen tint intensity", Range(0, 1)) = 0.5
		_screenTintAnimationSpeed("Screen tint animation speed", Range(0, 100)) = 50
		_index("Current Emoji", Float) = 0
	}
	SubShader
	{
		Tags{ "Queue" = "Transparent" "RenderType" = "Transparent" }
		LOD 200

		Pass
		{
			CGPROGRAM
			#pragma target 3.5
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				UNITY_FOG_COORDS(1)
				float4 vertex : SV_POSITION;
			};

			sampler2D _screenTint;
			UNITY_DECLARE_TEX2DARRAY(_texArray);
			fixed4 _screenTint_ST;
			float _index;
			float _screenTintIntensity;
			float _screenTintAnimationSpeed;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _screenTint);
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}
			
			fixed4 frag (v2f input) : SV_Target
			{
				//Sample current emoji texture in the array
				float3 uvcoords = float3(input.uv.x, input.uv.y, _index);
				fixed4 col = UNITY_SAMPLE_TEX2DARRAY(_texArray, uvcoords);
				//Add screen tint
				float2 screenuv = float2((float)(input.uv.x + sin(_Time * _screenTintAnimationSpeed * 10)), (float)(input.uv.y + _Time * _screenTintAnimationSpeed));
				fixed4 screenc = tex2D(_screenTint, screenuv);
				if (col.a > 0.1)
				{
					fixed4 newcol = screenc * _screenTintIntensity;
					col = 0.5 * col + newcol * col;
				}
				else
				{
					//discard;	//Discard all nearly transparent pixels
					col = screenc * _screenTintIntensity;
				}
				// apply fog
				UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}
			ENDCG
		}
	}
}
