using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class FloodNetworkManager : Component, Component.INetworkListener
{
	private readonly Dictionary<Guid, GameObject> SpawnedPlayers = new();

	[Property] public GameObject PlayerPrefab { get; set; }

	[Property, Group( "Spawning" )]
	public bool DestroyUnownedScenePlayersOnStart { get; set; } = true;

	[Property, Group( "Spawning" )]
	public Vector3 FallbackSpawnOffset { get; set; } = Vector3.Up * 8f;

	[Property, Group( "Spawning" )]
	public float SpawnSeparation { get; set; } = 80f;

	[Property, Group( "Debug" )]
	public bool LogDebug { get; set; } = true;

	private bool NeedsScenePlayerCleanup { get; set; }

	protected override void OnStart()
	{
		if ( !Networking.IsHost )
			return;

		NeedsScenePlayerCleanup = DestroyUnownedScenePlayersOnStart;

		LogNetworkState( "started" );
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		if ( !NeedsScenePlayerCleanup )
			return;

		NeedsScenePlayerCleanup = false;
		DestroyUnownedScenePlayers();
	}

	/// <summary>
	/// Called on the host when a connection has finished loading and is ready to play.
	/// </summary>
	public void OnActive( Connection connection )
	{
		if ( !Networking.IsHost )
			return;

		if ( connection is null )
			return;

		if ( DestroyUnownedScenePlayersOnStart )
			DestroyUnownedScenePlayers();

		if ( HasPlayerForConnection( connection ) )
		{
			LogInfo( $"Skipping spawn for {connection.DisplayName}: player already exists." );
			return;
		}

		var playerObject = CreatePlayerForConnection( connection );

		if ( !playerObject.IsValid() )
			return;

		SpawnedPlayers[connection.Id] = playerObject;
		LogInfo( $"Spawned player for {connection.DisplayName} [{connection.Id}] at {playerObject.WorldPosition}." );
	}

	public void OnDisconnected( Connection connection )
	{
		if ( !Networking.IsHost )
			return;

		if ( connection is null )
			return;

		if ( !SpawnedPlayers.TryGetValue( connection.Id, out var playerObject ) )
			return;

		SpawnedPlayers.Remove( connection.Id );

		if ( playerObject.IsValid() )
			playerObject.Destroy();

		LogInfo( $"Removed player for disconnected connection {connection.DisplayName} [{connection.Id}]." );
	}

	private GameObject CreatePlayerForConnection( Connection connection )
	{
		if ( !PlayerPrefab.IsValid() )
		{
			Log.Warning( "FloodNetworkManager needs PlayerPrefab assigned." );
			return null;
		}

		var spawnTransform = GetSpawnTransform( connection );
		var playerObject = PlayerPrefab.Clone( spawnTransform );
		playerObject.Name = string.IsNullOrWhiteSpace( connection.DisplayName )
			? "FloodPlayer"
			: connection.DisplayName;

		if ( !playerObject.NetworkSpawn( connection ) )
		{
			Log.Warning( $"Failed to network spawn player for {connection.DisplayName}." );
			playerObject.Destroy();
			return null;
		}

		var floodPlayer = playerObject.Components.Get<FloodPlayer>( FindMode.EverythingInSelfAndDescendants );

		if ( floodPlayer.IsValid() )
			FloodGameManager.Instance?.RegisterPlayer( floodPlayer );

		return playerObject;
	}

	private Transform GetSpawnTransform( Connection connection )
	{
		var spawnPoints = FloodPlayerSpawnPoint.All
			.Where( spawnPoint => spawnPoint.IsValid() )
			.OrderBy( spawnPoint => spawnPoint.SpawnOrder )
			.ToArray();

		if ( spawnPoints.Length == 0 )
			return new Transform( WorldPosition + FallbackSpawnOffset, WorldRotation );

		var connectionIndex = GetConnectionIndex( connection );
		var spawnPoint = spawnPoints[connectionIndex % spawnPoints.Length];

		var transform = spawnPoint.WorldTransform;
		transform.Position += GetSpawnSeparationOffset( connectionIndex );

		return transform;
	}

	private int GetConnectionIndex( Connection connection )
	{
		var activeConnections = Connection.All
			.Where( existingConnection => existingConnection is not null && existingConnection.IsActive )
			.OrderBy( existingConnection => existingConnection.ConnectionTime )
			.ToList();

		var index = activeConnections.IndexOf( connection );
		return index < 0 ? SpawnedPlayers.Count : index;
	}

	private bool HasPlayerForConnection( Connection connection )
	{
		if ( SpawnedPlayers.TryGetValue( connection.Id, out var playerObject ) && playerObject.IsValid() )
			return true;

		return FloodPlayer.All.Any( player =>
			player.IsValid() &&
			player.GameObject.Network.Active &&
			player.GameObject.Network.OwnerId == connection.Id
		);
	}

	private Vector3 GetSpawnSeparationOffset( int playerIndex )
	{
		if ( SpawnSeparation <= 0f )
			return Vector3.Zero;

		var row = playerIndex / 4;
		var column = playerIndex % 4;
		var x = column - 1.5f;
		var y = row;

		return new Vector3( x * SpawnSeparation, y * SpawnSeparation, 0f );
	}

	private void DestroyUnownedScenePlayers()
	{
		var scenePlayers = FloodPlayer.All
			.Where( player => player.IsValid() )
			.Where( player => !player.GameObject.Network.Active || player.GameObject.Network.Owner is null )
			.ToArray();

		foreach ( var player in scenePlayers )
		{
			LogInfo( $"Destroying unowned scene player template {player.GameObject.Name}." );
			player.GameObject.Destroy();
		}
	}

	private void LogNetworkState( string reason )
	{
		LogInfo( $"FloodNetworkManager {reason}. Connections={Connection.All.Count()}, Players={FloodPlayer.All.Count}" );
	}

	private void LogInfo( string message )
	{
		if ( LogDebug )
			Log.Info( message );
	}
}
