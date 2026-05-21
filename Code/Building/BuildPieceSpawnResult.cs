using Sandbox;

public readonly struct BuildPieceSpawnResult
{
	public bool Success { get; }
	public GameObject PieceObject { get; }
	public BuildPiece BuildPiece { get; }
	public string Reason { get; }

	private BuildPieceSpawnResult( bool success, GameObject pieceObject, BuildPiece buildPiece, string reason )
	{
		Success = success;
		PieceObject = pieceObject;
		BuildPiece = buildPiece;
		Reason = reason;
	}

	public static BuildPieceSpawnResult Succeeded( GameObject pieceObject, BuildPiece buildPiece )
	{
		return new BuildPieceSpawnResult( true, pieceObject, buildPiece, "" );
	}

	public static BuildPieceSpawnResult Failed( string reason )
	{
		return new BuildPieceSpawnResult( false, null, null, reason );
	}
}
