using Sandbox;

public sealed class FloodRoundManager : Component
{
	public static FloodRoundManager Instance { get; private set; }

	[Sync] public GamePhase CurrentPhase { get; private set; } = GamePhase.Build;

	[Property] public bool StartInBuildPhase { get; set; } = true;

	protected override void OnStart()
	{
		Instance = this;

		if ( Networking.IsHost )
		{
			CurrentPhase = StartInBuildPhase ? GamePhase.Build : GamePhase.Waiting;
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

	    if ( Input.Pressed( "Slot8" ) )
		    SetPhase( GamePhase.Build );

	    if ( Input.Pressed( "Slot9" ) )
		    SetPhase( GamePhase.Battle );
    }

	public bool IsBuildPhase()
	{
		return CurrentPhase == GamePhase.Build;
	}

	public void SetPhase( GamePhase phase )
	{
		if ( !Networking.IsHost )
			return;

		CurrentPhase = phase;

		Log.Info( $"Game phase changed to: {CurrentPhase}" );
	}
}