using Sandbox;

public sealed class CrowbarWeapon : BaseMeleeWeapon
{
	public override string DisplayName => "Crowbar";

	[Property] public SoundEvent SwingSound { get; set; }
	[Property] public SoundEvent HitSound { get; set; }

	protected override void OnStart()
	{
		base.OnStart();

		Damage = 25f;
		PrimaryFireRate = 0.6f;
		Range = 90f;
		TraceRadius = 10f;
	}

	public override void OnDeploy()
	{
		base.OnDeploy();

		Log.Info( "Crowbar deployed." );
	}

	public override void OnHolster()
	{
		base.OnHolster();

		Log.Info( "Crowbar holstered." );
	}

	public override void PrimaryAttack()
	{
		PlaySwingEffects();

		base.PrimaryAttack();

		// Later:
		// Trigger viewmodel swing animation.
		// Trigger third-person attack animation.
	}

	protected override void DoMeleeAttack()
	{
		var owner = OwnerPlayer;

		if ( !owner.IsValid() )
		{
			Log.Warning( "Crowbar has no valid owner player." );
			return;
		}

		var start = GetAttackStart();
		var end = start + GetAttackDirection() * Range;

		var tr = Scene.Trace
			.Sphere( TraceRadius, start, end )
			.IgnoreGameObjectHierarchy( owner.GameObject )
			.Run();

		if ( DrawDebugTrace )
		{
			DebugOverlay.Trace( tr, 1f );
		}

		if ( !tr.Hit )
		{
			Log.Info( "Crowbar missed." );
			return;
		}

		Log.Info( $"Crowbar hit {tr.GameObject.Name}." );

		PlayHitEffects( tr.HitPosition );

        var damageable = tr.GameObject.Components.Get<DamageableComponent>( FindMode.EverythingInSelfAndAncestors );

        if ( damageable.IsValid() )
        {
	        var damageInfo = DamageInfo.FromWeapon( this, tr );
	        damageable.TakeDamage( damageInfo );

	        Log.Info( $"Crowbar damaged {tr.GameObject.Name} for {Damage}." );
        }
	}

	private void PlaySwingEffects()
	{
		if ( SwingSound is not null )
		{
			Sound.Play( SwingSound, WorldPosition );
		}
	}

	private void PlayHitEffects( Vector3 hitPosition )
	{
		if ( HitSound is not null )
		{
			Sound.Play( HitSound, hitPosition );
		}
	}
}