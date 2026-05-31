FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	VrForward();
	Depth();
}

COMMON
{
	#define S_SPECULAR 1
	#include "common/shared.hlsl"
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput i )
	{
		PixelInput o = ProcessVertex( i );

		return FinalizeVertex( o );
	}
}

PS
{
	#define DEPTH_STATE_ALREADY_SET 1
	#define BLEND_MODE_ALREADY_SET 1
	#define S_TRANSLUCENT 1

	RenderState( BlendEnable, true );
	RenderState( SrcBlend, SRC_ALPHA );
	RenderState( DstBlend, INV_SRC_ALPHA );
	RenderState( SrcBlendAlpha, ONE );
	RenderState( DstBlendAlpha, INV_SRC_ALPHA );

	#include "common/utils/Material.CommonInputs.hlsl"
	#include "common/pixel.hlsl"

	float WaterOpacity < Default(0.62f); Range(0.0f, 1.0f); UiGroup("Flood Water"); >;
	float WaterBrightness < Default(1.15f); Range(0.25f, 2.0f); UiGroup("Flood Water"); >;
	float NormalScale < Default(0.45f); Range(0.0f, 2.0f); UiGroup("Flood Water"); >;
	float ShimmerStrength < Default(0.05f); Range(0.0f, 0.25f); UiGroup("Flood Water"); >;
	float Dirtiness < Default(0.25f); Range(0.0f, 1.0f); UiGroup("Flood Water"); >;

	float BigWaveSize < Default(0.35f); Range(0.0f, 1.0f); UiGroup("Wave"); >;
	float BigWaveScale < Default(180.0f); Range(1.0f, 512.0f); UiGroup("Wave"); >;
	float BigWaveTime < Default(0.38f); Range(0.0f, 4.0f); UiGroup("Wave"); >;
	float CloseRippleSize < Default(0.05f); Range(0.0f, 0.35f); UiGroup("Wave"); >;
	float CloseRippleScale < Default(42.0f); Range(4.0f, 160.0f); UiGroup("Wave"); >;
	float CloseRippleSpeed < Default(1.0f); Range(0.0f, 4.0f); UiGroup("Wave"); >;

	float g_fRoughness < Default(0.035f); Range(0.01f, 1.0f); UiGroup("Surface"); >;
	float g_fMetalness < Default(0.0f); Range(0.0f, 1.0f); UiGroup("Surface"); >;
	float g_fAmbientOcclusion < Default(0.75f); Range(0.0f, 1.0f); UiGroup("Surface"); >;

	float GetWaveHeight( float2 worldPosition )
	{
		float scale = max( BigWaveScale, 1.0f );
		float time = g_flTime * BigWaveTime;

		float wave = 0.0f;
		wave += sin( (worldPosition.x / scale) + time ) * BigWaveSize;
		wave += sin( ((worldPosition.x * 0.72f + worldPosition.y * 1.18f) / (scale * 0.62f)) - (time * 0.78f) ) * BigWaveSize * 0.55f;
		wave += cos( ((worldPosition.x * -1.10f + worldPosition.y * 0.86f) / (scale * 0.42f)) + (time * 1.37f) ) * BigWaveSize * 0.35f;
		wave += sin( ((worldPosition.x * 1.73f - worldPosition.y * 0.94f) / (scale * 0.18f)) + (time * 2.05f) ) * BigWaveSize * ShimmerStrength;

		float rippleScale = max( CloseRippleScale, 1.0f );
		float rippleTime = g_flTime * BigWaveTime * CloseRippleSpeed;

		wave += sin( ((worldPosition.x * 1.31f + worldPosition.y * 0.29f) / rippleScale) + (rippleTime * 2.10f) ) * CloseRippleSize;
		wave += cos( ((worldPosition.x * -0.47f + worldPosition.y * 1.54f) / (rippleScale * 0.72f)) - (rippleTime * 1.65f) ) * CloseRippleSize * 0.65f;
		wave += sin( ((worldPosition.x * 1.92f - worldPosition.y * 1.43f) / (rippleScale * 0.52f)) + (rippleTime * 2.85f) ) * CloseRippleSize * 0.35f;

		return wave;
	}

	float3 GetWaveNormal( float2 worldPosition )
	{
		float sampleDistance = 8.0f;

		float left = GetWaveHeight( worldPosition - float2( sampleDistance, 0.0f ) );
		float right = GetWaveHeight( worldPosition + float2( sampleDistance, 0.0f ) );
		float back = GetWaveHeight( worldPosition - float2( 0.0f, sampleDistance ) );
		float forward = GetWaveHeight( worldPosition + float2( 0.0f, sampleDistance ) );

		float dx = (right - left) / (sampleDistance * 2.0f);
		float dy = (forward - back) / (sampleDistance * 2.0f);

		return normalize( float3( -dx, -dy, 1.0f ) );
	}

	float4 MainPs( PixelInput i ) : SV_Target
	{
		float3 worldPos = g_vCameraPositionWs + i.vPositionWithOffsetWs;
		float waveHeight = GetWaveHeight( worldPos.xy );
		float3 waveNormal = GetWaveNormal( worldPos.xy );
		float shimmer = sin( ((worldPos.x + worldPos.y) / max( BigWaveScale * 0.42f, 1.0f )) + (g_flTime * BigWaveTime * 3.1f) ) * ShimmerStrength;
		float sediment =
			sin( ((worldPos.x * 0.38f + worldPos.y * 0.21f) / max( BigWaveScale * 0.33f, 1.0f )) + (g_flTime * BigWaveTime * 0.45f) ) * 0.5f +
			cos( ((worldPos.x * -0.16f + worldPos.y * 0.31f) / max( BigWaveScale * 0.22f, 1.0f )) - (g_flTime * BigWaveTime * 0.32f) ) * 0.5f;
		float colorBlend = saturate( 0.45f + waveHeight * 0.65f + shimmer - Dirtiness * 0.10f );

		float3 cleanDeepWater = float3( 0.005f, 0.250f, 0.430f );
		float3 cleanBrightWater = float3( 0.020f, 0.560f, 0.780f );
		float3 dirtyDeepWater = float3( 0.100f, 0.240f, 0.205f );
		float3 dirtyBrightWater = float3( 0.310f, 0.420f, 0.280f );

		float dirtBlend = saturate( Dirtiness + sediment * Dirtiness * 0.18f );
		float3 deepWater = lerp( cleanDeepWater, dirtyDeepWater, dirtBlend );
		float3 brightWater = lerp( cleanBrightWater, dirtyBrightWater, dirtBlend );

		Material m = Material::Init( i );
		m.Albedo = lerp( deepWater, brightWater, colorBlend ) * WaterBrightness;
		m.Normal = normalize( lerp( i.vNormalWs, waveNormal, NormalScale ) );
		m.Opacity = WaterOpacity;
		m.Roughness = g_fRoughness;
		m.Metalness = g_fMetalness;
		m.AmbientOcclusion = g_fAmbientOcclusion;

		if ( DepthNormals::WantsDepthNormals() )
			return DepthNormals::Output( m.Normal, m.Roughness, WaterOpacity );

		float4 output = ShadingModelStandard::Shade( i, m );
		output.rgb = Fog::Apply( worldPos, i.vPositionSs.xy, output.rgb );
		output.a = WaterOpacity;

		return output;
	}
}
