using Sandbox;

public sealed class PlayerHealth : DamageableComponent
{
	[Property] public float MaxHealth { get; set; } = 100f;

	[Sync] public float Health { get; private set; }

	public override bool IsAlive => Health > 0f;
	public bool IsDead => !IsAlive;

	protected override void OnStart()
	{
		if ( Networking.IsHost )
			ResetHealth();
	}

	public void TakeDamage( float amount )
	{
		var damageInfo = new DamageInfo
		{
			Damage = amount,
			HitObject = GameObject
		};

		TakeDamage( damageInfo );
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

		Log.Info( $"{GameObject.Name} took {amount} damage. Health: {Health}" );

		if ( Health <= 0f )
			Die( damageInfo );
	}

	public void Heal( float amount )
	{
		if ( !Networking.IsHost )
			return;

		if ( IsDead )
			return;

		amount = amount.Clamp( 0f, float.MaxValue );

		Health += amount;
		Health = Health.Clamp( 0f, MaxHealth );
	}

	public void Kill()
	{
		if ( !Networking.IsHost )
			return;

		if ( IsDead )
			return;

		var damageInfo = new DamageInfo
		{
			Damage = Health,
			HitObject = GameObject
		};

		Health = 0f;
		Die( damageInfo );
	}

	public void Respawn()
	{
		if ( !Networking.IsHost )
			return;

		ResetHealth();

		Log.Info( $"{GameObject.Name} respawned. Health: {Health}" );
	}

	public void ResetHealth()
	{
		if ( !Networking.IsHost )
			return;

		Health = MaxHealth;
	}

	private void Die( DamageInfo damageInfo )
	{
		Log.Info( $"{GameObject.Name} died." );

		// Do not disable PlayerController here.
		// Sandbox.PlayerController can throw errors if disabled/re-enabled during physics updates.
		// Later we will add a proper dead-state input/movement block instead.
	}
}
