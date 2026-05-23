using Sandbox;

public sealed class FloodPlayerCamera : Component, PlayerController.IEvents
{
	[Property] public float FieldOfView { get; set; } = 80f;
	[Property] public float EyeHeight { get; set; } = 64f;
	[Property] public bool DrawDebugAim { get; set; } = true;

	[Header( "Recoil" )]
	[Property] public float RecoilRecoveryRate { get; set; } = 10f;

	public Vector3 EyePosition { get; private set; }
	public Rotation EyeRotation { get; private set; }

	private Angles RecoilOffset { get; set; }

	public Vector3 AimForward => EyeRotation.Forward;
	public Ray AimRay => new Ray( EyePosition, AimForward );

	protected override void OnStart()
	{
		EyePosition = WorldPosition + Vector3.Up * EyeHeight;
		EyeRotation = WorldRotation;
	}

	protected override void OnUpdate()
	{
		if ( !IsLocalPlayer() )
			return;

		UpdateRecoilRecovery();
	}

	public void AddViewPunch( Angles punch )
	{
		RecoilOffset += punch;
	}

	public void OnEyeAngles( ref Angles angles )
	{
		// Leave this empty for now.
		// Forcing angles here can fight the built-in PlayerController camera handling.
	}

	private void UpdateDeathCamera( CameraComponent camera, FloodPlayer player )
	{
		var target = player.Health.DeathCameraTarget;

		var targetPosition = WorldPosition;

		if ( target.IsValid() )
			targetPosition = target.WorldPosition + Vector3.Up * 48f;

		var cameraOffset =
			Vector3.Up * 80f +
			EyeRotation.Backward * 180f;

		camera.WorldPosition = targetPosition + cameraOffset;
		camera.WorldRotation = Rotation.LookAt( targetPosition - camera.WorldPosition );

		EyePosition = camera.WorldPosition;
		EyeRotation = camera.WorldRotation;
	}

	public void PostCameraSetup( CameraComponent camera )
	{
		if ( !camera.IsValid() )
			return;

		if ( !IsLocalPlayer() )
		{
			camera.Enabled = false;
			return;
		}

		camera.Enabled = true;
		camera.FieldOfView = FieldOfView;

		var player = Components.Get<FloodPlayer>();

		if ( player.IsValid() && player.IsDead )
		{
			UpdateDeathCamera( camera, player );
			return;
		}

		var baseRotation = camera.WorldRotation;
		var recoilRotation = RecoilOffset.ToRotation();

		camera.WorldRotation = baseRotation * recoilRotation;

		EyePosition = camera.WorldPosition;
		EyeRotation = camera.WorldRotation;

		DrawDebugAimLine();
	}

	private bool IsLocalPlayer()
	{
		var player = Components.Get<FloodPlayer>();

		if ( player.IsValid() )
			return player.IsLocalPlayer;

		return !IsProxy;
	}

	public Vector3 GetPointInFront( float distance )
	{
		return EyePosition + AimForward * distance;
	}

	public SceneTraceResult TraceAim( float distance, float radius = 0f )
	{
		var end = EyePosition + AimForward * distance;

		if ( radius > 0f )
			return TraceAimSphere( end, radius );

		return TraceAimRay( end );
	}

	private SceneTraceResult TraceAimRay( Vector3 end )
	{
		return Scene.Trace
			.Ray( EyePosition, end )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();
	}

	private SceneTraceResult TraceAimSphere( Vector3 end, float radius )
	{
		return Scene.Trace
			.Sphere( radius, EyePosition, end )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();
	}

	private void UpdateRecoilRecovery()
	{
		if ( RecoilOffset == Angles.Zero )
			return;

		RecoilOffset = Angles.Lerp( RecoilOffset, Angles.Zero, RecoilRecoveryRate * Time.Delta );

		if (
			RecoilOffset.pitch.AlmostEqual( 0f, 0.01f ) &&
			RecoilOffset.yaw.AlmostEqual( 0f, 0.01f ) &&
			RecoilOffset.roll.AlmostEqual( 0f, 0.01f )
		)
		{
			RecoilOffset = Angles.Zero;
		}
	}

	private void DrawDebugAimLine()
	{
		if ( !DrawDebugAim )
			return;

		DebugOverlay.Line(
			EyePosition,
			EyePosition + AimForward * 200f,
			Color.Cyan,
			0f
		);
	}
}
