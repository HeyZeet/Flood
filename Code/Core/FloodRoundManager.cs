using Sandbox;
using System;
using System.Linq;

public sealed class FloodRoundManager : Component
{
	public static FloodRoundManager Instance { get; private set; }

	public event Action<GamePhase, GamePhase> OnPhaseChanged;
	public event Action OnBuildPhaseStarted;
	public event Action OnFloodPhaseStarted;
	public event Action OnBattlePhaseStarted;
	public event Action OnRoundEndPhaseStarted;

	[Sync] public GamePhase CurrentPhase { get; private set; } = GamePhase.Build;

	[Property, Group( "Round" )]
	public bool AutoRunRoundLoop { get; set; } = true;

	[Property, Group( "Round" )]
	public bool StartInBuildPhase { get; set; } = true;

	[Property, Group( "Round Timers" )]
	public float BuildDuration { get; set; } = 60f;

	[Property, Group( "Round Timers" )]
	public float FloodDuration { get; set; } = 20f;

	[Property, Group( "Round Timers" )]
	public float BattleDuration { get; set; } = 120f;

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

	private TimeSince TimeSincePhaseStarted { get; set; }

	protected override void OnStart()
	{
		Instance = this;

		if ( Networking.IsHost )
		{
			var startingPhase = StartInBuildPhase ? GamePhase.Build : GamePhase.Waiting;
			SetPhase( startingPhase, true );
		}

		Log.Info( $"Flood round manager started. Phase: {CurrentPhase}" );
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

		HandleRoundLoop();
		HandleDebugControls();
	}

	public bool IsBuildPhase()
	{
		return CurrentPhase == GamePhase.Build;
	}

	public bool IsFloodPhase()
	{
		return CurrentPhase == GamePhase.Flood;
	}

	public bool IsBattlePhase()
	{
		return CurrentPhase == GamePhase.Battle;
	}

	public bool IsWaitingPhase()
	{
		return CurrentPhase == GamePhase.Waiting;
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
		return TimeSincePhaseStarted;
	}

	public float GetCurrentPhaseTimeRemaining()
	{
		var duration = GetCurrentPhaseDuration();

		if ( duration <= 0f )
			return 0f;

		return (duration - TimeSincePhaseStarted).Clamp( 0f, duration );
	}

	public void SetPhase( GamePhase phase, bool force = false )
	{
		if ( !Networking.IsHost )
			return;

		if ( !force && CurrentPhase == phase )
			return;

		var previousPhase = CurrentPhase;

		CurrentPhase = phase;
		TimeSincePhaseStarted = 0f;

		Log.Info( $"Game phase changed: {previousPhase} -> {CurrentPhase}" );

		HandleWaterForPhase( CurrentPhase );
		NotifyPhaseChanged( previousPhase, CurrentPhase );
	}

	public void ResetRound()
	{
		if ( !Networking.IsHost )
			return;

		Log.Info( "Resetting round." );

		DeletePlacedBuildPieces();
		ResetPlayers();

		SetPhase( GamePhase.Build, true );

		Log.Info( "Round reset complete." );
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
			Log.Warning( "FloodRoundManager could not find FloodWaterController.Instance." );
			return;
		}

		switch ( phase )
		{
			case GamePhase.Build:
				water.ResetWater();
				break;

			case GamePhase.Flood:
				water.StartFlood();
				break;

			case GamePhase.Battle:
				water.StopFlood();
				break;

			case GamePhase.RoundEnd:
				water.StartDrain();
				break;

			case GamePhase.Waiting:
				water.ResetWater();
				break;
		}
	}

	private void NotifyPhaseChanged( GamePhase previousPhase, GamePhase newPhase )
	{
		OnPhaseChanged?.Invoke( previousPhase, newPhase );

		switch ( newPhase )
		{
			case GamePhase.Build:
				OnBuildPhaseStarted?.Invoke();
				break;

			case GamePhase.Flood:
				OnFloodPhaseStarted?.Invoke();
				break;

			case GamePhase.Battle:
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

		switch ( CurrentPhase )
		{
			case GamePhase.Waiting:
				break;

			case GamePhase.Build:
				if ( HasPhaseExpired() )
					SetPhase( GamePhase.Flood );
				break;

			case GamePhase.Flood:
				if ( HasPhaseExpired() )
					SetPhase( GamePhase.Battle );
				break;

			case GamePhase.Battle:
				if ( HasPhaseExpired() )
					SetPhase( GamePhase.RoundEnd );
				break;

			case GamePhase.RoundEnd:
				if ( HasPhaseExpired() )
					ResetRound();
				break;
		}
	}

	private bool HasPhaseExpired()
	{
		var duration = GetCurrentPhaseDuration();

		if ( duration <= 0f )
			return false;

		return TimeSincePhaseStarted >= duration;
	}

	private float GetPhaseDuration( GamePhase phase )
	{
		return phase switch
		{
			GamePhase.Build => BuildDuration,
			GamePhase.Flood => FloodDuration,
			GamePhase.Battle => BattleDuration,
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
		foreach ( var player in FloodPlayer.All.ToArray() )
		{
			if ( !player.IsValid() )
				continue;

			if ( player.Health.IsValid() )
				player.Health.Respawn();

			if ( player.BuildResources.IsValid() )
				player.BuildResources.ResetResources();
		}

		Log.Info( $"Reset {FloodPlayer.All.Count} players." );
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
			SetPhase( GamePhase.Build, true );

		if ( Input.Pressed( "Slot9" ) )
			SetPhase( GamePhase.Flood, true );
		
		if ( Input.Pressed( "Slot0" ) )
			SetPhase( GamePhase.Battle, true );
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