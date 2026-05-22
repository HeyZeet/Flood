using Sandbox;
using System.Collections.Generic;

public enum BuoyancyMaterialPreset
{
	Custom,
	LightPlastic,
	Wood,
	HeavyWood,
	Metal,
	Stone
}

public sealed class FloodBuoyancy : Component
{
	[Header( "References" )]
	[Property] public Rigidbody Body { get; set; }

	[Header( "Phase" )]
	[Property] public bool EnabledDuringBuildPhase { get; set; } = false;

	[Header( "Float / Sink" )]
	// Relative to water.
	// 0.35 = very light, floats high.
	// 0.75 = wood-like, floats well.
	// 1.0 = neutral, sits low.
	// 1.5+ = heavy, sinks.

    [Property] public BuoyancyMaterialPreset MaterialPreset { get; set; } = BuoyancyMaterialPreset.Custom;

    [Property] public bool ApplyPresetOnStart { get; set; } = true;

	[Property] public float RelativeDensity { get; set; } = 0.75f;

	[Property] public bool UseBuiltInApplyBuoyancy { get; set; } = true;
	[Property] public bool UseManualSampleBuoyancy { get; set; } = true;

	[Header( "Manual Buoyancy" )]
	[Property] public int SampleGridSize { get; set; } = 2;
	[Property] public float LiftAcceleration { get; set; } = 900f;
	[Property] public float MaxSampleDepth { get; set; } = 48f;
	[Property] public float SurfaceTouchDepth { get; set; } = 0f;

	[Header( "Drag" )]
	[Property] public float WaterLinearDrag { get; set; } = 0.8f;
	[Property] public float WaterAngularDrag { get; set; } = 1.5f;
	[Property] public float PointVelocityDrag { get; set; } = 0.15f;

	[Header( "Stability" )]
	[Property] public bool StabilizeRollPitch { get; set; } = true;
	[Property] public float UprightStabilization { get; set; } = 0.15f;

	[Header( "Raft Stability" )]
	[Property] public bool UseAttachedRaftStability { get; set; } = true;
	[Property] public float AttachedPieceStabilityBonus { get; set; } = 0.08f;
	[Property] public float MaxRaftStabilityMultiplier { get; set; } = 2.25f;
	[Property] public float MinDamagedRaftStabilityMultiplier { get; set; } = 0.35f;
	[Property] public float UnattachedInstabilityMultiplier { get; set; } = 0.65f;

	[Header( "Debug" )]
	[Property] public bool DrawDebug { get; set; } = true;
	[Property] public bool LogDebug { get; set; } = false;

    [Header( "Water Effects" )]
    [Property] public GameObject SplashPrefab { get; set; }
    [Property] public float SplashLifeTime { get; set; } = 1.5f;
    [Property] public float MinSplashSpeed { get; set; } = 80f;
    [Property] public float SplashCooldown { get; set; } = 0.35f;

	public float LastWetness { get; private set; }
	public bool IsTouchingWater => LastWetness > 0f;
	public Vector3 LastWaterContactPoint { get; private set; }
	public int LastRaftPieceCount { get; private set; } = 1;
	public float LastRaftHealthFraction { get; private set; } = 1f;
    private bool WasTouchingWater { get; set; }
    private TimeSince TimeSinceLastSplash { get; set; } = 999f;
	private BuildPiece BuildPiece { get; set; }

	private struct RaftStabilityState
	{
		public int PieceCount { get; set; }
		public float HealthFraction { get; set; }
		public float StabilityMultiplier { get; set; }
		public float LiftMultiplier { get; set; }
	}

    private void ApplyMaterialPreset()
    {
	    switch ( MaterialPreset )
	    {
		    case BuoyancyMaterialPreset.Custom:
			    return;

		    case BuoyancyMaterialPreset.LightPlastic:
			    RelativeDensity = 0.35f;
			    LiftAcceleration = 1000f;
			    MaxSampleDepth = 40f;
			    WaterLinearDrag = 0.7f;
			    WaterAngularDrag = 1.2f;
			    PointVelocityDrag = 0.12f;
			    UprightStabilization = 0.1f;
			    break;

		    case BuoyancyMaterialPreset.Wood:
			    RelativeDensity = 0.65f;
			    LiftAcceleration = 900f;
			    MaxSampleDepth = 48f;
			    WaterLinearDrag = 0.8f;
			    WaterAngularDrag = 1.5f;
			    PointVelocityDrag = 0.15f;
			    UprightStabilization = 0.15f;
			    break;

		    case BuoyancyMaterialPreset.HeavyWood:
			    RelativeDensity = 0.9f;
			    LiftAcceleration = 850f;
			    MaxSampleDepth = 56f;
			    WaterLinearDrag = 0.9f;
			    WaterAngularDrag = 1.7f;
			    PointVelocityDrag = 0.18f;
			    UprightStabilization = 0.12f;
			    break;

		    case BuoyancyMaterialPreset.Metal:
			    RelativeDensity = 1.8f;
			    LiftAcceleration = 650f;
			    MaxSampleDepth = 64f;
			    WaterLinearDrag = 1.0f;
			    WaterAngularDrag = 2.0f;
			    PointVelocityDrag = 0.22f;
			    UprightStabilization = 0.05f;
			    break;

		    case BuoyancyMaterialPreset.Stone:
			    RelativeDensity = 2.4f;
			    LiftAcceleration = 500f;
			    MaxSampleDepth = 64f;
			    WaterLinearDrag = 1.1f;
			    WaterAngularDrag = 2.2f;
			    PointVelocityDrag = 0.25f;
			    UprightStabilization = 0.02f;
			    break;
	    }
    } 

	protected override void OnStart()
	{
		if ( !Body.IsValid() )
			Body = Components.Get<Rigidbody>( FindMode.EverythingInSelfAndAncestors );

		if ( !Body.IsValid() )
			Log.Warning( $"{GameObject.Name} has FloodBuoyancy but no Rigidbody." );

		BuildPiece = Components.Get<BuildPiece>( FindMode.EverythingInSelfAndAncestors );

        if ( ApplyPresetOnStart )
	        ApplyMaterialPreset();
	}

	protected override void OnFixedUpdate()
	{
		if ( !Body.IsValid() )
			return;

		var water = FloodWaterController.Instance;

		if ( !water.IsValid() )
			return;

		if ( !ShouldApplyBuoyancy() )
			return;

		SimulateBuoyancy( water );
	}

	private bool ShouldApplyBuoyancy()
	{
		if ( EnabledDuringBuildPhase )
			return true;

		var roundManager = FloodGameManager.Instance;

		if ( !roundManager.IsValid() )
			return true;

		return !roundManager.IsBuildPhase();
	}

	private void SimulateBuoyancy( FloodWaterController water )
	{
		var bounds = Body.GetWorldBounds();

		var bottomZ = bounds.Mins.z;
		var topZ = bounds.Maxs.z;
		var height = (topZ - bottomZ).Clamp( 1f, 999999f );

		var waterSurfaceAtBody = water.GetSurfaceHeight( Body.WorldPosition );

		var submergedHeight = (waterSurfaceAtBody - bottomZ).Clamp( 0f, height );
		var wholeBodyWetness = (submergedHeight / height).Clamp( 0f, 1f );
		var raftStability = GetRaftStabilityState();

		LastWetness = wholeBodyWetness;
		LastRaftPieceCount = raftStability.PieceCount;
		LastRaftHealthFraction = raftStability.HealthFraction;

        if ( wholeBodyWetness <= 0f )
        {
	        WasTouchingWater = false;
	        return;
        }

        if ( UseBuiltInApplyBuoyancy )
	        Body.ApplyBuoyancy( water.WaterPlane, Time.Delta );

        if ( UseManualSampleBuoyancy )
	        ApplySampleBuoyancy( water, bounds, raftStability );

        UpdateWaterContactEffects( water, wholeBodyWetness );

        ApplyWaterDrag( wholeBodyWetness, raftStability );
        
		if ( StabilizeRollPitch )
			ApplyUprightStabilization( wholeBodyWetness, raftStability );

		if ( LogDebug )
		{
			Log.Info(
				$"{GameObject.Name} wetness:{wholeBodyWetness:0.00} density:{RelativeDensity:0.00} raftPieces:{raftStability.PieceCount} raftHealth:{raftStability.HealthFraction:0.00} stability:{raftStability.StabilityMultiplier:0.00} surface:{waterSurfaceAtBody:0.00} bottom:{bottomZ:0.00}"
			);
		}
	}

    private void UpdateWaterContactEffects( FloodWaterController water, float wetness )
    {
	    var isTouchingWater = wetness > 0f;

	    if ( isTouchingWater && !WasTouchingWater )
		    PlayEnterWaterSplash( water );

	    WasTouchingWater = isTouchingWater;
    }

    private void PlayEnterWaterSplash( FloodWaterController water )
    {
	    if ( !SplashPrefab.IsValid() )
		    return;

	    if ( TimeSinceLastSplash < SplashCooldown )
		    return;

	    if ( Body.Velocity.Length < MinSplashSpeed )
		    return;

	    TimeSinceLastSplash = 0f;

	    var splash = SplashPrefab.Clone();

	    var position = LastWaterContactPoint;

	    if ( position == Vector3.Zero )
		    position = Body.WorldPosition;

	    position.z = water.SurfaceHeight;

	    splash.WorldPosition = position;
	    splash.WorldRotation = Rotation.Identity;

	    if ( SplashLifeTime > 0f )
	    {
		    var destroyAfterTime = splash.Components.Create<DestroyAfterTime>();
		    destroyAfterTime.LifeTime = SplashLifeTime;
	    }
    }

	private void ApplySampleBuoyancy( FloodWaterController water, BBox bounds, RaftStabilityState raftStability )
	{
		var samples = BuildBottomSamples( bounds );

		var wetSampleCount = 0;
		var contactPointTotal = Vector3.Zero;

		foreach ( var sample in samples )
		{
			var surfaceHeight = water.GetSurfaceHeight( sample );
			var depth = surfaceHeight - sample.z;

			if ( depth < SurfaceTouchDepth )
			{
				if ( DrawDebug )
					DrawSampleDebug( sample, surfaceHeight, false );

				continue;
			}

			var depthPercent = (depth / MaxSampleDepth).Clamp( 0f, 1f );

			// Lower density = stronger lift.
			// Higher density = weaker lift, meaning heavy objects sink.
			var densityScale = 1f / RelativeDensity.Clamp( 0.1f, 10f );

			var liftVelocity = Vector3.Up * LiftAcceleration * depthPercent * densityScale * raftStability.LiftMultiplier * Time.Delta;

			// Simple point drag from body movement through water.
			var waterFlow = water.GetFlowVelocity( sample );
			var relativeVelocity = Body.Velocity - waterFlow;
			var dragVelocity = -relativeVelocity * PointVelocityDrag * raftStability.StabilityMultiplier * depthPercent * Time.Delta;

			Body.Velocity += liftVelocity + dragVelocity;

			wetSampleCount++;
			contactPointTotal += sample;

			if ( DrawDebug )
				DrawSampleDebug( sample, surfaceHeight, true );
		}

		if ( wetSampleCount > 0 )
			LastWaterContactPoint = contactPointTotal / wetSampleCount;
	}

	private IEnumerable<Vector3> BuildBottomSamples( BBox bounds )
	{
		var gridSize = SampleGridSize.Clamp( 1, 4 );

		var min = bounds.Mins;
		var max = bounds.Maxs;

		var z = min.z;

		if ( gridSize == 1 )
		{
			yield return new Vector3(
				(min.x + max.x) * 0.5f,
				(min.y + max.y) * 0.5f,
				z
			);

			yield break;
		}

		for ( var x = 0; x < gridSize; x++ )
		{
			for ( var y = 0; y < gridSize; y++ )
			{
				var xPercent = gridSize == 1 ? 0.5f : x / (float)(gridSize - 1);
				var yPercent = gridSize == 1 ? 0.5f : y / (float)(gridSize - 1);

				yield return new Vector3(
					min.x.LerpTo( max.x, xPercent ),
					min.y.LerpTo( max.y, yPercent ),
					z
				);
			}
		}
	}

	private void ApplyWaterDrag( float wetness, RaftStabilityState raftStability )
	{
		if ( WaterLinearDrag > 0f )
		{
			var linearDrag = 1f - (WaterLinearDrag * raftStability.StabilityMultiplier * wetness * Time.Delta).Clamp( 0f, 0.95f );
			Body.Velocity *= linearDrag;
		}

		if ( WaterAngularDrag > 0f )
		{
			var angularDrag = 1f - (WaterAngularDrag * raftStability.StabilityMultiplier * wetness * Time.Delta).Clamp( 0f, 0.95f );
			Body.AngularVelocity *= angularDrag;
		}
	}

	private void ApplyUprightStabilization( float wetness, RaftStabilityState raftStability )
	{
		if ( wetness <= 0f )
			return;

		// Lightweight roll/pitch stabilization.
		// This helps rafts/blocks calm down without forcing them perfectly upright.
		var up = Body.WorldRotation.Up;
		var correction = Vector3.Cross( up, Vector3.Up );

		Body.AngularVelocity += correction * UprightStabilization * raftStability.StabilityMultiplier * wetness * Time.Delta;
	}

	private RaftStabilityState GetRaftStabilityState()
	{
		if ( !UseAttachedRaftStability )
			return BuildRaftStabilityState( 1, 1f, 1f, 1f );

		if ( !BuildPiece.IsValid() )
			BuildPiece = Components.Get<BuildPiece>( FindMode.EverythingInSelfAndAncestors );

		if ( !BuildPiece.IsValid() )
			return BuildRaftStabilityState( 1, 1f, 1f, 1f );

		var connectedPieces = GetConnectedRaftPieces( BuildPiece );
		var pieceCount = connectedPieces.Count;
		var healthFraction = GetAverageHealthFraction( connectedPieces );

		if ( pieceCount <= 1 )
		{
			var singleLift = healthFraction.Clamp( 0.55f, 1f );
			return BuildRaftStabilityState( 1, healthFraction, UnattachedInstabilityMultiplier, singleLift );
		}

		var groupBonus = 1f + (pieceCount - 1) * AttachedPieceStabilityBonus;
		var damageScale = healthFraction.Clamp( MinDamagedRaftStabilityMultiplier, 1f );
		var stability = (groupBonus * damageScale).Clamp( MinDamagedRaftStabilityMultiplier, MaxRaftStabilityMultiplier );
		var lift = healthFraction.Clamp( 0.35f, 1f );

		return BuildRaftStabilityState( pieceCount, healthFraction, stability, lift );
	}

	private RaftStabilityState BuildRaftStabilityState(
		int pieceCount,
		float healthFraction,
		float stabilityMultiplier,
		float liftMultiplier )
	{
		var maxStability = MaxRaftStabilityMultiplier > 0.05f
			? MaxRaftStabilityMultiplier
			: 0.05f;

		return new RaftStabilityState
		{
			PieceCount = pieceCount,
			HealthFraction = healthFraction.Clamp( 0f, 1f ),
			StabilityMultiplier = stabilityMultiplier.Clamp( 0.05f, maxStability ),
			LiftMultiplier = liftMultiplier.Clamp( 0.05f, 2f )
		};
	}

	private List<BuildPiece> GetConnectedRaftPieces( BuildPiece rootPiece )
	{
		var connectedPieces = new List<BuildPiece>();
		var openPieces = new Queue<BuildPiece>();

		openPieces.Enqueue( rootPiece );

		while ( openPieces.Count > 0 )
		{
			var currentPiece = openPieces.Dequeue();

			if ( !currentPiece.IsValid() )
				continue;

			if ( connectedPieces.Contains( currentPiece ) )
				continue;

			connectedPieces.Add( currentPiece );

			foreach ( var candidate in BuildPiece.All )
			{
				if ( !candidate.IsValid() )
					continue;

				if ( connectedPieces.Contains( candidate ) )
					continue;

				if ( ArePiecesLinked( currentPiece, candidate ) )
					openPieces.Enqueue( candidate );
			}
		}

		return connectedPieces;
	}

	private bool ArePiecesLinked( BuildPiece firstPiece, BuildPiece secondPiece )
	{
		if ( firstPiece.AttachedTo == secondPiece.GameObject )
			return true;

		return secondPiece.AttachedTo == firstPiece.GameObject;
	}

	private float GetAverageHealthFraction( List<BuildPiece> pieces )
	{
		if ( pieces.Count == 0 )
			return 1f;

		var totalHealthFraction = 0f;

		foreach ( var piece in pieces )
		{
			if ( !piece.Health.IsValid() )
			{
				totalHealthFraction += 1f;
				continue;
			}

			if ( piece.Health.MaxHealth <= 0f )
			{
				totalHealthFraction += 1f;
				continue;
			}

			totalHealthFraction += (piece.Health.Health / piece.Health.MaxHealth).Clamp( 0f, 1f );
		}

		return (totalHealthFraction / pieces.Count).Clamp( 0f, 1f );
	}

	private void DrawSampleDebug( Vector3 sample, float surfaceHeight, bool wet )
	{
		var color = wet ? Color.Green : Color.Red;

		var surfacePoint = sample;
		surfacePoint.z = surfaceHeight;

		DebugOverlay.Line(
			sample,
			surfacePoint,
			color,
			0f
		);

		DebugOverlay.Sphere(
			new Sphere( sample, 3f ),
			color,
			0f
		);
	}
}
