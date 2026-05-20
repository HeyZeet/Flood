using Sandbox;
using System.Collections.Generic;

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

	[Header( "Debug" )]
	[Property] public bool DrawDebug { get; set; } = true;
	[Property] public bool LogDebug { get; set; } = false;

	public float LastWetness { get; private set; }
	public bool IsTouchingWater => LastWetness > 0f;
	public Vector3 LastWaterContactPoint { get; private set; }

	protected override void OnStart()
	{
		if ( !Body.IsValid() )
			Body = Components.Get<Rigidbody>( FindMode.EverythingInSelfAndAncestors );

		if ( !Body.IsValid() )
			Log.Warning( $"{GameObject.Name} has FloodBuoyancy but no Rigidbody." );
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

		var roundManager = FloodRoundManager.Instance;

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

		LastWetness = wholeBodyWetness;

		if ( wholeBodyWetness <= 0f )
			return;

		if ( UseBuiltInApplyBuoyancy )
			Body.ApplyBuoyancy( water.WaterPlane, Time.Delta );

		if ( UseManualSampleBuoyancy )
			ApplySampleBuoyancy( water, bounds );

		ApplyWaterDrag( wholeBodyWetness );

		if ( StabilizeRollPitch )
			ApplyUprightStabilization( wholeBodyWetness );

		if ( LogDebug )
		{
			Log.Info(
				$"{GameObject.Name} wetness:{wholeBodyWetness:0.00} density:{RelativeDensity:0.00} surface:{waterSurfaceAtBody:0.00} bottom:{bottomZ:0.00}"
			);
		}
	}

	private void ApplySampleBuoyancy( FloodWaterController water, BBox bounds )
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

			var liftVelocity = Vector3.Up * LiftAcceleration * depthPercent * densityScale * Time.Delta;

			// Simple point drag from body movement through water.
			var waterFlow = water.GetFlowVelocity( sample );
			var relativeVelocity = Body.Velocity - waterFlow;
			var dragVelocity = -relativeVelocity * PointVelocityDrag * depthPercent * Time.Delta;

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

	private void ApplyWaterDrag( float wetness )
	{
		if ( WaterLinearDrag > 0f )
		{
			var linearDrag = 1f - (WaterLinearDrag * wetness * Time.Delta).Clamp( 0f, 0.95f );
			Body.Velocity *= linearDrag;
		}

		if ( WaterAngularDrag > 0f )
		{
			var angularDrag = 1f - (WaterAngularDrag * wetness * Time.Delta).Clamp( 0f, 0.95f );
			Body.AngularVelocity *= angularDrag;
		}
	}

	private void ApplyUprightStabilization( float wetness )
	{
		if ( wetness <= 0f )
			return;

		// Lightweight roll/pitch stabilization.
		// This helps rafts/blocks calm down without forcing them perfectly upright.
		var up = Body.WorldRotation.Up;
		var correction = Vector3.Cross( up, Vector3.Up );

		Body.AngularVelocity += correction * UprightStabilization * wetness * Time.Delta;
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