using Sandbox;

public sealed class FloodWaterController : Component
{
	public static FloodWaterController Instance { get; private set; }

	[Header( "Surface Source" )]
	[Property] public GameObject WaterSurfaceMarker { get; set; }

	[Header( "Visual Water" )]
	[Property] public GameObject WaterVisualObject { get; set; }

	// Legacy support. If you already assigned WaterObject before, this can still work.
	[Property] public GameObject WaterObject { get; set; }

	// If the visible water object is a box/cube and its origin is in the middle,
	// this should be half of the visible water thickness.
	// Example: visible water depth 50 -> VisualSurfaceOffset = 25.
	[Property] public float VisualSurfaceOffset { get; set; } = 25f;

	[Header( "Heights" )]
	[Property] public bool UseMarkerPositionAsStartHeight { get; set; } = true;
	[Property] public float StartSurfaceHeight { get; set; } = 0f;
	[Property] public float MaxSurfaceHeight { get; set; } = 512f;
	[Property] public float DrainSurfaceHeight { get; set; } = -64f;

	[Header( "Speed" )]
	[Property] public float RiseSpeed { get; set; } = 16f;
	[Property] public float DrainSpeed { get; set; } = 48f;

	[Header( "Startup" )]
	[Property] public bool StartFlooded { get; set; } = false;

	[Header( "Debug" )]
	[Property] public bool DrawDebugSurface { get; set; } = true;
	[Property] public float DebugLineSize { get; set; } = 512f;

	public float SurfaceHeight { get; private set; }

	// Compatibility for older code that may still ask for WaterHeight.
	public float WaterHeight => SurfaceHeight;

	public Plane WaterPlane => new Plane( Vector3.Up, SurfaceHeight );

	public bool IsRising { get; private set; }
	public bool IsDraining { get; private set; }

	protected override void OnStart()
	{
		Instance = this;

		if ( UseMarkerPositionAsStartHeight && WaterSurfaceMarker.IsValid() )
			StartSurfaceHeight = WaterSurfaceMarker.WorldPosition.z;

		SurfaceHeight = StartFlooded ? MaxSurfaceHeight : StartSurfaceHeight;

		SetSurfaceHeight( SurfaceHeight );

		Log.Info( $"FloodWaterController started. SurfaceHeight: {SurfaceHeight}" );
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnUpdate()
	{
		if ( IsRising )
			UpdateRising();

		if ( IsDraining )
			UpdateDraining();

		if ( DrawDebugSurface )
			DrawSurfaceDebug();
	}

	public void StartFlood()
	{
		IsRising = true;
		IsDraining = false;
	}

	public void StopFlood()
	{
		IsRising = false;
	}

	public void StartDrain()
	{
		IsDraining = true;
		IsRising = false;
	}

	public void ResetWater()
	{
		IsRising = false;
		IsDraining = false;

		SetSurfaceHeight( StartSurfaceHeight );
	}

	public bool IsUnderwater( Vector3 position )
	{
		return position.z < SurfaceHeight;
	}

	public float GetDepth( Vector3 position )
	{
		return SurfaceHeight - position.z;
	}

	public float GetSurfaceHeight( Vector3 position )
	{
		// Later we can add waves here.
		// For now the flood water is a flat plane.
		return SurfaceHeight;
	}

	public Vector3 GetFlowVelocity( Vector3 position )
	{
		// Later we can add currents here.
		return Vector3.Zero;
	}

	private void UpdateRising()
	{
		var newHeight = SurfaceHeight + RiseSpeed * Time.Delta;

		if ( newHeight >= MaxSurfaceHeight )
		{
			newHeight = MaxSurfaceHeight;
			IsRising = false;
		}

		SetSurfaceHeight( newHeight );
	}

	private void UpdateDraining()
	{
		var newHeight = SurfaceHeight - DrainSpeed * Time.Delta;

		if ( newHeight <= DrainSurfaceHeight )
		{
			newHeight = DrainSurfaceHeight;
			IsDraining = false;
		}

		SetSurfaceHeight( newHeight );
	}

	private void SetSurfaceHeight( float height )
	{
		SurfaceHeight = height;

		UpdateSurfaceMarker();
		UpdateVisualWaterObject();
	}

	private void UpdateSurfaceMarker()
	{
		if ( !WaterSurfaceMarker.IsValid() )
			return;

		var position = WaterSurfaceMarker.WorldPosition;
		position.z = SurfaceHeight;
		WaterSurfaceMarker.WorldPosition = position;
	}

	private void UpdateVisualWaterObject()
	{
		var visual = GetVisualWaterObject();

		if ( !visual.IsValid() )
			return;

		var position = visual.WorldPosition;

		// SurfaceHeight is the actual water surface.
		// The visual object can sit lower so its top face lines up with the surface.
		position.z = SurfaceHeight - VisualSurfaceOffset;

		visual.WorldPosition = position;
	}

	private GameObject GetVisualWaterObject()
	{
		if ( WaterVisualObject.IsValid() )
			return WaterVisualObject;

		return WaterObject;
	}

	private void DrawSurfaceDebug()
	{
		var center = WaterSurfaceMarker.IsValid()
			? WaterSurfaceMarker.WorldPosition
			: WorldPosition;

		center.z = SurfaceHeight;

		DebugOverlay.Line(
			center + Vector3.Left * DebugLineSize,
			center + Vector3.Right * DebugLineSize,
			Color.Cyan,
			0f
		);

		DebugOverlay.Line(
			center + Vector3.Forward * DebugLineSize,
			center + Vector3.Backward * DebugLineSize,
			Color.Cyan,
			0f
		);
	}
}