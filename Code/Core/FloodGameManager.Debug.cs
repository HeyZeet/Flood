using Sandbox;
using System.Linq;

public sealed partial class FloodGameManager
{
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
