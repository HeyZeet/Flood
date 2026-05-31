HEADER
{
	DevShader = true;
}

MODES
{
	Default();
	Forward();
}

COMMON
{
	#include "postprocess/shared.hlsl"
}

struct VertexInput
{
	float3 vPositionOs : POSITION < Semantic( PosXyz ); >;
	float2 vTexCoord : TEXCOORD0 < Semantic( LowPrecisionUv ); >;
};

struct PixelInput
{
	float2 vTexCoord : TEXCOORD0;

	#if ( PROGRAM == VFX_PROGRAM_VS )
		float4 vPositionPs : SV_Position;
	#endif

	#if ( PROGRAM == VFX_PROGRAM_PS )
		float4 vPositionSs : SV_Position;
	#endif
};

VS
{
	PixelInput MainVs( VertexInput i )
	{
		PixelInput o;
		o.vPositionPs = float4( i.vPositionOs.xy, 0.0f, 1.0f );
		o.vTexCoord = i.vTexCoord;
		return o;
	}
}

PS
{
	#include "postprocess/common.hlsl"
	#include "postprocess/functions.hlsl"

	Texture2D g_tColorBuffer < Attribute( "ColorBuffer" ); SrgbRead( true ); >;

	float DistortionStrength < Attribute( "DistortionStrength" ); Default( 0.0f ); >;
	float UnderwaterStrength < Attribute( "UnderwaterStrength" ); Default( 0.0f ); >;
	float ImpactStrength < Attribute( "ImpactStrength" ); Default( 0.0f ); >;
	float RippleScale < Attribute( "RippleScale" ); Default( 34.0f ); >;
	float RippleSpeed < Attribute( "RippleSpeed" ); Default( 3.5f ); >;
	float TintStrength < Attribute( "TintStrength" ); Default( 0.0f ); >;

	float2 GetRippleOffset( float2 uv )
	{
		float2 centered = uv - 0.5f;
		float radius = length( centered );
		float time = g_flTime * RippleSpeed;

		float broadWave =
			sin( (uv.y * RippleScale) + time ) +
			cos( ((uv.x + uv.y) * RippleScale * 0.72f) - (time * 1.25f) );

		float closeWave =
			sin( ((uv.x * 1.7f - uv.y * 0.6f) * RippleScale * 1.9f) + (time * 1.7f) ) * 0.45f;

		float impactRing = sin( (radius * 46.0f) - (time * 5.0f) ) * saturate( 1.0f - radius * 1.35f );
		float ripple = broadWave * (0.55f + UnderwaterStrength * 0.45f) + closeWave + impactRing * ImpactStrength * 2.0f;

		float2 direction = normalize( centered + float2( 0.001f, 0.002f ) );
		float2 tangent = float2( -direction.y, direction.x );

		float2 offset = direction * ripple * 0.55f + tangent * broadWave * 0.22f;
		offset *= DistortionStrength * (UnderwaterStrength + ImpactStrength);

		return offset;
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float2 uv = CalculateViewportUv( i.vPositionSs.xy );
		float2 distortedUv = uv + GetRippleOffset( uv );

		float4 color = g_tColorBuffer.SampleLevel( g_sBilinearMirror, distortedUv, 0 );
		float vignette = saturate( length( uv - 0.5f ) * 1.55f );
		float3 waterTint = float3( 0.035f, 0.220f, 0.190f );

		color.rgb = lerp( color.rgb, waterTint, TintStrength * (0.45f + vignette * 0.35f) );

		return color;
	}
}
