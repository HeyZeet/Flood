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

	[Property, Group( "Rewards" )]
	public bool AwardWinnerResources { get; set; } = true;

	[Property, Group( "Rewards" )]
	public int WinnerResourceAward { get; set; } = 250;

	[Property, Group( "Debug" )]
	public bool LogWinChecks { get; set; } = false;

	private FloodGameManager RoundManager { get; set; }
	private GameObject WinningPlayerObject { get; set; }
	private TimeSince TimeSinceLastCheck { get; set; }
	private TimeSince TimeSinceCombatStarted { get; set; }
	private int CombatStartPlayerCount { get; set; }
	private bool HasEndedCurrentCombatRound { get; set; }
	private bool HasAwardedWinner { get; set; }

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
			.Where( IsActiveCompetitor )
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

	private bool IsActiveCompetitor( FloodPlayer player )
	{
		if ( !IsEligiblePlayer( player ) )
			return false;

		if ( player.IsEliminated )
			return false;

		return player.IsAlive;
	}

	private void HandleCombatPhaseStarted()
	{
		if ( !Networking.IsHost )
			return;

		WinningPlayerObject = null;
		HasEndedCurrentCombatRound = false;
		HasAwardedWinner = false;
		TimeSinceCombatStarted = 0f;
		TimeSinceLastCheck = CheckInterval;

		CombatStartPlayerCount = FloodPlayer.All
			.Count( IsActiveCompetitor );

		Log.Info( $"WinConditionController watching combat round. Starting alive players: {CombatStartPlayerCount}." );
	}

	private void HandleRoundEndPhaseStarted()
	{
		if ( !Networking.IsHost )
			return;

		HasEndedCurrentCombatRound = true;
		AwardWinningPlayer();
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

	private void AwardWinningPlayer()
	{
		if ( HasAwardedWinner )
			return;

		HasAwardedWinner = true;

		if ( !AwardWinnerResources )
			return;

		if ( WinnerResourceAward <= 0 )
			return;

		var winner = GetWinningPlayer();

		if ( !winner.IsValid() )
			return;

		var resources = winner.BuildResources;

		if ( !resources.IsValid() )
		{
			Log.Warning( $"Could not award {winner.GameObject.Name}: missing PlayerBuildResources." );
			return;
		}

		resources.AddRoundAward( WinnerResourceAward );
		Log.Info( $"Awarded {WinnerResourceAward} resources to {winner.GameObject.Name}." );
	}

	private FloodPlayer GetWinningPlayer()
	{
		if ( !WinningPlayerObject.IsValid() )
			return null;

		return WinningPlayerObject.Components.Get<FloodPlayer>( FindMode.EverythingInSelfAndDescendants );
	}
}
