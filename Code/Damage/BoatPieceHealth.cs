using Sandbox;
using System;

public sealed class BoatPieceHealth : DamageableComponent
{
	[Property] public float MaxHealth { get; set; } = 100f;
	[Property] public bool DestroyOnDeath { get; set; } = true;

	[Sync] public float Health { get; private set; }

	public event Action<BoatPieceHealth, DamageInfo> Damaged;
	public event Action<BoatPieceHealth, DamageInfo> Died;

	public override bool IsAlive => Health > 0f;
	public bool IsDead => !IsAlive;
	public float HealthFraction => MaxHealth <= 0f ? 0f : (Health / MaxHealth).Clamp( 0f, 1f );

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

		if ( !CanTakeGameplayDamage( damageInfo ) )
			return;

		var amount = damageInfo.Damage.Clamp( 0f, float.MaxValue );

		Health -= amount;
		Health = Health.Clamp( 0f, MaxHealth );

		Log.Info( $"{GameObject.Name} boat piece took {amount} damage. Health: {Health}" );

		Damaged?.Invoke( this, damageInfo );

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

		Died?.Invoke( this, damageInfo );

		if ( DestroyOnDeath )
			GameObject.Destroy();
	}
}
