using Sandbox;

public sealed class RoundPhaseDebugListener : Component
{
	private FloodRoundManager RoundManager { get; set; }

	protected override void OnStart()
	{
		RoundManager = FloodRoundManager.Instance;

		if ( !RoundManager.IsValid() )
		{
			Log.Warning( "RoundPhaseDebugListener could not find FloodRoundManager.Instance." );
			return;
		}

		RoundManager.OnPhaseChanged += HandlePhaseChanged;
		RoundManager.OnBuildPhaseStarted += HandleBuildPhaseStarted;
		RoundManager.OnFloodPhaseStarted += HandleFloodPhaseStarted;
		RoundManager.OnBattlePhaseStarted += HandleBattlePhaseStarted;
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
		RoundManager.OnBattlePhaseStarted -= HandleBattlePhaseStarted;
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

	private void HandleBattlePhaseStarted()
	{
		Log.Info( "[Phase Event] Battle phase started." );
	}

	private void HandleRoundEndPhaseStarted()
	{
		Log.Info( "[Phase Event] Round end phase started." );
	}
}