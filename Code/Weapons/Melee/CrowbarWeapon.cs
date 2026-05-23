using Sandbox;

public sealed class CrowbarWeapon : BaseMeleeWeapon
{
	public override string DisplayName => "Crowbar";

	[Property] public SoundEvent SwingSound { get; set; }
	[Property] public SoundEvent HitSound { get; set; }

	private ThirdPersonWeaponModel ThirdPersonModel =>
	Components.Get<ThirdPersonWeaponModel>( FindMode.EverythingInSelfAndDescendants );
	private ViewModelWeapon ViewModel => 
	Components.Get<ViewModelWeapon>( FindMode.EverythingInSelfAndDescendants );

	protected override void OnStart()
	{
		base.OnStart();

		Damage = 25f;
		PrimaryFireRate = 0.6f;
		Range = 90f;
		TraceRadius = 10f;
		HoldType = "melee_weapons";
	}

	public override void OnDeploy()
	{
		base.OnDeploy();

		if ( IsLocallyControlled() )
			ViewModel?.Show();

		ThirdPersonModel?.Show();

		Log.Info( "Crowbar deployed." );
	}

	public override void OnHolster()
	{
		ViewModel?.Hide();
		ThirdPersonModel?.Hide();

		base.OnHolster();

		Log.Info( "Crowbar holstered." );
	}

	public override void PrimaryAttack()
	{
		PlaySwingEffects();

		base.PrimaryAttack();
	}

	protected override void DoMeleeAttack( Vector3 start, Vector3 direction )
	{
		base.DoMeleeAttack( start, direction );

		TryPlayHitSound( start, direction );
	}

	protected override void OnMeleeAttackApproved( bool skipLocalOwner )
	{
		base.OnMeleeAttackApproved( skipLocalOwner );
		BroadcastSwingEffects( skipLocalOwner );
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

	private void BroadcastSwingEffects( bool skipLocalOwner )
	{
		if ( !Networking.IsHost )
			return;

		PlaySwingEffectsBroadcast( skipLocalOwner );
	}

	[Rpc.Broadcast]
	private void PlaySwingEffectsBroadcast( bool skipLocalOwner )
	{
		if ( skipLocalOwner && IsLocallyControlled() )
			return;

		PlaySwingEffects();
	}

	private void PlayHitEffects( Vector3 hitPosition )
	{
		if ( HitSound is not null )
			Sound.Play( HitSound, hitPosition );

		BroadcastHitEffects( hitPosition, true );
	}

	private void BroadcastHitEffects( Vector3 hitPosition, bool skipLocalOwner )
	{
		if ( !Networking.IsHost )
			return;

		PlayHitEffectsBroadcast( hitPosition, skipLocalOwner );
	}

	[Rpc.Broadcast]
	private void PlayHitEffectsBroadcast( Vector3 hitPosition, bool skipLocalOwner )
	{
		if ( skipLocalOwner && IsLocallyControlled() )
			return;

		if ( HitSound is not null )
			Sound.Play( HitSound, hitPosition );
	}
}
