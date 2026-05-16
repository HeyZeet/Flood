using Sandbox;
using System.Linq;

public sealed class FloodRoundManager : Component
{
	public static FloodRoundManager Instance { get; private set; }

	[Sync] public GamePhase CurrentPhase { get; private set; } = GamePhase.Build;

	[Property] public bool StartInBuildPhase { get; set; } = true;
	[Property] public bool EnableDebugPhaseKeys { get; set; } = true;
	[Property] public bool EnableDebugResetKey { get; set; } = true;

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
		HandleDebugResetInput();
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

	public bool IsRoundEndPhase()
	{
		return CurrentPhase == GamePhase.RoundEnd;
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

	public void ResetRound()
	{
		if ( !Networking.IsHost )
			return;

		Log.Info( "Resetting round." );

		SetPhase( GamePhase.RoundEnd );

		DeletePlacedBuildPieces();
		ResetPlayers();

		SetPhase( GamePhase.Build );

		Log.Info( "Round reset complete." );
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

	private void HandleDebugPhaseInput()
	{
		if ( !EnableDebugPhaseKeys )
			return;

		if ( Input.Pressed( "Slot8" ) )
			SetPhase( GamePhase.Build );

		if ( Input.Pressed( "Slot9" ) )
			SetPhase( GamePhase.Battle );
	}

	private void HandleDebugResetInput()
	{
		if ( !EnableDebugResetKey )
			return;

		if ( Input.Pressed( "Slot7" ) )
			ResetRound();
	}
}
