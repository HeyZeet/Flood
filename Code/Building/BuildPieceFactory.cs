using Sandbox;

public sealed class BuildPieceFactory : Component
{
	public GameObject SpawnPiece( BuildPieceData pieceData, Vector3 position, Rotation rotation, GameObject owner )
	{
		if ( !Networking.IsHost )
			return null;

		if ( pieceData is null )
		{
			Log.Warning( "BuildPieceFactory tried to spawn a null piece data." );
			return null;
		}

		if ( !pieceData.Prefab.IsValid() )
		{
			Log.Warning( $"{pieceData.DisplayName} has no prefab assigned." );
			return null;
		}

		var pieceObject = pieceData.Prefab.Clone( position, rotation );

		var buildPiece = pieceObject.Components.Get<BuildPiece>( FindMode.EverythingInSelfAndDescendants );

		if ( buildPiece.IsValid() )
		{
			buildPiece.ApplyData( pieceData );
			buildPiece.SetOwner( owner );
			buildPiece.MarkPlaced();
		}
		else
		{
			Log.Warning( $"Spawned {pieceData.DisplayName}, but it has no BuildPiece component." );
		}

		pieceObject.NetworkSpawn();

		Log.Info( $"Placed build piece: {pieceData.DisplayName} at {position}" );

		return pieceObject;
	}
}