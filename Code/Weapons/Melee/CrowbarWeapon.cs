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
		var start = GetAttackStart();
		var direction = GetAttackDirection();

		base.DoMeleeAttack();

		TryPlayHitSound( start, direction );
	}

	private void TryPlayHitSound( Vector3 start, Vector3 direction )
	{
		if ( HitSound is null )
			return;

		var owner = OwnerPlayer;

		if ( !owner.IsValid() )
			return;

		var end = start + direction * Range;

		var tr = Scene.Trace
			.Sphere( TraceRadius, start, end )
			.IgnoreGameObjectHierarchy( owner.GameObject )
			.Run();

		if ( tr.Hit )
			PlayHitEffects( tr.HitPosition );
	}

	private void PlaySwingEffects()
	{
		if ( SwingSound is not null )
			Sound.Play( SwingSound, WorldPosition );
	}

	private void PlayHitEffects( Vector3 hitPosition )
	{
		if ( HitSound is not null )
			Sound.Play( HitSound, hitPosition );
	}
}
