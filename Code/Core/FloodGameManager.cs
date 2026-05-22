using Sandbox;
using System;
using System.Linq;

public sealed class FloodGameManager : Component
{
	public static FloodGameManager Instance { get; private set; }

	public event Action<GamePhase, GamePhase> OnPhaseChanged;
	public event Action OnBuildPhaseStarted;
	public event Action OnFloodPhaseStarted;
	public event Action OnCombatPhaseStarted;
	public event Action OnBattlePhaseStarted;
	public event Action OnRoundEndPhaseStarted;

	[Sync] public GamePhase CurrentPhase { get; private set; } = GamePhase.BuildPhase;
	[Sync] public float SyncedPhaseTimeRemaining { get; private set; }

	[Property, Group( "Round" )]
	public bool AutoRunRoundLoop { get; set; } = true;

	[Property, Group( "Round" )]
	public bool StartInBuildPhase { get; set; } = true;

	[Property, Group( "Round" )]
	public bool AutoStartWhenEnoughPlayers { get; set; } = true;

	[Property, Group( "Round" )]
	public int MinPlayersToStart { get; set; } = 1;

	[Property, Group( "Player Reset" )]
	public bool MovePlayersToSpawnPointsOnReset { get; set; } = true;

	[Property, Group( "Player Reset" )]
	public Vector3 FallbackSpawnOffset { get; set; } = Vector3.Up * 8f;

	[Property, Group( "Player Reset" )]
	public bool ResetPlayersWhenManagerStarts { get; set; } = true;

	[Property, Group( "Player Reset" )]
	public bool SpawnLateJoinersDuringBuildPhase { get; set; } = true;

	[Property, Group( "Round Timers" )]
	public float BuildDuration { get; set; } = 60f;

	[Property, Group( "Round Timers" )]
	public float FloodDuration { get; set; } = 20f;

	[Property, Group( "Round Timers" )]
	public float CombatDuration { get; set; } = 120f;

	[Property, Group( "Round Timers" )]
	public float RoundEndDuration { get; set; } = 8f;

	[Property, Group( "Debug" )]
	public bool EnableDebugControls { get; set; } = true;

	[Property, Group( "Debug" )]
	public bool EnableDebugPhaseKeys { get; set; } = true;

	[Property, Group( "Debug" )]
	public bool EnableDebugResetKey { get; set; } = true;

	[Property, Group( "Debug" )]
	public bool EnableDebugDamageKey { get; set; } = true;

	[Property, Group( "Debug" )]
	public float DebugPlayerDamageAmount { get; set; } = 25f;

	[Property, Group( "Debug" )]
	public bool LogRoundLoopDiagnostics { get; set; } = false;

	private float PhaseElapsedSeconds { get; set; }

	protected override void OnStart()
	{
		Instance = this;

		if ( Networking.IsHost )
		{
			var startingPhase = StartInBuildPhase ? GamePhase.BuildPhase : GamePhase.WaitingForPlayers;
			SetPhase( startingPhase, true );

			if ( ResetPlayersWhenManagerStarts )
				ResetPlayers();
		}

		Log.Info( $"Flood game manager started. Host={Networking.IsHost}, Phase={CurrentPhase}, AutoRun={AutoRunRoundLoop}" );
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		AdvancePhaseTimer();
		HandleRoundLoop();
		UpdateSyncedPhaseTime();
		HandleDebugControls();
	}

	public bool IsBuildPhase()
	{
		return CurrentPhase == GamePhase.BuildPhase;
	}

	public bool IsFloodPhase()
	{
		return CurrentPhase == GamePhase.FloodPhase;
	}

	public bool IsBattlePhase()
	{
		return IsCombatPhase();
	}

	public bool IsCombatPhase()
	{
		return CurrentPhase == GamePhase.CombatPhase;
	}

	public bool IsWaitingPhase()
	{
		return CurrentPhase == GamePhase.WaitingForPlayers;
	}

	public bool IsRoundEndPhase()
	{
		return CurrentPhase == GamePhase.RoundEnd;
	}

	public float GetCurrentPhaseDuration()
	{
		return GetPhaseDuration( CurrentPhase );
	}

	public float GetCurrentPhaseTimeElapsed()
	{
		return PhaseElapsedSeconds;
	}

	public float GetCurrentPhaseTimeRemaining()
	{
		if ( !Networking.IsHost )
			return SyncedPhaseTimeRemaining;

		var duration = GetCurrentPhaseDuration();

		if ( duration <= 0f )
			return 0f;

		return (duration - PhaseElapsedSeconds).Clamp( 0f, duration );
	}

	public void SetPhase( GamePhase phase, bool force = false )
	{
		if ( !Networking.IsHost )
			return;

		if ( !force && CurrentPhase == phase )
			return;

		var previousPhase = CurrentPhase;

		CurrentPhase = phase;
		PhaseElapsedSeconds = 0f;
		UpdateSyncedPhaseTime();

		Log.Info( $"Round phase changed: {previousPhase} -> {CurrentPhase} (duration: {GetCurrentPhaseDuration():0.##}s)" );

		HandleWaterForPhase( CurrentPhase );
		NotifyPhaseChanged( previousPhase, CurrentPhase );
	}

	public void StartWaitingForPlayers()
	{
		SetPhase( GamePhase.WaitingForPlayers );
	}

	public void StartBuildPhase()
	{
		SetPhase( GamePhase.BuildPhase );
	}

	public void StartFloodPhase()
	{
		SetPhase( GamePhase.FloodPhase );
	}

	public void StartCombatPhase()
	{
		SetPhase( GamePhase.CombatPhase );
	}

	public void ResetRound()
	{
		if ( !Networking.IsHost )
			return;

		Log.Info( "Resetting round." );

		DeletePlacedBuildPieces();
		ResetPlayers();

		SetPhase( GamePhase.BuildPhase, true );

		Log.Info( "Round reset complete." );
	}

	public void RegisterPlayer( FloodPlayer player )
	{
		if ( !Networking.IsHost )
			return;

		if ( !player.IsValid() )
			return;

		if ( !ShouldResetJoiningPlayer() )
		{
			Log.Info( $"Registered player {player.GameObject.Name} without reset. Phase={CurrentPhase}" );
			return;
		}

		ResetPlayerForCurrentRound( player );
		Log.Info( $"Registered and reset joining player {player.GameObject.Name}. Phase={CurrentPhase}" );
	}

	public void EndRound()
	{
		if ( !Networking.IsHost )
			return;

		SetPhase( GamePhase.RoundEnd );
	}

	private void HandleWaterForPhase( GamePhase phase )
	{
		var water = FloodWaterController.Instance;

		if ( !water.IsValid() )
		{
			Log.Warning( "FloodGameManager could not find FloodWaterController.Instance." );
			return;
		}

		switch ( phase )
		{
			case GamePhase.BuildPhase:
				water.ResetWater();
				break;

			case GamePhase.FloodPhase:
				water.StartFlood();
				break;

			case GamePhase.CombatPhase:
				water.StopFlood();
				break;

			case GamePhase.RoundEnd:
				water.StartDrain();
				break;

			case GamePhase.WaitingForPlayers:
				water.ResetWater();
				break;
		}
	}

	private void NotifyPhaseChanged( GamePhase previousPhase, GamePhase newPhase )
	{
		OnPhaseChanged?.Invoke( previousPhase, newPhase );

		switch ( newPhase )
		{
			case GamePhase.BuildPhase:
				OnBuildPhaseStarted?.Invoke();
				break;

			case GamePhase.FloodPhase:
				OnFloodPhaseStarted?.Invoke();
				break;

			case GamePhase.CombatPhase:
				OnCombatPhaseStarted?.Invoke();
				OnBattlePhaseStarted?.Invoke();
				break;

			case GamePhase.RoundEnd:
				OnRoundEndPhaseStarted?.Invoke();
				break;
		}
	}

	private void HandleRoundLoop()
	{
		if ( !AutoRunRoundLoop )
			return;

		if ( LogRoundLoopDiagnostics )
		{
			Log.Info(
				$"Round loop. Phase={CurrentPhase}, Elapsed={PhaseElapsedSeconds:0.00}, Remaining={GetCurrentPhaseTimeRemaining():0.00}, Players={FloodPlayer.All.Count}"
			);
		}

		switch ( CurrentPhase )
		{
			case GamePhase.WaitingForPlayers:
				if ( AutoStartWhenEnoughPlayers && HasEnoughPlayersToStart() )
					StartBuildPhase();
				break;

			case GamePhase.BuildPhase:
				if ( HasPhaseExpired() )
				{
					Log.Info( "Build phase expired. Starting flood phase." );
					StartFloodPhase();
				}
				break;

			case GamePhase.FloodPhase:
				if ( HasPhaseExpired() )
				{
					Log.Info( "Flood phase expired. Starting combat phase." );
					StartCombatPhase();
				}
				break;

			case GamePhase.CombatPhase:
				if ( HasPhaseExpired() )
				{
					Log.Info( "Combat phase expired. Ending round." );
					EndRound();
				}
				break;

			case GamePhase.RoundEnd:
				if ( HasPhaseExpired() )
				{
					Log.Info( "Round end phase expired. Resetting round." );
					ResetRound();
				}
				break;
		}
	}

	private bool HasPhaseExpired()
	{
		var duration = GetCurrentPhaseDuration();

		if ( duration <= 0f )
			return false;

		return PhaseElapsedSeconds >= duration;
	}

	private void AdvancePhaseTimer()
	{
		var duration = GetCurrentPhaseDuration();

		if ( duration <= 0f )
		{
			PhaseElapsedSeconds = 0f;
			return;
		}

		PhaseElapsedSeconds += Time.Delta;
		PhaseElapsedSeconds = PhaseElapsedSeconds.Clamp( 0f, duration );
	}

	private void UpdateSyncedPhaseTime()
	{
		SyncedPhaseTimeRemaining = CalculateCurrentPhaseTimeRemaining();
	}

	private float CalculateCurrentPhaseTimeRemaining()
	{
		var duration = GetCurrentPhaseDuration();

		if ( duration <= 0f )
			return 0f;

		return (duration - PhaseElapsedSeconds).Clamp( 0f, duration );
	}

	private bool HasEnoughPlayersToStart()
	{
		return FloodPlayer.All.Count >= MinPlayersToStart;
	}

	private float GetPhaseDuration( GamePhase phase )
	{
		return phase switch
		{
			GamePhase.BuildPhase => BuildDuration,
			GamePhase.FloodPhase => FloodDuration,
			GamePhase.CombatPhase => CombatDuration,
			GamePhase.RoundEnd => RoundEndDuration,
			_ => 0f
		};
	}

	private void DeletePlacedBuildPieces()
	{
		var pieces = BuildPiece.All
			.Where( piece => piece.IsValid() )
			.Where( piece => piece.IsPlaced )
			.ToArray();

		foreach ( var piece in pieces )
		{
			if ( !piece.IsValid() )
				continue;

			piece.GameObject.Destroy();
		}

		Log.Info( $"Deleted {pieces.Length} placed build pieces." );
	}

	private void ResetPlayers()
	{
		var players = FloodPlayer.All
			.Where( player => player.IsValid() )
			.ToArray();

		var spawnPoints = FloodPlayerSpawnPoint.All
			.Where( spawnPoint => spawnPoint.IsValid() )
			.OrderBy( spawnPoint => spawnPoint.SpawnOrder )
			.ToArray();

		for ( var i = 0; i < players.Length; i++ )
		{
			ResetPlayerForCurrentRound( players[i], spawnPoints, i );
		}

		Log.Info( $"Reset {players.Length} players." );
	}

	private void ResetPlayerForCurrentRound( FloodPlayer player )
	{
		var spawnPoints = FloodPlayerSpawnPoint.All
			.Where( spawnPoint => spawnPoint.IsValid() )
			.OrderBy( spawnPoint => spawnPoint.SpawnOrder )
			.ToArray();

		var playerIndex = FloodPlayer.All
			.Where( existingPlayer => existingPlayer.IsValid() )
			.ToList()
			.IndexOf( player );

		if ( playerIndex < 0 )
			playerIndex = 0;

		ResetPlayerForCurrentRound( player, spawnPoints, playerIndex );
	}

	private void ResetPlayerForCurrentRound( FloodPlayer player, FloodPlayerSpawnPoint[] spawnPoints, int playerIndex )
	{
		if ( !player.IsValid() )
			return;

		if ( player.Health.IsValid() )
			player.Health.Respawn();

		if ( player.BuildResources.IsValid() )
			player.BuildResources.ResetResources();

		if ( MovePlayersToSpawnPointsOnReset )
			MovePlayerToSpawnPoint( player, spawnPoints, playerIndex );
	}

	private bool ShouldResetJoiningPlayer()
	{
		if ( !SpawnLateJoinersDuringBuildPhase )
			return false;

		return CurrentPhase is GamePhase.WaitingForPlayers or GamePhase.BuildPhase;
	}

	private void MovePlayerToSpawnPoint( FloodPlayer player, FloodPlayerSpawnPoint[] spawnPoints, int playerIndex )
	{
		if ( !player.IsValid() )
			return;

		if ( spawnPoints.Length == 0 )
		{
			player.WorldPosition += FallbackSpawnOffset;
			ClearPlayerVelocity( player );
			Log.Warning( "No FloodPlayerSpawnPoint components found; nudged player upward at current position." );
			return;
		}

		var spawnPoint = spawnPoints[playerIndex % spawnPoints.Length];

		player.WorldPosition = spawnPoint.WorldPosition;
		player.WorldRotation = spawnPoint.WorldRotation;

		ClearPlayerVelocity( player );

		Log.Info( $"Moved {player.GameObject.Name} to spawn point {spawnPoint.GameObject.Name}." );
	}

	private void ClearPlayerVelocity( FloodPlayer player )
	{
		var body = player.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );

		if ( !body.IsValid() )
			return;

		body.Velocity = Vector3.Zero;
		body.AngularVelocity = Vector3.Zero;
	}

	// -----------------------------
	// Debug controls
	// -----------------------------

	private void HandleDebugControls()
	{
		if ( !EnableDebugControls )
			return;

		HandleDebugPhaseInput();
		HandleDebugResetInput();
		HandleDebugDamageInput();
	}

	private void HandleDebugPhaseInput()
	{
		if ( !EnableDebugPhaseKeys )
			return;

		if ( Input.Pressed( "Slot8" ) )
			SetPhase( GamePhase.BuildPhase, true );

		if ( Input.Pressed( "Slot9" ) )
			SetPhase( GamePhase.FloodPhase, true );
		
		if ( Input.Pressed( "Slot0" ) )
			SetPhase( GamePhase.CombatPhase, true );
	}

	private void HandleDebugResetInput()
	{
		if ( !EnableDebugResetKey )
			return;

		if ( Input.Pressed( "Slot7" ) )
			ResetRound();
	}

	private void HandleDebugDamageInput()
	{
		if ( !EnableDebugDamageKey )
			return;

		if ( !Input.Pressed( "Slot6" ) )
			return;

		DebugDamageAllPlayers();
	}

	private void DebugDamageAllPlayers()
	{
		foreach ( var player in FloodPlayer.All.ToArray() )
		{
			if ( !player.IsValid() )
				continue;

			if ( !player.Health.IsValid() )
				continue;

			player.Health.TakeDebugDamage( DebugPlayerDamageAmount );
		}

		Log.Info( $"Debug damaged all players for {DebugPlayerDamageAmount}." );
	}
}
