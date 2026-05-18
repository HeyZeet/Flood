using Sandbox;
using System;
public abstract class BaseGunWeapon : BaseWeapon
{
	[Header( "Gun" )]
	[Property] public float BulletRange { get; set; } = 5000f;
	[Property] public float BulletRadius { get; set; } = 1.5f;
	[Property] public bool DrawDebugTrace { get; set; } = true;

	[Property] public bool InfiniteAmmo { get; set; } = true;
	[Property] public int ClipSize { get; set; } = 12;
	[Property, Sync] public int AmmoInClip { get; set; } = 12;

	[Header( "Effects" )]
	[Property] public SoundEvent FireSound { get; set; }
	[Property] public SoundEvent DryFireSound { get; set; }

	protected override void OnStart()
	{
		base.OnStart();

		if ( AmmoInClip <= 0 )
			AmmoInClip = ClipSize;
	}

	public override void PrimaryAttack()
	{
		if ( !HasAmmo() )
		{
			PlayDryFireEffects();
			return;
		}

		ConsumeAmmo();

		PlayFireEffects();
		FireBullet();

		base.PrimaryAttack();
	}

	protected virtual bool HasAmmo()
	{
		if ( InfiniteAmmo )
			return true;

		return AmmoInClip > 0;
	}

	protected virtual void ConsumeAmmo()
	{
		if ( InfiniteAmmo )
			return;

		AmmoInClip = System.Math.Clamp( AmmoInClip - 1, 0, ClipSize );
	}

	protected virtual void PlayFireEffects()
	{
		if ( FireSound is not null )
			Sound.Play( FireSound, WorldPosition );

		var viewModel = Components.Get<ViewModelWeapon>( FindMode.EverythingInSelfAndDescendants );

		if ( viewModel.IsValid() )
			viewModel.PlayAttack();
	}

	protected virtual void PlayDryFireEffects()
	{
		if ( DryFireSound is not null )
			Sound.Play( DryFireSound, WorldPosition );
	}

	protected virtual void FireBullet()
	{
		var owner = OwnerPlayer;

		if ( !owner.IsValid() )
		{
			Log.Warning( $"{DisplayName} has no valid owner player." );
			return;
		}

		var start = GetShootStart();
		var end = start + GetShootDirection() * BulletRange;

		var tr = Scene.Trace
			.Sphere( BulletRadius, start, end )
			.IgnoreGameObjectHierarchy( owner.GameObject )
			.Run();

		if ( DrawDebugTrace )
			DebugOverlay.Trace( tr, 1f );

		if ( !tr.Hit )
			return;

		TryDamageHitObject( tr );
	}

	protected virtual Vector3 GetShootStart()
	{
		var camera = OwnerPlayer?.Components.Get<FloodPlayerCamera>();

		if ( camera.IsValid() )
			return camera.EyePosition;

		var sceneCamera = Scene.Camera;

		if ( sceneCamera.IsValid() )
			return sceneCamera.WorldPosition;

		return WorldPosition;
	}

	protected virtual Vector3 GetShootDirection()
	{
		var camera = OwnerPlayer?.Components.Get<FloodPlayerCamera>();

		if ( camera.IsValid() )
			return camera.AimForward;

		var sceneCamera = Scene.Camera;

		if ( sceneCamera.IsValid() )
			return sceneCamera.WorldRotation.Forward;

		return WorldRotation.Forward;
	}

	protected virtual void TryDamageHitObject( SceneTraceResult trace )
	{
		var damageable = trace.GameObject.Components.Get<DamageableComponent>( FindMode.EverythingInSelfAndAncestors );

		if ( !damageable.IsValid() )
			return;

		var damageInfo = DamageInfo.FromWeapon( this, trace );

		damageable.TakeDamage( damageInfo );

		Log.Info( $"{DisplayName} shot {trace.GameObject.Name} for {Damage} damage." );
	}
}