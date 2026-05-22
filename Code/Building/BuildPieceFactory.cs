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

		buildPiece.ApplyData( pieceData );
		buildPiece.SetOwner( owner );
		buildPiece.MarkPlaced();

		return buildPiece;
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
