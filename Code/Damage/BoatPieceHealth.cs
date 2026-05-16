using Sandbox;

public sealed class BoatPieceHealth : DamageableComponent
{
	[Property] public float MaxHealth { get; set; } = 100f;
	[Property] public bool DestroyOnDeath { get; set; } = true;

	[Sync] public float Health { get; private set; }

	public override bool IsAlive => Health > 0f;
	public bool IsDead => !IsAlive;

	protected override void OnStart()
	{
		if ( Networking.IsHost )
			ResetHealth();
	}

	public override void TakeDamage( DamageInfo damageInfo )
	{
		if ( !Networking.IsHost )
			return;

		if ( IsDead )
			return;

		var amount = damageInfo.Damage.Clamp( 0f, float.MaxValue );

		Health -= amount;
		Health = Health.Clamp( 0f, MaxHealth );

		Log.Info( $"{GameObject.Name} boat piece took {amount} damage. Health: {Health}" );

		if ( Health <= 0f )
			Die( damageInfo );
	}

	public void Repair( float amount )
	{
		if ( !Networking.IsHost )
			return;

		if ( IsDead )
			return;

		amount = amount.Clamp( 0f, float.MaxValue );

		Health += amount;
		Health = Health.Clamp( 0f, MaxHealth );
	}

	public void ResetHealth()
	{
		if ( !Networking.IsHost )
			return;

		Health = MaxHealth;
	}

	private void Die( DamageInfo damageInfo )
	{
		Log.Info( $"{GameObject.Name} boat piece destroyed." );

		if ( DestroyOnDeath )
			GameObject.Destroy();
	}
}