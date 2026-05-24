using Sandbox;

public sealed class BuildPieceFactory : Component
{
	[Property, Group( "Attachment" )]
	public bool AutoAttachNearbyPieces { get; set; } = true;

	[Property, Group( "Attachment" )]
	public float AutoAttachRadius { get; set; } = 65f;

	[Property, Group( "Welding" )]
	public bool CreatePhysicalWelds { get; set; } = true;

	[Property, Group( "Welding" )]
	public float WeldLinearStrength { get; set; } = 25000f;

	[Property, Group( "Welding" )]
	public float WeldAngularStrength { get; set; } = 25000f;

	[Property, Group( "Welding" )]
	public bool EnableWeldedPieceCollisions { get; set; } = false;

	public BuildPieceSpawnResult SpawnPiece(
		BuildPieceData pieceData,
		Vector3 position,
		Rotation rotation,
		GameObject owner )
	{
		if ( !Networking.IsHost )
			return BuildPieceSpawnResult.Failed( "Only the host can spawn build pieces." );

		var validationError = GetPieceDataValidationError( pieceData );

		if ( !string.IsNullOrWhiteSpace( validationError ) )
			return BuildPieceSpawnResult.Failed( validationError );

		var pieceObject = pieceData.Prefab.Clone( position, rotation );
		var buildPiece = SetupBuildPieceComponent( pieceObject, pieceData, owner );

		if ( !buildPiece.IsValid() )
		{
			pieceObject.Destroy();
			return BuildPieceSpawnResult.Failed( $"{pieceData.DisplayName} prefab needs a BuildPiece component." );
		}

		pieceObject.NetworkSpawn();
		TryAttachToNearbyPiece( buildPiece );

		Log.Info( $"Placed build piece: {pieceData.DisplayName} at {position}" );

		return BuildPieceSpawnResult.Succeeded( pieceObject, buildPiece );
	}

	private string GetPieceDataValidationError( BuildPieceData pieceData )
	{
		if ( pieceData is null )
			return "BuildPieceFactory tried to spawn a null piece data.";

		if ( !pieceData.Prefab.IsValid() )
			return $"{pieceData.DisplayName} has no prefab assigned.";

		return "";
	}

	private BuildPiece SetupBuildPieceComponent( GameObject pieceObject, BuildPieceData pieceData, GameObject owner )
	{
		var buildPiece = pieceObject.Components.Get<BuildPiece>( FindMode.EverythingInSelfAndDescendants );

		if ( !buildPiece.IsValid() )
		{
			Log.Warning( $"Spawned {pieceData.DisplayName}, but it has no BuildPiece component." );
			return null;
		}

		ApplyPropPresentation( pieceObject, pieceData );
		ApplyPropPhysics( pieceObject, pieceData );
		ApplyMaterialTags( pieceObject, pieceData.Material );
		buildPiece.ApplyData( pieceData );
		buildPiece.SetOwner( owner );
		buildPiece.MarkPlaced();

		return buildPiece;
	}

	private void ApplyPropPresentation( GameObject pieceObject, BuildPieceData pieceData )
	{
		var renderer = pieceObject.Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndDescendants );

		if ( renderer.IsValid() && pieceData.PropModel.IsValid() )
			renderer.Model = pieceData.PropModel;

		pieceObject.WorldScale = Vector3.One * pieceData.ModelScale.Clamp( 0.05f, 10f );
		pieceObject.Name = pieceData.DisplayName;
	}

	private void ApplyPropPhysics( GameObject pieceObject, BuildPieceData pieceData )
	{
		ApplyModelCollision( pieceObject, pieceData );

		var body = pieceObject.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );

		if ( body.IsValid() )
			body.MassOverride = pieceData.Weight.Clamp( 1f, 10000f );

		var buoyancy = pieceObject.Components.Get<FloodBuoyancy>( FindMode.EverythingInSelfAndDescendants );

		if ( buoyancy.IsValid() )
			ApplyBuoyancyPreset( buoyancy, pieceData.Material );
	}

	private void ApplyModelCollision( GameObject pieceObject, BuildPieceData pieceData )
	{
		if ( !pieceData.PropModel.IsValid() )
			return;

		// The real props ship with authored collision. Keep the old builder box disabled so it
		// does not override the prop's actual shape.
		foreach ( var boxCollider in pieceObject.Components.GetAll<BoxCollider>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( boxCollider.IsValid() )
				boxCollider.Enabled = false;
		}

		var modelCollider = pieceObject.Components.Get<ModelCollider>( FindMode.EverythingInSelfAndDescendants );

		if ( !modelCollider.IsValid() )
			modelCollider = pieceObject.Components.Create<ModelCollider>();

		modelCollider.Model = pieceData.PropModel;
		modelCollider.Enabled = true;
		modelCollider.IsTrigger = false;
	}

	private void ApplyBuoyancyPreset( FloodBuoyancy buoyancy, BuildPieceMaterial material )
	{
		switch ( material )
		{
			case BuildPieceMaterial.Wood:
				buoyancy.MaterialPreset = BuoyancyMaterialPreset.Wood;
				buoyancy.RelativeDensity = 0.65f;
				buoyancy.LiftAcceleration = 900f;
				break;

			case BuildPieceMaterial.Metal:
				buoyancy.MaterialPreset = BuoyancyMaterialPreset.Metal;
				buoyancy.RelativeDensity = 1.8f;
				buoyancy.LiftAcceleration = 650f;
				break;

			case BuildPieceMaterial.Plastic:
				buoyancy.MaterialPreset = BuoyancyMaterialPreset.LightPlastic;
				buoyancy.RelativeDensity = 0.35f;
				buoyancy.LiftAcceleration = 1000f;
				break;

			case BuildPieceMaterial.Armor:
				buoyancy.MaterialPreset = BuoyancyMaterialPreset.Metal;
				buoyancy.RelativeDensity = 1.35f;
				buoyancy.LiftAcceleration = 700f;
				break;
		}
	}

	private void ApplyMaterialTags( GameObject pieceObject, BuildPieceMaterial material )
	{
		pieceObject.Tags.Remove( "wood" );
		pieceObject.Tags.Remove( "metal" );
		pieceObject.Tags.Remove( "plastic" );
		pieceObject.Tags.Remove( "armor" );

		pieceObject.Tags.Add( material.ToString().ToLower() );
	}

	private void TryAttachToNearbyPiece( BuildPiece buildPiece )
	{
		if ( !AutoAttachNearbyPieces )
			return;

		if ( !buildPiece.IsValid() || !buildPiece.CanBeWelded )
			return;

		var nearestPiece = FindNearestAttachablePiece( buildPiece );

		if ( !nearestPiece.IsValid() )
			return;

		if ( CreatePhysicalWelds )
		{
			var weldPosition = (buildPiece.WorldPosition + nearestPiece.WorldPosition) * 0.5f;

			if ( buildPiece.WeldTo(
				nearestPiece,
				weldPosition,
				WeldLinearStrength,
				WeldAngularStrength,
				EnableWeldedPieceCollisions
			) )
			{
				return;
			}

			Log.Warning( $"Failed to create physical weld between {buildPiece.DisplayName} and {nearestPiece.DisplayName}; using logical attachment only." );
		}

		buildPiece.AttachTo( nearestPiece );
	}

	private BuildPiece FindNearestAttachablePiece( BuildPiece buildPiece )
	{
		BuildPiece nearestPiece = null;
		var nearestDistance = AutoAttachRadius;

		foreach ( var otherPiece in BuildPiece.All )
		{
			if ( !otherPiece.IsValid() )
				continue;

			if ( !buildPiece.CanAttachTo( otherPiece ) )
				continue;

			var distance = (buildPiece.WorldPosition - otherPiece.WorldPosition).Length;

			if ( distance > nearestDistance )
				continue;

			nearestDistance = distance;
			nearestPiece = otherPiece;
		}

		return nearestPiece;
	}
}
