public static class FloodBuoyancyMaterialPresets
{
	public static void ApplyTo( FloodBuoyancy buoyancy, BuoyancyMaterialPreset preset )
	{
		if ( buoyancy is null || preset == BuoyancyMaterialPreset.Custom )
			return;

		buoyancy.SampleGridSize = 3;
		buoyancy.HullSampleSpread = 0.78f;
		buoyancy.SampleHeightFraction = 0.12f;
		buoyancy.WaveTransportStrength = 0.15f;
		buoyancy.WaveTransportForce = 8f;
		buoyancy.GroupSampleTorqueStrength = 0f;
		buoyancy.GroupUprightStabilization = 8f;
		buoyancy.GroupUprightAngularDamping = 1.5f;
		buoyancy.GroupMaxUprightCorrection = 16f;
		buoyancy.GroupUpsideDownRecoveryMultiplier = 3.5f;
		buoyancy.GroupUpsideDownRecoveryThreshold = 0.1f;
		buoyancy.UseRaftAngularAssist = true;
		buoyancy.RaftAngularAssistStrength = 2.5f;

		switch ( preset )
		{
			case BuoyancyMaterialPreset.LightPlastic:
				buoyancy.RelativeDensity = 0.5f;
				buoyancy.LiftAcceleration = 850f;
				buoyancy.Damping = 6f;
				buoyancy.MaxSampleDepth = 40f;
				buoyancy.WaterLinearDrag = 0.7f;
				buoyancy.WaterAngularDrag = 1.8f;
				buoyancy.PointVelocityDrag = 0.2f;
				buoyancy.UprightStabilization = 0.1f;
				break;

			case BuoyancyMaterialPreset.Wood:
				buoyancy.RelativeDensity = 0.65f;
				buoyancy.LiftAcceleration = 820f;
				buoyancy.Damping = 5.5f;
				buoyancy.MaxSampleDepth = 48f;
				buoyancy.WaterLinearDrag = 0.8f;
				buoyancy.WaterAngularDrag = 1.5f;
				buoyancy.PointVelocityDrag = 0.15f;
				buoyancy.UprightStabilization = 0.15f;
				break;

			case BuoyancyMaterialPreset.HeavyWood:
				buoyancy.RelativeDensity = 0.9f;
				buoyancy.LiftAcceleration = 850f;
				buoyancy.Damping = 6f;
				buoyancy.MaxSampleDepth = 56f;
				buoyancy.WaterLinearDrag = 0.9f;
				buoyancy.WaterAngularDrag = 1.7f;
				buoyancy.PointVelocityDrag = 0.18f;
				buoyancy.UprightStabilization = 0.12f;
				break;

			case BuoyancyMaterialPreset.Metal:
				buoyancy.RelativeDensity = 1.8f;
				buoyancy.LiftAcceleration = 650f;
				buoyancy.Damping = 6.5f;
				buoyancy.MaxSampleDepth = 64f;
				buoyancy.WaterLinearDrag = 1.0f;
				buoyancy.WaterAngularDrag = 2.0f;
				buoyancy.PointVelocityDrag = 0.22f;
				buoyancy.UprightStabilization = 0.05f;
				break;

			case BuoyancyMaterialPreset.Stone:
				buoyancy.RelativeDensity = 2.4f;
				buoyancy.LiftAcceleration = 500f;
				buoyancy.Damping = 7f;
				buoyancy.MaxSampleDepth = 64f;
				buoyancy.WaterLinearDrag = 1.1f;
				buoyancy.WaterAngularDrag = 2.2f;
				buoyancy.PointVelocityDrag = 0.25f;
				buoyancy.UprightStabilization = 0.02f;
				break;
		}
	}
}
