using Sandbox;
using System.Collections.Generic;

public sealed class FloodPlayerSpawnPoint : Component
{
	private static readonly List<FloodPlayerSpawnPoint> SpawnPoints = new();

	public static IReadOnlyList<FloodPlayerSpawnPoint> All => SpawnPoints;

	[Property] public int SpawnOrder { get; set; }

	protected override void OnStart()
	{
		if ( !SpawnPoints.Contains( this ) )
			SpawnPoints.Add( this );
	}

	protected override void OnDestroy()
	{
		SpawnPoints.Remove( this );
	}
}
