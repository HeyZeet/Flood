using Sandbox;
using System;
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
	[Property] public float ConnectedRaftAngularDragBonus { get; set; } = 1.25f;
	[Property] public float MaxWaterVelocity { get; set; } = 420f;
	[Property] public float MaxWaterAngularVelocity { get; set; } = 3f;

	[Header( "Stability" )]
	[Property] public bool StabilizeRollPitch { get; set; } = true;
	[Property] public float UprightStabilization { get; set; } = 0.15f;

	[Header( "Raft Stability" )]
	[Property] public bool UseAttachedRaftStability { get; set; } = true;
	[Property] public bool UseGroupBuoyancyForRafts { get; set; } = true;
	[Property] public float GroupLiftAcceleration { get; set; } = 700f;
	[Property] public float GroupFloatTargetWetness { get; set; } = 0.45f;
	[Property] public float GroupFloatCorrectionStrength { get; set; } = 350f;
	[Property] public float GroupVerticalVelocityDamping { get; set; } = 4.5f;
	[Property] public float GroupMaxUpwardVelocity { get; set; } = 95f;
	[Property] public float GroupMaxDownwardVelocity { get; set; } = 220f;
	[Property] public float GroupUprightStabilization { get; set; } = 0.08f;
	[Property] public float AttachedPieceStabilityBonus { get; set; } = 0.08f;
	[Property] public float AttachedPieceLiftBonus { get; set; } = 0.035f;
	[Property] public float AttachedArmorStabilityBonus { get; set; } = 0.03f;
	[Property] public float MaxFootprintStabilityBonus { get; set; } = 0.45f;
	[Property] public float FootprintSizeForMaxBonus { get; set; } = 220f;
	[Property] public float MaxRaftStabilityMultiplier { get; set; } = 2.25f;
	[Property] public float MaxRaftLiftMultiplier { get; set; } = 1.45f;
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
	[Sync( SyncFlags.FromHost )] public bool IsInWater { get; private set; }
	[Sync( SyncFlags.FromHost )] public float SyncedWetness { get; private set; }
    private bool WasTouchingWater { get; set; }
    private TimeSince TimeSinceLastSplash { get; set; } = 999f;
	private BuildPiece BuildPiece { get; set; }

	private struct RaftStabilityState
	{
		public int PieceCount { get; set; }
		public int BoatPartCount { get; set; }
		public int ArmorPieceCount { get; set; }
		public float HealthFraction { get; set; }
		public float StabilityMultiplier { get; set; }
		public float LiftMultiplier { get; set; }
	}

	protected override void OnStart()
	{
		if ( !Body.IsValid() )
			Body = Components.Get<Rigidbody>( FindMode.EverythingInSelfAndAncestors );

		if ( !Body.IsValid() )
			Log.Warning( $"{GameObject.Name} has FloodBuoyancy but no Rigidbody." );

		BuildPiece = Components.Get<BuildPiece>( FindMode.EverythingInSelfAndAncestors );

		if ( ApplyPresetOnStart )
			FloodBuoyancyMaterialPresets.ApplyTo( this, MaterialPreset );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost )
			return;

		if ( !Body.IsValid() )
			return;

		var water = FloodWaterController.Instance;

		if ( !water.IsValid() )
			return;

		if ( !ShouldApplyBuoyancy() )
			return;

		if ( SuppressWeldedChildBuoyancy() )
			return;

		if ( TrySimulateGroupRaftBuoyancy( water ) )
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

	private bool SuppressWeldedChildBuoyancy()
	{
		if ( !BuildPiece.IsValid() )
			BuildPiece = Components.Get<BuildPiece>( FindMode.EverythingInSelfAndAncestors );

		if ( !BuildPiece.IsValid() )
			return false;

		if ( BuildPiece.GetWeldRoot() == BuildPiece )
			return false;

		if ( Body.IsValid() )
		{
			Body.Velocity = Vector3.Zero;
			Body.AngularVelocity = Vector3.Zero;

			if ( Body.MotionEnabled )
				Body.MotionEnabled = false;
		}

		return true;
	}

	private bool TrySimulateGroupRaftBuoyancy( FloodWaterController water )
	{
		if ( !UseGroupBuoyancyForRafts || !UseAttachedRaftStability )
			return false;

		if ( !BuildPiece.IsValid() )
			BuildPiece = Components.Get<BuildPiece>( FindMode.EverythingInSelfAndAncestors );

		if ( !BuildPiece.IsValid() )
			return false;

		var connectedPieces = GetConnectedRaftPieces( BuildPiece );

		if ( connectedPieces.Count <= 1 )
			return false;

		if ( !IsRaftLeader() )
		{
			SetDryStateIfOutOfWater( water );
			return true;
		}

		SimulateGroupRaftBuoyancy( water, connectedPieces );
		return true;
	}

	private bool IsRaftLeader()
	{
		return BuildPiece.IsValid() && BuildPiece.GetWeldRoot() == BuildPiece;
	}

	private void SimulateGroupRaftBuoyancy( FloodWaterController water, List<BuildPiece> connectedPieces )
	{
		if ( !TryGetRaftWorldBounds( connectedPieces, out var raftBounds ) )
			return;

		var bottomZ = raftBounds.Mins.z;
		var topZ = raftBounds.Maxs.z;
		var height = (topZ - bottomZ).Clamp( 1f, 999999f );
		var waterSurface = water.GetSurfaceHeight( raftBounds.Center );
		var submergedHeight = (waterSurface - bottomZ).Clamp( 0f, height );
		var wetness = (submergedHeight / height).Clamp( 0f, 1f );
		var depthPercent = ((waterSurface - bottomZ) / MaxSampleDepth.Clamp( 1f, 10000f )).Clamp( 0f, 1f );
		var raftStability = BuildRaftStabilityStateFromPieces( connectedPieces );

		UpdateConnectedBuoyancyState( connectedPieces, wetness, raftStability, waterSurface );

		if ( wetness <= 0f )
			return;

		var materialLift = GetAverageRaftFloatScore( connectedPieces );
		var targetWetness = GetTargetWetness( materialLift );
		var floatError = (wetness - targetWetness).Clamp( 0f, 1f );
		var liftAcceleration = GroupLiftAcceleration * materialLift * depthPercent * raftStability.LiftMultiplier;
		liftAcceleration += GroupFloatCorrectionStrength * floatError * raftStability.StabilityMultiplier;
		var liftVelocity = Vector3.Up * liftAcceleration * Time.Delta;

		foreach ( var body in GetActiveRaftBodies( connectedPieces ) )
		{
			var flow = water.GetFlowVelocity( body.WorldPosition );
			var relativeVelocity = body.Velocity - flow;
			var linearDrag = 1f - (WaterLinearDrag * raftStability.StabilityMultiplier * wetness * Time.Delta).Clamp( 0f, 0.95f );
			var angularDrag = 1f - ((WaterAngularDrag + ConnectedRaftAngularDragBonus) * raftStability.StabilityMultiplier * wetness * Time.Delta).Clamp( 0f, 0.95f );
			var verticalDamping = Vector3.Up * body.Velocity.z * GroupVerticalVelocityDamping * wetness * Time.Delta;

			body.Velocity += liftVelocity;
			body.Velocity -= verticalDamping;
			body.Velocity -= relativeVelocity * PointVelocityDrag * wetness * Time.Delta;
			body.Velocity *= linearDrag;
			body.AngularVelocity *= angularDrag;

			if ( StabilizeRollPitch )
			{
				var correction = Vector3.Cross( body.WorldRotation.Up, Vector3.Up );
				body.AngularVelocity += correction * GroupUprightStabilization * raftStability.StabilityMultiplier * wetness * Time.Delta;
			}

			ClampBodyWaterMotion( body, wetness, raftStability, true );
			ClampGroupVerticalVelocity( body );
		}

		if ( LogDebug )
		{
			Log.Info(
				$"{GameObject.Name} group raft wetness:{wetness:0.00} target:{targetWetness:0.00} pieces:{raftStability.PieceCount} boatParts:{raftStability.BoatPartCount} lift:{raftStability.LiftMultiplier:0.00} stability:{raftStability.StabilityMultiplier:0.00} materialLift:{materialLift:0.00} liftAccel:{liftAcceleration:0.00}"
			);
		}
	}

	private IEnumerable<Rigidbody> GetActiveRaftBodies( List<BuildPiece> connectedPieces )
	{
		var activeBodies = new HashSet<Rigidbody>();

		foreach ( var piece in connectedPieces )
		{
			if ( !piece.IsValid() || !piece.Rigidbody.IsValid() )
				continue;

			var body = piece.Rigidbody;

			// Structural welds parent child pieces to a carrier body and disable their rigidbody motion.
			// Only the moving carrier bodies should receive raft-level lift; pushing disabled child bodies
			// can stack stored velocity and produce violent launches when physics wakes them.
			if ( !body.MotionEnabled )
				continue;

			if ( activeBodies.Add( body ) )
				yield return body;
		}
	}

	private void ClampGroupVerticalVelocity( Rigidbody body )
	{
		var velocity = body.Velocity;
		var maxUp = GroupMaxUpwardVelocity.Clamp( 1f, 10000f );
		var maxDown = GroupMaxDownwardVelocity.Clamp( 1f, 10000f );

		if ( velocity.z > maxUp )
			body.Velocity = velocity.WithZ( maxUp );
		else if ( velocity.z < -maxDown )
			body.Velocity = velocity.WithZ( -maxDown );
	}

	private float GetTargetWetness( float materialLift )
	{
		var target = GroupFloatTargetWetness;

		if ( materialLift >= 1f )
			target -= (materialLift - 1f) * 0.12f;
		else
			target += (1f - materialLift) * 0.2f;

		return target.Clamp( 0.25f, 0.75f );
	}

	private void SetDryStateIfOutOfWater( FloodWaterController water )
	{
		var bounds = Body.GetWorldBounds();
		var waterSurface = water.GetSurfaceHeight( Body.WorldPosition );
		var wetness = ((waterSurface - bounds.Mins.z) / (bounds.Maxs.z - bounds.Mins.z).Clamp( 1f, 999999f )).Clamp( 0f, 1f );

		LastWetness = wetness;
		SyncedWetness = wetness;
		IsInWater = wetness > 0f;

		if ( wetness <= 0f )
			WasTouchingWater = false;
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
		SyncedWetness = wholeBodyWetness;
		IsInWater = wholeBodyWetness > 0f;
		LastRaftPieceCount = raftStability.PieceCount;
		LastRaftHealthFraction = raftStability.HealthFraction;

        if ( wholeBodyWetness <= 0f )
        {
	        WasTouchingWater = false;
	        IsInWater = false;
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

		ClampWaterMotion( wholeBodyWetness, raftStability );

		if ( LogDebug )
		{
			Log.Info(
				$"{GameObject.Name} wetness:{wholeBodyWetness:0.00} density:{RelativeDensity:0.00} raftPieces:{raftStability.PieceCount} boatParts:{raftStability.BoatPartCount} armor:{raftStability.ArmorPieceCount} raftHealth:{raftStability.HealthFraction:0.00} lift:{raftStability.LiftMultiplier:0.00} stability:{raftStability.StabilityMultiplier:0.00} surface:{waterSurfaceAtBody:0.00} bottom:{bottomZ:0.00}"
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
			var connectedRaftDrag = raftStability.PieceCount > 1 ? ConnectedRaftAngularDragBonus : 0f;
			var angularDragAmount = WaterAngularDrag + connectedRaftDrag;
			var angularDrag = 1f - (angularDragAmount * raftStability.StabilityMultiplier * wetness * Time.Delta).Clamp( 0f, 0.95f );
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

	private void ClampWaterMotion( float wetness, RaftStabilityState raftStability )
	{
		ClampBodyWaterMotion( Body, wetness, raftStability, raftStability.PieceCount > 1 );
	}

	private void ClampBodyWaterMotion( Rigidbody body, float wetness, RaftStabilityState raftStability, bool connectedRaft )
	{
		if ( wetness <= 0f )
			return;

		var maxVelocity = MaxWaterVelocity.Clamp( 1f, 10000f );
		var maxAngularVelocity = MaxWaterAngularVelocity.Clamp( 0.1f, 1000f );

		if ( connectedRaft )
		{
			maxVelocity *= 0.85f;
			maxAngularVelocity *= 0.5f;
		}

		if ( body.Velocity.Length > maxVelocity )
			body.Velocity = body.Velocity.Normal * maxVelocity;

		if ( body.AngularVelocity.Length > maxAngularVelocity )
			body.AngularVelocity = body.AngularVelocity.Normal * maxAngularVelocity;
	}

	private RaftStabilityState GetRaftStabilityState()
	{
		if ( !UseAttachedRaftStability )
			return BuildRaftStabilityState( 1, 1, 0, 1f, 1f, 1f );

		if ( !BuildPiece.IsValid() )
			BuildPiece = Components.Get<BuildPiece>( FindMode.EverythingInSelfAndAncestors );

		if ( !BuildPiece.IsValid() )
			return BuildRaftStabilityState( 1, 1, 0, 1f, 1f, 1f );

		var connectedPieces = GetConnectedRaftPieces( BuildPiece );
		var pieceCount = connectedPieces.Count;
		return BuildRaftStabilityStateFromPieces( connectedPieces );
	}

	private RaftStabilityState BuildRaftStabilityStateFromPieces( List<BuildPiece> connectedPieces )
	{
		var pieceCount = connectedPieces.Count;
		var boatPartCount = GetBoatPartCount( connectedPieces );
		var armorPieceCount = GetArmorPieceCount( connectedPieces );
		var healthFraction = GetAverageHealthFraction( connectedPieces );

		if ( pieceCount <= 1 )
		{
			var singleLift = BuildPiece.CountsAsBoatPart
				? healthFraction.Clamp( 0.55f, 1f )
				: (healthFraction * 0.25f).Clamp( 0.1f, 0.35f );

			return BuildRaftStabilityState( 1, boatPartCount, armorPieceCount, healthFraction, UnattachedInstabilityMultiplier, singleLift );
		}

		var floatingParts = Math.Max( boatPartCount, 1 );
		var groupBonus = 1f + (floatingParts - 1) * AttachedPieceStabilityBonus;
		var armorBonus = armorPieceCount * AttachedArmorStabilityBonus;
		var footprintBonus = GetFootprintStabilityBonus( connectedPieces );
		var liftBonus = 1f + (floatingParts - 1) * AttachedPieceLiftBonus;
		var damageScale = healthFraction.Clamp( MinDamagedRaftStabilityMultiplier, 1f );
		var stability = ((groupBonus + armorBonus + footprintBonus) * damageScale).Clamp( MinDamagedRaftStabilityMultiplier, MaxRaftStabilityMultiplier );
		var lift = (liftBonus * damageScale).Clamp( 0.35f, MaxRaftLiftMultiplier );

		return BuildRaftStabilityState( pieceCount, boatPartCount, armorPieceCount, healthFraction, stability, lift );
	}

	private RaftStabilityState BuildRaftStabilityState(
		int pieceCount,
		int boatPartCount,
		int armorPieceCount,
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
			BoatPartCount = boatPartCount,
			ArmorPieceCount = armorPieceCount,
			HealthFraction = healthFraction.Clamp( 0f, 1f ),
			StabilityMultiplier = stabilityMultiplier.Clamp( 0.05f, maxStability ),
			LiftMultiplier = liftMultiplier.Clamp( 0.05f, MaxRaftLiftMultiplier.Clamp( 0.05f, 10f ) )
		};
	}

	private List<BuildPiece> GetConnectedRaftPieces( BuildPiece rootPiece )
	{
		if ( !rootPiece.IsValid() )
			return new List<BuildPiece>();

		return rootPiece.GetWeldedPieces();
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

	private int GetBoatPartCount( List<BuildPiece> pieces )
	{
		var count = 0;

		foreach ( var piece in pieces )
		{
			if ( piece.IsValid() && piece.CountsAsBoatPart )
				count++;
		}

		return count;
	}

	private int GetArmorPieceCount( List<BuildPiece> pieces )
	{
		var count = 0;

		foreach ( var piece in pieces )
		{
			if ( piece.IsValid() && piece.Material == BuildPieceMaterial.Armor )
				count++;
		}

		return count;
	}

	private float GetFootprintStabilityBonus( List<BuildPiece> pieces )
	{
		if ( pieces.Count <= 1 )
			return 0f;

		var hasBounds = false;
		var raftBounds = new BBox();

		foreach ( var piece in pieces )
		{
			if ( !piece.IsValid() || !piece.Rigidbody.IsValid() )
				continue;

			var pieceBounds = piece.Rigidbody.GetWorldBounds();

			if ( !hasBounds )
			{
				raftBounds = pieceBounds;
				hasBounds = true;
				continue;
			}

			raftBounds = raftBounds.AddBBox( pieceBounds );
		}

		if ( !hasBounds )
			return 0f;

		var horizontalSize = new Vector2( raftBounds.Size.x, raftBounds.Size.y ).Length;
		var footprintFraction = (horizontalSize / FootprintSizeForMaxBonus.Clamp( 1f, 10000f )).Clamp( 0f, 1f );

		return MaxFootprintStabilityBonus * footprintFraction;
	}

	private bool TryGetRaftWorldBounds( List<BuildPiece> pieces, out BBox raftBounds )
	{
		var hasBounds = false;
		raftBounds = new BBox();

		foreach ( var piece in pieces )
		{
			if ( !piece.IsValid() || !piece.Rigidbody.IsValid() )
				continue;

			var bounds = piece.Rigidbody.GetWorldBounds();

			if ( !hasBounds )
			{
				raftBounds = bounds;
				hasBounds = true;
				continue;
			}

			raftBounds = raftBounds.AddBBox( bounds );
		}

		return hasBounds;
	}

	private float GetAverageRaftFloatScore( List<BuildPiece> pieces )
	{
		var total = 0f;
		var count = 0;

		foreach ( var piece in pieces )
		{
			if ( !piece.IsValid() )
				continue;

			total += GetMaterialFloatScore( piece );
			count++;
		}

		if ( count <= 0 )
			return 1f;

		return (total / count).Clamp( 0.35f, 1.2f );
	}

	private float GetMaterialFloatScore( BuildPiece piece )
	{
		if ( !piece.CountsAsBoatPart )
			return 0.35f;

		return piece.Material switch
		{
			BuildPieceMaterial.Plastic => 1.15f,
			BuildPieceMaterial.Wood => 0.95f,
			BuildPieceMaterial.Foam => 1.2f,
			BuildPieceMaterial.Metal => 0.45f,
			BuildPieceMaterial.Armor => 0.35f,
			_ => 0.8f
		};
	}

	private void UpdateConnectedBuoyancyState(
		List<BuildPiece> pieces,
		float wetness,
		RaftStabilityState raftStability,
		float waterSurface )
	{
		foreach ( var piece in pieces )
		{
			if ( !piece.IsValid() )
				continue;

			var buoyancy = piece.Components.Get<FloodBuoyancy>( FindMode.EverythingInSelfAndDescendants );

			if ( !buoyancy.IsValid() )
				continue;

			buoyancy.LastWetness = wetness;
			buoyancy.SyncedWetness = wetness;
			buoyancy.IsInWater = wetness > 0f;
			buoyancy.LastRaftPieceCount = raftStability.PieceCount;
			buoyancy.LastRaftHealthFraction = raftStability.HealthFraction;
			buoyancy.LastWaterContactPoint = new Vector3( piece.WorldPosition.x, piece.WorldPosition.y, waterSurface );

			if ( wetness <= 0f )
				buoyancy.WasTouchingWater = false;
		}
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
