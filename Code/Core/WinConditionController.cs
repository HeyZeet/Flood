using Sandbox;
using System;
using System.Linq;

public sealed class WinConditionController : Component
{
	public event Action<FloodPlayer> OnWinnerDetermined;

	[Property, Group( "Rules" )]
	public bool OnlyCheckDuringCombatPhase { get; set; } = true;

	[Property, Group( "Rules" )]
	public int MinPlayersRequiredToEndRound { get; set; } = 2;

	[Property, Group( "Rules" )]
	public float CombatStartGracePeriod { get; set; } = 2f;

	[Property, Group( "Rules" )]
	public float CheckInterval { get; set; } = 0.5f;

	[Property, Group( "Debug" )]
	public bool LogWinChecks { get; set; } = false;

	[Sync] public GameObject WinningPlayerObject { get; private set; }

	private FloodGameManager RoundManager { get; set; }
	private TimeSince TimeSinceLastCheck { get; set; }
	private TimeSince TimeSinceCombatStarted { get; set; }
	private int CombatStartPlayerCount { get; set; }
	private bool HasEndedCurrentCombatRound { get; set; }

	protected override void OnStart()
	{
		RoundManager = FloodGameManager.Instance;

		if ( !RoundManager.IsValid() )
		{
			Log.Warning( "WinConditionController could not find FloodGameManager.Instance." );
			return;
		}

		RoundManager.OnCombatPhaseStarted += HandleCombatPhaseStarted;
		RoundManager.OnRoundEndPhaseStarted += HandleRoundEndPhaseStarted;

		Log.Info( "WinConditionController started." );
	}

	protected override void OnDestroy()
	{
		if ( !RoundManager.IsValid() )
			return;

		RoundManager.OnCombatPhaseStarted -= HandleCombatPhaseStarted;
		RoundManager.OnRoundEndPhaseStarted -= HandleRoundEndPhaseStarted;
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		if ( !ShouldCheckWinConditions() )
			return;

		CheckWinConditions();
	}

	public void CheckWinConditions()
	{
		TimeSinceLastCheck = 0f;

		var alivePlayers = FloodPlayer.All
			.Where( IsEligiblePlayer )
			.Where( player => player.IsAlive )
			.ToArray();

		if ( LogWinChecks )
		{
			Log.Info(
				$"Win check. Alive: {alivePlayers.Length}, CombatStart: {CombatStartPlayerCount}, Phase: {RoundManager?.CurrentPhase}"
			);
		}

		if ( CombatStartPlayerCount < MinPlayersRequiredToEndRound )
			return;

		if ( alivePlayers.Length > 1 )
			return;

		var winner = alivePlayers.FirstOrDefault();

		EndRoundWithWinner( winner );
	}

	private bool ShouldCheckWinConditions()
	{
		if ( HasEndedCurrentCombatRound )
			return false;

		if ( !RoundManager.IsValid() )
			return false;

		if ( OnlyCheckDuringCombatPhase && !RoundManager.IsCombatPhase() )
			return false;

		if ( TimeSinceCombatStarted < CombatStartGracePeriod )
			return false;

		return TimeSinceLastCheck >= CheckInterval;
	}

	private bool IsEligiblePlayer( FloodPlayer player )
	{
		if ( !player.IsValid() )
			return false;

		if ( !player.Health.IsValid() )
			return false;

		return true;
	}

	private void HandleCombatPhaseStarted()
	{
		if ( !Networking.IsHost )
			return;

		WinningPlayerObject = null;
		HasEndedCurrentCombatRound = false;
		TimeSinceCombatStarted = 0f;
		TimeSinceLastCheck = CheckInterval;

		CombatStartPlayerCount = FloodPlayer.All
			.Where( IsEligiblePlayer )
			.Count( player => player.IsAlive );

		Log.Info( $"WinConditionController watching combat round. Starting alive players: {CombatStartPlayerCount}." );
	}

	private void HandleRoundEndPhaseStarted()
	{
		if ( !Networking.IsHost )
			return;

		HasEndedCurrentCombatRound = true;
	}

	private void EndRoundWithWinner( FloodPlayer winner )
	{
		HasEndedCurrentCombatRound = true;
		WinningPlayerObject = winner.IsValid() ? winner.GameObject : null;

		if ( winner.IsValid() )
		{
			Log.Info( $"Round winner: {winner.GameObject.Name}." );
			OnWinnerDetermined?.Invoke( winner );
		}
		else
		{
			Log.Info( "Round ended with no surviving players." );
			OnWinnerDetermined?.Invoke( null );
		}

		RoundManager.EndRound();
	}
}
