using Sandbox;
using Sandbox.Rendering;
using System;

public sealed class FloodWaterScreenDistortion : BasePostProcess<FloodWaterScreenDistortion>
{
	[Header( "Tracking" )]
	[Property] public bool AutoTrackLocalPlayer { get; set; } = true;
	[Property, Range( 0f, 1f )] public float WaterTouchFraction { get; set; } = 0.08f;
	[Property] public float ImpactMinDownwardSpeed { get; set; } = 65f;

	[Header( "Distortion" )]
	[Property] public float UnderwaterDistortionStrength { get; set; } = 0.0024f;
	[Property] public float ImpactDistortionStrength { get; set; } = 0.0065f;
	[Property] public float RippleScale { get; set; } = 36f;
	[Property] public float RippleSpeed { get; set; } = 2.8f;
	[Property] public float UnderwaterTintStrength { get; set; } = 0.35f;

	[Header( "Timing" )]
	[Property] public float ImpactDuration { get; set; } = 0.65f;
	[Property] public float UnderwaterBlendSpeed { get; set; } = 9f;

	private const string ShaderPath = "shaders/postprocess/flood_water_distortion.shader";

	private bool WasTouchingWater { get; set; }
	private float ImpactStrength { get; set; }
	private float UnderwaterStrength { get; set; }

	protected override void OnUpdate()
	{
		if ( AutoTrackLocalPlayer )
			UpdateFromLocalPlayer();

		UpdateImpactDecay();
	}

	public void AddImpact( float strength = 1f )
	{
		ImpactStrength = MathF.Max( ImpactStrength, strength.Clamp( 0f, 1f ) );
	}

	public override void Render()
	{
		var impactStrength = ImpactStrength.Clamp( 0f, 1f );
		var underwaterStrength = UnderwaterStrength.Clamp( 0f, 1f );
		var distortionStrength =
			impactStrength * ImpactDistortionStrength +
			underwaterStrength * UnderwaterDistortionStrength;

		if ( distortionStrength <= 0.00001f )
			return;

		Attributes.Set( "DistortionStrength", distortionStrength );
		Attributes.Set( "UnderwaterStrength", underwaterStrength );
		Attributes.Set( "ImpactStrength", impactStrength );
		Attributes.Set( "RippleScale", RippleScale.Clamp( 1f, 256f ) );
		Attributes.Set( "RippleSpeed", RippleSpeed.Clamp( 0f, 20f ) );
		Attributes.Set( "TintStrength", underwaterStrength * UnderwaterTintStrength.Clamp( 0f, 1f ) );

		var shader = Material.FromShader( ShaderPath );
		var blit = BlitMode.WithBackbuffer( shader, Stage.AfterPostProcess, 260, false );

		Blit( blit, "Flood Water Distortion" );
	}

	private void UpdateFromLocalPlayer()
	{
		var player = FloodPlayer.Local;
		var water = FloodWaterController.Instance;

		if ( !player.IsValid() || !water.IsValid() || player.IsDead )
		{
			WasTouchingWater = false;
			UnderwaterStrength = UnderwaterStrength.LerpTo( 0f, Time.Delta * UnderwaterBlendSpeed );
			return;
		}

		var waterLevel = GetPlayerWaterLevel( player, water );
		var isTouchingWater = waterLevel >= WaterTouchFraction.Clamp( 0f, 1f );
		var enteredWater = isTouchingWater && !WasTouchingWater;
		var downwardSpeed = GetPlayerDownwardSpeed( player );

		if ( enteredWater && downwardSpeed >= ImpactMinDownwardSpeed.Clamp( 0f, 2000f ) )
			AddImpact( GetImpactStrength( downwardSpeed ) );

		WasTouchingWater = isTouchingWater;

		var eyePosition = GetEyePosition( player );
		var targetUnderwaterStrength = water.IsUnderwater( eyePosition ) ? 1f : 0f;

		UnderwaterStrength = UnderwaterStrength.LerpTo(
			targetUnderwaterStrength,
			Time.Delta * UnderwaterBlendSpeed.Clamp( 0.1f, 100f )
		);
	}

	private void UpdateImpactDecay()
	{
		if ( ImpactStrength <= 0f )
			return;

		ImpactStrength -= Time.Delta / ImpactDuration.Clamp( 0.05f, 5f );
		ImpactStrength = ImpactStrength.Clamp( 0f, 1f );
	}

	private float GetPlayerWaterLevel( FloodPlayer player, FloodWaterController water )
	{
		var bodyHeight = GetPlayerBodyHeight( player );
		var submergedHeight = water.SurfaceHeight - player.WorldPosition.z;

		return (submergedHeight / bodyHeight).Clamp( 0f, 1f );
	}

	private float GetPlayerBodyHeight( FloodPlayer player )
	{
		var controller = player.Controller;

		if ( !controller.IsValid() )
			controller = player.Components.Get<PlayerController>( FindMode.EverythingInSelfAndDescendants );

		if ( controller.IsValid() && controller.BodyHeight > 0f )
			return controller.BodyHeight;

		return 72f;
	}

	private float GetPlayerDownwardSpeed( FloodPlayer player )
	{
		var body = player.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );

		if ( !body.IsValid() )
			return ImpactMinDownwardSpeed;

		return MathF.Max( 0f, -body.Velocity.z );
	}

	private float GetImpactStrength( float downwardSpeed )
	{
		var minimum = ImpactMinDownwardSpeed.Clamp( 0f, 2000f );
		var normalized = ((downwardSpeed - minimum) / 450f).Clamp( 0f, 1f );

		return 0.35f + normalized * 0.65f;
	}

	private Vector3 GetEyePosition( FloodPlayer player )
	{
		var camera = player.Components.Get<FloodPlayerCamera>( FindMode.EverythingInSelfAndDescendants );

		if ( camera.IsValid() )
			return camera.EyePosition;

		return player.WorldPosition + Vector3.Up * 64f;
	}
}
