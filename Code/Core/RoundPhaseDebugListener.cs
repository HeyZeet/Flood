using Sandbox;

public sealed class RoundPhaseDebugListener : Component
{
	private FloodGameManager RoundManager { get; set; }

	protected override void OnStart()
	{
		RoundManager = FloodGameManager.Instance;

		if ( !RoundManager.IsValid() )
		{
			Log.Warning( "RoundPhaseDebugListener could not find FloodGameManager.Instance." );
			return;
		}

		RoundManager.OnPhaseChanged += HandlePhaseChanged;
		RoundManager.OnBuildPhaseStarted += HandleBuildPhaseStarted;
		RoundManager.OnFloodPhaseStarted += HandleFloodPhaseStarted;
		RoundManager.OnCombatPhaseStarted += HandleCombatPhaseStarted;
		RoundManager.OnRoundEndPhaseStarted += HandleRoundEndPhaseStarted;

		Log.Info( "RoundPhaseDebugListener subscribed to round phase events." );
	}

	protected override void OnDestroy()
	{
		if ( !RoundManager.IsValid() )
			return;

		RoundManager.OnPhaseChanged -= HandlePhaseChanged;
		RoundManager.OnBuildPhaseStarted -= HandleBuildPhaseStarted;
		RoundManager.OnFloodPhaseStarted -= HandleFloodPhaseStarted;
		RoundManager.OnCombatPhaseStarted -= HandleCombatPhaseStarted;
		RoundManager.OnRoundEndPhaseStarted -= HandleRoundEndPhaseStarted;
	}

	private void HandlePhaseChanged( GamePhase previousPhase, GamePhase newPhase )
	{
		Log.Info( $"[Phase Event] {previousPhase} -> {newPhase}" );
	}

	private void HandleBuildPhaseStarted()
	{
		Log.Info( "[Phase Event] Build phase started." );
	}

	private void HandleFloodPhaseStarted()
	{
		Log.Info( "[Phase Event] Flood phase started." );
	}

	private void HandleCombatPhaseStarted()
	{
		Log.Info( "[Phase Event] Combat phase started." );
	}

	private void HandleRoundEndPhaseStarted()
	{
		Log.Info( "[Phase Event] Round end phase started." );
	}
}
