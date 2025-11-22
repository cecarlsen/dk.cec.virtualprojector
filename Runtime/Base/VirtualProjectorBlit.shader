/*
	Copyright © Carl Emil Carlsen 2025
	http://cec.dk

	

*/

Shader "Hidden/VirtualProjectorBlit"
{
	Properties
	{
		_MainTex( "Whatever", 2D ) = "black" {} // Needed for Graphics.Blit to work.
	}

	HLSLINCLUDE

		#include "UnityCG.cginc"

		struct ToVert
		{
			float4 vertex : POSITION;
			float2 uv : TEXCOORD0;
		};

		struct ToFrag
		{
			float4 vertex : SV_POSITION;
			float2 uv : TEXCOORD0;
		};

		sampler2D _MainTex;
		//float4 _MainTex_TexelSize;
		//float4 _MainTex_ST; // It looks like Graphics.Blit overwrites this.
		float4 _MainTexTransform;
		float4 _MaskColor;

		ToFrag Vert( ToVert v )
		{
			ToFrag o;
			o.vertex = UnityObjectToClipPos( v.vertex );
			o.uv = v.uv;
			return o;
		}


		fixed4 FragWhite( ToFrag i ) : SV_Target
		{
			
			return fixed4( 0, 1, 0, 1 );
		}


		fixed4 FragTexture( ToFrag i ) : SV_Target
		{
			float2 mainTexUv = i.uv * _MainTexTransform.xy + _MainTexTransform.zw;
			//if( mainTexUv.x < 0.0 || mainTexUv.x > 1.0 ) return fixed4( 0, 1, 0, 1 );

			// TODO: be clever.
			if( mainTexUv.x < 0.0 || mainTexUv.x > 1.0 || mainTexUv.y < 0.0 || mainTexUv.y > 1.0 ) return _MaskColor;

			fixed4 col = tex2D( _MainTex, mainTexUv );

			return col;
		}


	ENDHLSL


	SubShader
	{
		Pass
		{
			Name "_WhitePass"
			Cull Off
			ZWrite Off
			ZTest Always

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment FragWhite
			ENDHLSL
		}

		Pass
		{
			Name "_TexturePass"
			Cull Off
			ZWrite Off
			ZTest Always

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment FragTexture
			ENDHLSL
		}
	}
}
