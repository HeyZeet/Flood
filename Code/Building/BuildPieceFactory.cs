using Sandbox;

public sealed class BuildPieceFactory : Component
{
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
}
