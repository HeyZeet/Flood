using Sandbox;

public abstract class DamageableComponent : Component
{
	public abstract bool IsAlive { get; }

	public abstract void TakeDamage( DamageInfo damageInfo );
}