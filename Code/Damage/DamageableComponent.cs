using Sandbox;

public abstract class DamageableComponent : Component
{
	[Property, Group( "Damage Rules" )]
	public bool RequireBattlePhaseForDamage { get; set; } = true;

	public abstract bool IsAlive { get; }

	public abstract void TakeDamage( DamageInfo damageInfo );

	protected bool CanTakeGameplayDamage( DamageInfo damageInfo )
	{
		if ( damageInfo.IsDebugDamage )
			return true;

		if ( !RequireBattlePhaseForDamage )
			return true;

		var roundManager = FloodGameManager.Instance;

		if ( !roundManager.IsValid() )
			return true;

		var canDamage = roundManager.IsBattlePhase();

		if ( !canDamage )
			Log.Info( $"{GameObject.Name} ignored damage outside combat phase. Current phase: {roundManager.CurrentPhase}." );

		return canDamage;
	}
}
