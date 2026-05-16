using Sandbox;

public struct BuildPlacementResult
{
	public bool IsValid { get; set; }
	public Vector3 Position { get; set; }
	public Rotation Rotation { get; set; }
	public string Reason { get; set; }

	public static BuildPlacementResult Valid( Vector3 position, Rotation rotation )
	{
		return new BuildPlacementResult
		{
			IsValid = true,
			Position = position,
			Rotation = rotation,
			Reason = ""
		};
	}

	public static BuildPlacementResult Invalid( Vector3 position, Rotation rotation, string reason )
	{
		return new BuildPlacementResult
		{
			IsValid = false,
			Position = position,
			Rotation = rotation,
			Reason = reason
		};
	}
}
