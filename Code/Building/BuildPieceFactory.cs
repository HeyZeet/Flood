using Sandbox;

public sealed class BuildPieceFactory : Component
{
	public GameObject SpawnPiece(
		BuildPieceData pieceData,
		Vector3 position,
		Rotation rotation,
		GameObject owner )
	{
		if ( !Networking.IsHost )
			return null;

		if ( !IsValidPieceData( pieceData ) )
			return null;

		var pieceObject = pieceData.Prefab.Clone( position, rotation );

		SetupBuildPieceComponent( pieceObject, pieceData, owner );

		pieceObject.NetworkSpawn();

		Log.Info( $"Placed build piece: {pieceData.DisplayName} at {position}" );

		return pieceObject;
	}

	private bool IsValidPieceData( BuildPieceData pieceData )
	{
		if ( pieceData is null )
		{
			Log.Warning( "BuildPieceFactory tried to spawn a null piece data." );
			return false;
		}

		if ( !pieceData.Prefab.IsValid() )
		{
			Log.Warning( $"{pieceData.DisplayName} has no prefab assigned." );
			return false;
		}

		return true;
	}

	private void SetupBuildPieceComponent( GameObject pieceObject, BuildPieceData pieceData, GameObject owner )
	{
		var buildPiece = pieceObject.Components.Get<BuildPiece>( FindMode.EverythingInSelfAndDescendants );

		if ( !buildPiece.IsValid() )
		{
			Log.Warning( $"Spawned {pieceData.DisplayName}, but it has no BuildPiece component." );
			return;
		}

		buildPiece.ApplyData( pieceData );
		buildPiece.SetOwner( owner );
		buildPiece.MarkPlaced();
	}
}
