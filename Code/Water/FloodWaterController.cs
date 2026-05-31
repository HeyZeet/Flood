using Sandbox;
using System;
using System.Linq;

public sealed class FloodWaterController : Component
{
	public static FloodWaterController Instance { get; private set; }

	[Header( "Surface Source" )]
	[Property] public GameObject WaterSurfaceMarker { get; set; }

	[Header( "Water Volume" )]
	[Property] public GameObject WaterVolumeObject { get; set; }
	[Property] public bool HideWaterVolumeRenderer { get; set; } = true;

	// Legacy support. If you already assigned WaterObject before, it is treated as the water volume.
	[Property] public GameObject WaterObject { get; set; }
	[Property] public string WaterTag { get; set; } = "water";
	[Property] public bool RepairWaterVolumeLocally { get; set; } = true;

	// Distance from the water volume origin to the top of the volume.
	// For basemap this is authored from StartSurfaceHeight - water.WorldPosition.z.
	[Property] public float VisualSurfaceOffset { get; set; } = 25f;
	[Property] public bool UseSceneWaterOffset { get; set; } = true;

	[Header( "Surface Visual" )]
	[Property] public GameObject WaterVisualObject { get; set; }
	[Property] public bool AutoCreateSurfaceVisual { get; set; } = true;
	[Property] public bool RepairSurfaceVisualLocally { get; set; } = true;
	[Property] public bool CopyVolumeXYScaleToSurfaceVisual { get; set; } = true;
	[Property] public Model SurfaceVisualModel { get; set; }
	[Property] public Material SurfaceVisualMaterial { get; set; }
	[Property] public Vector3 SurfaceVisualScale { get; set; } = new( 128f, 128f, 1f );
	[Property] public float SurfaceVisualOffset { get; set; } = 0f;

	[Header( "Heights" )]
	[Property] public bool UseMarkerPositionAsStartHeight { get; set; } = true;
	[Property] public float StartSurfaceHeight { get; set; } = 0f;
	[Property] public float MaxSurfaceHeight { get; set; } = 512f;
	[Property] public float DrainSurfaceHeight { get; set; } = -64f;

	[Header( "Speed" )]
	[Property] public float RiseSpeed { get; set; } = 16f;
	[Property] public float DrainSpeed { get; set; } = 48f;

	[Header( "Gameplay Waves" )]
	[Property] public bool EnableGameplayWaves { get; set; } = true;
	[Property, Range( 0f, 24f )] public float WaveHeight { get; set; } = 2.5f;
	[Property] public float WaveScale { get; set; } = 0.01f;
	[Property, Range( 0f, 50f )] public float WaveSpeed { get; set; } = 18f;
	[Property] public Vector2 WaveDirection { get; set; } = new( 1f, 0.35f );
	[Property, Range( 1, 5 )] public int WaveOctaves { get; set; } = 2;
	[Property] public float WaveLacunarity { get; set; } = 1.8f;
	[Property, Range( 0f, 1f )] public float WavePersistence { get; set; } = 0.45f;
	[Property, Range( 0f, 1f )] public float WaveSteepness { get; set; } = 0.12f;
	[Property, Range( 0f, 1f )] public float WaveVelocityInfluence { get; set; } = 0.35f;
	[Property] public float FlowVelocityMultiplier { get; set; } = 12f;

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
	public float FlatSurfaceHeight => SurfaceHeight;

	public Plane WaterPlane => new Plane( Vector3.Up, SurfaceHeight );

	[Sync( SyncFlags.FromHost | SyncFlags.Query )] public bool IsRising { get; private set; }
	[Sync( SyncFlags.FromHost | SyncFlags.Query )] public bool IsDraining { get; private set; }

	private const string SurfaceVisualObjectName = "WaterSurfaceVisual";
	private const string DefaultSurfaceVisualModel = "models/dev/plane.vmdl";
	private const string DefaultSurfaceVisualMaterial = "materials/flood/flood_water.vmat";

	private float LastAppliedSurfaceHeight { get; set; } = float.MinValue;
	private GameObject GeneratedSurfaceVisualObject { get; set; }

	protected override void OnStart()
	{
		Instance = this;

		ResolveWaterObjects();
		RepairWaterVolume();

		if ( UseMarkerPositionAsStartHeight && WaterSurfaceMarker.IsValid() )
			StartSurfaceHeight = WaterSurfaceMarker.WorldPosition.z;

		if ( UseSceneWaterOffset )
			UpdateVisualSurfaceOffsetFromScene();

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

		if ( RepairSurfaceVisualLocally )
		{
			ResolveWaterObjects();
			RepairSurfaceVisual();
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
		return position.z < GetSurfaceHeight( position );
	}

	public float GetDepth( Vector3 position )
	{
		return GetSurfaceHeight( position ) - position.z;
	}

	public float GetSurfaceHeight( Vector3 position )
	{
		return SurfaceHeight + GetSurfaceDisplacement( position ).z;
	}

	public Vector3 GetFlowVelocity( Vector3 position )
	{
		if ( !EnableGameplayWaves || WaveHeight <= 0f || WaveVelocityInfluence <= 0f )
			return Vector3.Zero;

		var velocity = ComputeWaveVelocity( new Vector2( position.x, position.y ) );
		var horizontal = new Vector3( velocity.x, velocity.y, 0f ) * FlowVelocityMultiplier;
		var vertical = Vector3.Up * velocity.z;

		return (horizontal + vertical) * WaveVelocityInfluence;
	}

	public Vector3 GetSurfaceDisplacement( Vector3 position )
	{
		if ( !EnableGameplayWaves || WaveHeight <= 0f )
			return Vector3.Zero;

		return ComputeWaveDisplacement( new Vector2( position.x, position.y ) ) * WaveHeight;
	}

	public Vector3 GetSurfaceVelocity( Vector3 position )
	{
		if ( !EnableGameplayWaves || WaveHeight <= 0f )
			return Vector3.Zero;

		return ComputeWaveVelocity( new Vector2( position.x, position.y ) );
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

		UpdateWaterVolumeObject();
		UpdateSurfaceVisualObject();
		UpdateSurfaceMarker();
	}

	private void ResolveWaterObjects()
	{
		if ( !WaterSurfaceMarker.IsValid() )
		{
			WaterSurfaceMarker = GameObject.Children.FirstOrDefault( child =>
				child.IsValid() &&
				child.Name.Equals( "WaterSurfaceMarker", StringComparison.OrdinalIgnoreCase ) );
		}

		if ( !WaterVolumeObject.IsValid() )
		{
			if ( WaterObject.IsValid() )
				WaterVolumeObject = WaterObject;
			else
				WaterVolumeObject = FindWaterObject();
		}

		if ( !WaterObject.IsValid() && WaterVolumeObject.IsValid() )
			WaterObject = WaterVolumeObject;

		if ( !GetSurfaceVisualObject().IsValid() || GetSurfaceVisualObject() == GetWaterVolumeObject() )
		{
			var surfaceVisual = FindSurfaceVisualObject();

			if ( surfaceVisual.IsValid() )
				WaterVisualObject = surfaceVisual;
			else if ( AutoCreateSurfaceVisual )
				WaterVisualObject = CreateSurfaceVisualObject();
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

		var volume = GetWaterVolumeObject();

		if ( !volume.IsValid() )
			return;

		if ( !string.IsNullOrWhiteSpace( WaterTag ) && !volume.Tags.Has( WaterTag ) )
			volume.Tags.Add( WaterTag );

		foreach ( var renderer in volume.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( renderer.IsValid() )
				renderer.Enabled = !HideWaterVolumeRenderer;
		}

		foreach ( var boxCollider in volume.Components.GetAll<BoxCollider>( FindMode.EverythingInSelfAndDescendants ) )
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

	private void UpdateWaterVolumeObject()
	{
		var volume = GetWaterVolumeObject();

		if ( !volume.IsValid() )
			return;

		var position = volume.WorldPosition;

		// SurfaceHeight is the actual water surface. The trigger volume sits lower so its
		// top lines up with the surface while still reaching down through the arena.
		position.z = SurfaceHeight - VisualSurfaceOffset;
		volume.WorldPosition = position;
	}

	private void UpdateSurfaceVisualObject()
	{
		var visual = GetSurfaceVisualObject();

		if ( !visual.IsValid() )
			return;

		var position = visual.WorldPosition;
		var volume = GetWaterVolumeObject();

		if ( volume.IsValid() )
		{
			position.x = volume.WorldPosition.x;
			position.y = volume.WorldPosition.y;
		}

		position.z = SurfaceHeight - SurfaceVisualOffset;

		visual.WorldPosition = position;

		if ( CopyVolumeXYScaleToSurfaceVisual && volume.IsValid() )
		{
			var volumeScale = volume.WorldScale;
			visual.WorldScale = new Vector3( volumeScale.x, volumeScale.y, SurfaceVisualScale.z.Clamp( 0.001f, 1000f ) );
		}
		else
		{
			visual.WorldScale = SurfaceVisualScale;
		}
	}

	private void UpdateVisualSurfaceOffsetFromScene()
	{
		var volume = GetWaterVolumeObject();

		if ( !volume.IsValid() )
			return;

		VisualSurfaceOffset = StartSurfaceHeight - volume.WorldPosition.z;
	}

	private void RepairSurfaceVisual()
	{
		var visual = GetSurfaceVisualObject();

		if ( !visual.IsValid() )
			return;

		if ( !string.IsNullOrWhiteSpace( WaterTag ) && visual.Tags.Has( WaterTag ) )
			visual.Tags.Remove( WaterTag );

		foreach ( var collider in visual.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( collider.IsValid() )
				collider.Enabled = false;
		}

		var renderer = visual.Components.Get<ModelRenderer>();

		if ( !renderer.IsValid() )
			renderer = visual.Components.Create<ModelRenderer>();

		renderer.Enabled = true;
		renderer.Model = GetSurfaceVisualModel();
		renderer.MaterialOverride = GetSurfaceVisualMaterial();
	}

	private GameObject FindSurfaceVisualObject()
	{
		var childVisual = GameObject.Children.FirstOrDefault( child =>
			child.IsValid() &&
			child.Name.Equals( SurfaceVisualObjectName, StringComparison.OrdinalIgnoreCase ) );

		if ( childVisual.IsValid() )
			return childVisual;

		return Scene.GetAllObjects( true ).FirstOrDefault( child =>
			child.IsValid() &&
			child.Name.Equals( SurfaceVisualObjectName, StringComparison.OrdinalIgnoreCase ) );
	}

	private GameObject CreateSurfaceVisualObject()
	{
		if ( GeneratedSurfaceVisualObject.IsValid() )
			return GeneratedSurfaceVisualObject;

		GeneratedSurfaceVisualObject = new GameObject( true, SurfaceVisualObjectName );
		GeneratedSurfaceVisualObject.NetworkMode = NetworkMode.Never;
		GeneratedSurfaceVisualObject.SetParent( GameObject );

		var volume = GetWaterVolumeObject();
		GeneratedSurfaceVisualObject.WorldPosition = volume.IsValid()
			? new Vector3( volume.WorldPosition.x, volume.WorldPosition.y, SurfaceHeight - SurfaceVisualOffset )
			: new Vector3( WorldPosition.x, WorldPosition.y, SurfaceHeight - SurfaceVisualOffset );

		GeneratedSurfaceVisualObject.WorldRotation = Rotation.Identity;
		GeneratedSurfaceVisualObject.WorldScale = SurfaceVisualScale;

		var renderer = GeneratedSurfaceVisualObject.Components.Create<ModelRenderer>();
		renderer.Model = GetSurfaceVisualModel();
		renderer.MaterialOverride = GetSurfaceVisualMaterial();

		return GeneratedSurfaceVisualObject;
	}

	private Model GetSurfaceVisualModel()
	{
		if ( SurfaceVisualModel.IsValid() )
			return SurfaceVisualModel;

		SurfaceVisualModel = Model.Load( DefaultSurfaceVisualModel );
		return SurfaceVisualModel;
	}

	private Material GetSurfaceVisualMaterial()
	{
		if ( SurfaceVisualMaterial is not null && SurfaceVisualMaterial.IsValid() )
			return SurfaceVisualMaterial;

		SurfaceVisualMaterial = Material.Load( DefaultSurfaceVisualMaterial );
		return SurfaceVisualMaterial;
	}

	private GameObject GetSurfaceVisualObject()
	{
		if ( WaterVisualObject.IsValid() && WaterVisualObject != GetWaterVolumeObject() )
			return WaterVisualObject;

		if ( GeneratedSurfaceVisualObject.IsValid() )
			return GeneratedSurfaceVisualObject;

		return null;
	}

	private GameObject GetWaterVolumeObject()
	{
		if ( WaterVolumeObject.IsValid() )
			return WaterVolumeObject;

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

	private Vector3 ComputeWaveDisplacement( Vector2 worldPosition )
	{
		return ComputeGerstnerWave( worldPosition, false );
	}

	private Vector3 ComputeWaveVelocity( Vector2 worldPosition )
	{
		return ComputeGerstnerWave( worldPosition, true ) * WaveHeight;
	}

	private Vector3 ComputeGerstnerWave( Vector2 worldPosition, bool velocity )
	{
		var scale = WaveScale.Clamp( 0.0001f, 10f );
		var direction = GetSafeWaveDirection();
		var octaves = WaveOctaves.Clamp( 1, 5 );
		var lacunarity = WaveLacunarity.Clamp( 0.1f, 8f );
		var persistence = WavePersistence.Clamp( 0f, 1f );
		var steepness = WaveSteepness.Clamp( 0f, 1f );
		var time = Time.Now * WaveSpeed;
		var amplitude = 1f;
		var frequency = scale;
		var amplitudeTotal = 0f;
		var result = Vector3.Zero;

		for ( var octave = 0; octave < octaves; octave++ )
		{
			var octaveDirection = Rotate( direction, octave * 1.2f );
			var phase = frequency * ((octaveDirection.x * worldPosition.x) + (octaveDirection.y * worldPosition.y)) + time * frequency;

			if ( velocity )
			{
				var angularVelocity = frequency * WaveSpeed;
				result.x -= steepness * amplitude * octaveDirection.x * angularVelocity * MathF.Sin( phase );
				result.y -= steepness * amplitude * octaveDirection.y * angularVelocity * MathF.Sin( phase );
				result.z += amplitude * angularVelocity * MathF.Cos( phase );
			}
			else
			{
				result.x += steepness * amplitude * octaveDirection.x * MathF.Cos( phase );
				result.y += steepness * amplitude * octaveDirection.y * MathF.Cos( phase );
				result.z += amplitude * MathF.Sin( phase );
			}

			amplitudeTotal += amplitude;
			amplitude *= persistence;
			frequency *= lacunarity;
		}

		if ( amplitudeTotal <= 0f )
			return Vector3.Zero;

		return result / amplitudeTotal;
	}

	private Vector2 GetSafeWaveDirection()
	{
		if ( WaveDirection.LengthSquared < 0.001f )
			return new Vector2( 1f, 0f );

		return WaveDirection.Normal;
	}

	private static Vector2 Rotate( Vector2 direction, float radians )
	{
		var sin = MathF.Sin( radians );
		var cos = MathF.Cos( radians );

		return new Vector2(
			(direction.x * cos) - (direction.y * sin),
			(direction.x * sin) + (direction.y * cos)
		);
	}
}
