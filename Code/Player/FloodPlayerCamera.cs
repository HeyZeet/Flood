using Sandbox;

public sealed class FloodPlayerCamera : Component, PlayerController.IEvents
{
	[Property] public float FieldOfView { get; set; } = 80f;

	[Property] public float EyeHeight { get; set; } = 64f;

	[Property] public bool DrawDebugAim { get; set; } = true;

	public Vector3 EyePosition { get; private set; }
	public Rotation EyeRotation { get; private set; }

	public Vector3 AimForward => EyeRotation.Forward;

	public Ray AimRay => new Ray( EyePosition, AimForward );

	protected override void OnStart()
	{
		EyePosition = WorldPosition + Vector3.Up * EyeHeight;
		EyeRotation = WorldRotation;
	}

	public void OnEyeAngles( ref Angles angles )
	{
		// Later we can add recoil, view punch, camera sway, etc.
	}

	public void PostCameraSetup( CameraComponent camera )
	{
		if ( !camera.IsValid() )
			return;

		camera.FieldOfView = FieldOfView;

		EyePosition = camera.WorldPosition;
		EyeRotation = camera.WorldRotation;

		if ( DrawDebugAim )
		{
			DebugOverlay.Line(
				EyePosition,
				EyePosition + AimForward * 200f,
				Color.Cyan,
				0f
			);
		}
	}

	public Vector3 GetPointInFront( float distance )
	{
		return EyePosition + AimForward * distance;
	}

	public SceneTraceResult TraceAim( float distance, float radius = 0f )
	{
		var end = EyePosition + AimForward * distance;

		if ( radius > 0f )
		{
			return Scene.Trace
				.Sphere( radius, EyePosition, end )
				.IgnoreGameObjectHierarchy( GameObject )
				.Run();
		}

		return Scene.Trace
			.Ray( EyePosition, end )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();
	}
}
