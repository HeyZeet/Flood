using Sandbox;

public sealed class FloodRoundManager : Component
{
	public static FloodRoundManager Instance { get; private set; }

	[Sync] public GamePhase CurrentPhase { get; private set; } = GamePhase.Build;

	[Property] public bool StartInBuildPhase { get; set; } = true;
	[Property] public bool EnableDebugPhaseKeys { get; set; } = true;

	protected override void OnStart()
	{
		Instance = this;

		if ( Networking.IsHost )
			CurrentPhase = StartInBuildPhase ? GamePhase.Build : GamePhase.Waiting;

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

		HandleDebugPhaseInput();
	}

	public bool IsBuildPhase()
	{
		return CurrentPhase == GamePhase.Build;
	}

	public bool IsBattlePhase()
	{
		return CurrentPhase == GamePhase.Battle;
	}

	public bool IsWaitingPhase()
	{
		return CurrentPhase == GamePhase.Waiting;
	}

	public void SetPhase( GamePhase phase )
	{
		if ( !Networking.IsHost )
			return;

		if ( CurrentPhase == phase )
			return;

		CurrentPhase = phase;

		Log.Info( $"Game phase changed to: {CurrentPhase}" );
	}

	private void HandleDebugPhaseInput()
	{
		if ( !EnableDebugPhaseKeys )
			return;

		if ( Input.Pressed( "Slot8" ) )
			SetPhase( GamePhase.Build );

		if ( Input.Pressed( "Slot9" ) )
			SetPhase( GamePhase.Battle );
	}
}
