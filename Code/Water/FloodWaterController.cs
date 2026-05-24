using Sandbox;
using System;
using System.Linq;

public sealed class FloodWaterController : Component
{
	public static FloodWaterController Instance { get; private set; }

	[Header( "Surface Source" )]
	[Property] public GameObject WaterSurfaceMarker { get; set; }

	[Header( "Visual Water" )]
	[Property] public GameObject WaterVisualObject { get; set; }

	// Legacy support. If you already assigned WaterObject before, this can still work.
	[Property] public GameObject WaterObject { get; set; }
	[Property] public string WaterTag { get; set; } = "water";
	[Property] public bool RepairWaterVolumeLocally { get; set; } = true;

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
	[Property] public bool LogNetworkState { get; set; } = false;

	[Sync( SyncFlags.FromHost | SyncFlags.Query ), Change( nameof( OnSurfaceHeightSynced ) )]
	public float SurfaceHeight { get; private set; }

	// Compatibility for older code that may still ask for WaterHeight.
	public float WaterHeight => SurfaceHeight;

	public Plane WaterPlane => new Plane( Vector3.Up, SurfaceHeight );

	[Sync( SyncFlags.FromHost | SyncFlags.Query )] public bool IsRising { get; private set; }
	[Sync( SyncFlags.FromHost | SyncFlags.Query )] public bool IsDraining { get; private set; }

	private float LastAppliedSurfaceHeight { get; set; } = float.MinValue;

	protected override void OnStart()
	{
		Instance = this;

		ResolveWaterObjects();
		RepairWaterVolume();

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
		if ( RepairWaterVolumeLocally )
		{
			ResolveWaterObjects();
			RepairWaterVolume();
		}

		if ( Networking.IsHost )
		{
			if ( IsRising )
				UpdateRising();

			if ( IsDraining )
				UpdateDraining();
		}
		else
		{
			ApplySyncedSurfaceHeight();
		}

		if ( DrawDebugSurface )
			DrawSurfaceDebug();
	}

	public void StartFlood()
	{
		if ( !Networking.IsHost )
			return;

		IsRising = true;
		IsDraining = false;
	}

	public void StopFlood()
	{
		if ( !Networking.IsHost )
			return;

		IsRising = false;
	}

	public void StartDrain()
	{
		if ( !Networking.IsHost )
			return;

		IsDraining = true;
		IsRising = false;
	}

	public void ResetWater()
	{
		if ( !Networking.IsHost )
			return;

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
		ApplySyncedSurfaceHeight();
	}

	private void OnSurfaceHeightSynced( float oldHeight, float newHeight )
	{
		if ( Networking.IsHost )
			return;

		ApplySyncedSurfaceHeight( true );

		if ( LogNetworkState )
			Log.Info( $"Water height synced: {oldHeight:0.00} -> {newHeight:0.00}" );
	}

	private void ApplySyncedSurfaceHeight( bool force = false )
	{
		if ( !force && LastAppliedSurfaceHeight.AlmostEqual( SurfaceHeight, 0.01f ) )
			return;

		LastAppliedSurfaceHeight = SurfaceHeight;

		UpdateSurfaceMarker();
		UpdateVisualWaterObject();
	}

	private void ResolveWaterObjects()
	{
		if ( !WaterSurfaceMarker.IsValid() )
		{
			WaterSurfaceMarker = GameObject.Children.FirstOrDefault( child =>
				child.IsValid() &&
				child.Name.Equals( "WaterSurfaceMarker", StringComparison.OrdinalIgnoreCase ) );
		}

		if ( !GetVisualWaterObject().IsValid() )
		{
			var waterObject = FindWaterObject();

			if ( waterObject.IsValid() )
			{
				WaterVisualObject = waterObject;
				WaterObject = waterObject;
			}
		}
	}

	private GameObject FindWaterObject()
	{
		var waterObject = GameObject.Children.FirstOrDefault( IsWaterObject );

		if ( waterObject.IsValid() )
			return waterObject;

		return Scene.GetAllObjects( true ).FirstOrDefault( IsWaterObject );
	}

	private bool IsWaterObject( GameObject gameObject )
	{
		if ( !gameObject.IsValid() )
			return false;

		if ( !string.IsNullOrWhiteSpace( WaterTag ) && gameObject.Tags.Has( WaterTag ) )
			return true;

		return gameObject.Name.Equals( "water", StringComparison.OrdinalIgnoreCase );
	}

	private void RepairWaterVolume()
	{
		if ( !RepairWaterVolumeLocally )
			return;

		var visual = GetVisualWaterObject();

		if ( !visual.IsValid() )
			return;

		if ( !string.IsNullOrWhiteSpace( WaterTag ) && !visual.Tags.Has( WaterTag ) )
			visual.Tags.Add( WaterTag );

		foreach ( var boxCollider in visual.Components.GetAll<BoxCollider>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !boxCollider.IsValid() )
				continue;

			boxCollider.IsTrigger = true;
		}
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
