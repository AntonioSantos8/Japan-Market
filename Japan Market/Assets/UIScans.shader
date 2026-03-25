//https://patreon.com/teamdogpit
//questions? Ask Manuela! https://twitter.com/ManuelaXibanya
Shader "Xibanya/UI/Scans"
{
	Properties
	{
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "transparent" {}
		[Header(Scanlines)]
		_ScanlineCount("Scanline Count", int) = 800
		_ScanlineSpeed("Scanline Move Speed", float) = 200.0
		_ScanlineIntensity("Scanline Intensity", float) = 0.04
		_ScanColor("Scanline Alpha", Color) = (1,1,1,0.2)
	}

		CGINCLUDE
#include "UnityCG.cginc"
#include "UnityUI.cginc"

		struct appdata_t
		{
			float4 vertex   : POSITION;
			float2 uv : TEXCOORD0;
			float4 color : COLOR;
		};

		struct v2f
		{
			float4 vertex   : SV_POSITION;
			half2 uv  : TEXCOORD0;
			float4 color : COLOR;
		};

		v2f vert(appdata_t i)
		{
			v2f o;
			o.vertex = UnityObjectToClipPos(i.vertex);

			o.uv = i.uv;
			o.color = i.color;

#ifdef UNITY_HALF_TEXEL_OFFSET
			o.vertex.xy += (_ScreenParams.zw - 1.0)*float2(-1, 1);
#endif
			return o;
		}
		sampler2D _MainTex;

		float _ScanlineCount;
		float _ScanlineSpeed;
		float _ScanlineIntensity;
		fixed4 _ScanColor;

		fixed4 frag(v2f i) : SV_Target
		{
			fixed4 col = tex2D(_MainTex, i.uv) * i.color;
			float scanlineYDelta = sin(_Time.y / _ScanlineSpeed);
			float scanline = sin((i.uv.y - scanlineYDelta) * _ScanlineCount) * _ScanlineIntensity;
			col.rgb = lerp(col.rgb, _ScanColor.rgb, scanline);
			col.a = lerp(col.a, _ScanColor.a, scanline);

			return col;
		}
			ENDCG

			SubShader
		{
			Tags
			{
				"Queue" = "Transparent"
				"IgnoreProjector" = "True"
				"RenderType" = "Transparent"
				"PreviewType" = "Plane"
				"CanUseSpriteAtlas" = "True"
			}

				Cull Off
				Lighting Off
				ZWrite Off
				ZTest[unity_GUIZTestMode]
				Blend SrcAlpha OneMinusSrcAlpha

				Pass
			{
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
			ENDCG
			}
		}
}