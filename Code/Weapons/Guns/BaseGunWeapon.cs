using Sandbox;
using System;

public abstract class BaseGunWeapon : BaseWeapon
{
	[Property, Group( "Gun" )] public float BulletRange { get; set; } = 5000f;
	[Property, Group( "Gun" )] public float BulletRadius { get; set; } = 1.5f;
	[Property, Group( "Gun" )] public bool DrawDebugTrace { get; set; } = true;

	[Property, Group( "Ammo" ), Sync( SyncFlags.FromHost )] public int ReserveAmmo { get; set; } = 48;
	[Property, Group( "Ammo" )] public bool HasReserveAmmo { get; set; } = true;
	[Property, Group( "Ammo" )] public bool InfiniteAmmo { get; set; } = true;
	[Property, Group( "Ammo" )] public int ClipSize { get; set; } = 12;
	[Property, Group( "Ammo" ), Sync( SyncFlags.FromHost )] public int AmmoInClip { get; set; } = 12;

	[Property, Group( "Reload" )] public float ReloadTime { get; set; } = 1.4f;
	[Property, Group( "Sounds" )] public SoundEvent ReloadSound { get; set; }

	[Property, Group( "Accuracy" )] public float BaseSpreadDegrees { get; set; } = 0.5f;
	[Property, Group( "Accuracy" )] public float MaxSpreadDegrees { get; set; } = 3f;
	[Property, Group( "Accuracy" )] public float SpreadPerShot { get; set; } = 0.35f;
	[Property, Group( "Accuracy" )] public float SpreadRecoveryRate { get; set; } = 4f;

	[Property, Group( "Recoil" )] public float RecoilPitch { get; set; } = 1.1f;
	[Property, Group( "Recoil" )] public float RecoilYaw { get; set; } = 0.35f;

	[Property, Group( "Sounds" )] public SoundEvent FireSound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent DryFireSound { get; set; }

	[Property, Group( "Impact Effects" )] public GameObject DefaultImpactPrefab { get; set; }
	[Property, Group( "Impact Effects" )] public GameObject WoodImpactPrefab { get; set; }
	[Property, Group( "Impact Effects" )] public GameObject MetalImpactPrefab { get; set; }
	[Property, Group( "Impact Effects" )] public GameObject GlassImpactPrefab { get; set; }
	[Property, Group( "Impact Effects" )] public GameObject WaterImpactPrefab { get; set; }
	[Property, Group( "Impact Effects" )] public GameObject BrickImpactPrefab { get; set; }
	[Property, Group( "Impact Effects" )] public GameObject FleshImpactPrefab { get; set; }
	[Property, Group( "Impact Effects" )] public float ImpactLifeTime { get; set; } = 1.5f;

	[Property, Group( "Muzzle Flash" )] public GameObject MuzzleFlashPrefab { get; set; }
	[Property, Group( "Muzzle Flash" )] public string MuzzleBoneName { get; set; } = "muzzle";
	[Property, Group( "Muzzle Flash" )] public Vector3 MuzzleFlashPositionOffset { get; set; } = Vector3.Zero;
	[Property, Group( "Muzzle Flash" )] public Angles MuzzleFlashRotationOffset { get; set; } = Angles.Zero;
	[Property, Group( "Muzzle Flash" )] public float MuzzleFlashLifeTime { get; set; } = 0.08f;

	[Sync( SyncFlags.FromHost )] public bool IsReloading { get; protected set; }

	private TimeSince TimeSinceReloadStarted { get; set; }
	private float CurrentSpreadDegrees { get; set; }
	private bool WasReloadingLastFrame { get; set; }

	public string AmmoDisplay
	{
		get
		{
			if ( InfiniteAmmo )
				return $"{AmmoInClip} / INF";

			if ( !HasReserveAmmo )
				return $"{AmmoInClip} / --";

			return $"{AmmoInClip} / {ReserveAmmo}";
		}
	}

	protected override void OnStart()
	{
		base.OnStart();

		if ( AmmoInClip <= 0 )
			AmmoInClip = ClipSize;

		CurrentSpreadDegrees = BaseSpreadDegrees;
	}

	protected override void OnUpdate()
	{
		UpdateReloadPresentation();

		if ( !Networking.IsHost )
			return;

		UpdateReload();
		UpdateSpreadRecovery();
	}

	public override void OnHolster()
	{
		CancelReload();

		base.OnHolster();
	}

	public override void OnPlayerUpdate()
	{
		base.OnPlayerUpdate();

		if ( Input.Pressed( "reload" ) )
			StartReload();
	}

	public override bool CanPrimaryAttack()
	{
		if ( IsReloading && !CanPrimaryAttackWhileReloading() )
			return false;

		return base.CanPrimaryAttack();
	}

	public override void PrimaryAttack()
	{
		if ( !Networking.IsHost )
		{
			if ( HasAmmo() )
			{
				PlayFireEffects();
				ApplyRecoil();

				if ( PlayAttackAnimation )
					PlayWeaponAnimation( AttackTrigger );
			}
			else
			{
				PlayDryFireEffects();
			}

			RequestPrimaryAttack( GetOwnerEyePosition(), GetOwnerAimDirection() );
			return;
		}

		PrimaryAttackHost( GetShootStart(), GetBaseShootDirection() );
	}

	[Rpc.Host]
	private void RequestPrimaryAttack( Vector3 start, Vector3 direction )
	{
		if ( !CanPrimaryAttack() )
			return;

		if ( !IsAimRequestReasonable( start, direction ) )
			return;

		ResetPrimaryAttackCooldown();
		PrimaryAttackHost( start, direction );
	}

	protected virtual void PrimaryAttackHost( Vector3 start, Vector3 baseDirection )
	{
		if ( IsReloading )
			return;

		if ( !HasAmmo() )
		{
			PlayDryFireEffects();
			BroadcastDryFireEffects( true );
			return;
		}

		ConsumeAmmo();

		PlayFireEffects();
		BroadcastFireEffects( true );
		FireBullet( start, GetShootDirection( baseDirection ) );

		AddSpread();
		ApplyRecoil();

		base.PrimaryAttack();
		BroadcastWeaponAnimation( AttackTrigger, true );

		if ( AmmoInClip <= 0 && HasReserveAmmo )
			StartReload( false );
	}

	protected virtual bool CanPrimaryAttackWhileReloading()
	{
		return false;
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

		PlayMuzzleFlash();
	}

	protected virtual void PlayDryFireEffects()
	{
		if ( DryFireSound is not null )
			Sound.Play( DryFireSound, WorldPosition );

		ClearOneShotAnimationParams();
		PlayWeaponAnimation( DryAttackTrigger );
	}

	private void BroadcastFireEffects( bool skipLocalOwner = true )
	{
		if ( !Networking.IsHost )
			return;

		PlayFireEffectsBroadcast( skipLocalOwner );
	}

	[Rpc.Broadcast]
	private void PlayFireEffectsBroadcast( bool skipLocalOwner )
	{
		if ( skipLocalOwner && IsLocallyControlled() )
			return;

		PlayFireEffects();
	}

	private void BroadcastDryFireEffects( bool skipLocalOwner = true )
	{
		if ( !Networking.IsHost )
			return;

		PlayDryFireEffectsBroadcast( skipLocalOwner );
	}

	[Rpc.Broadcast]
	private void PlayDryFireEffectsBroadcast( bool skipLocalOwner )
	{
		if ( skipLocalOwner && IsLocallyControlled() )
			return;

		PlayDryFireEffects();
	}

	protected virtual void PlayMuzzleFlash()
	{
		if ( !MuzzleFlashPrefab.IsValid() )
			return;

		PlayFirstPersonMuzzleFlash();
		PlayThirdPersonMuzzleFlash();
	}

	protected virtual bool CanReload()
	{
		if ( IsReloading )
			return false;

		if ( InfiniteAmmo )
			return false;

		if ( AmmoInClip >= ClipSize )
			return false;

		if ( HasReserveAmmo && ReserveAmmo <= 0 )
			return false;

		return true;
	}

	protected virtual void StartReload( bool ownerAlreadyPredicted = true )
	{
		if ( !Networking.IsHost )
		{
			if ( CanReload() )
				PlayReloadEffects();

			RequestReload();
			return;
		}

		if ( !CanReload() )
			return;

		IsReloading = true;
		TimeSinceReloadStarted = 0f;

		PlayReloadEffects();
		BroadcastReloadEffects( ownerAlreadyPredicted );
	}

	[Rpc.Host]
	private void RequestReload()
	{
		StartReload();
	}

	protected virtual void UpdateReload()
	{
		if ( !IsReloading )
			return;

		if ( TimeSinceReloadStarted < ReloadTime )
			return;

		FinishReload();
	}

	protected virtual void FinishReload()
	{
		if ( !Networking.IsHost )
			return;

		IsReloading = false;

		var ammoNeeded = ClipSize - AmmoInClip;

		if ( ammoNeeded <= 0 )
			return;

		if ( !HasReserveAmmo )
		{
			AmmoInClip = ClipSize;
			return;
		}

		var ammoToLoad = System.Math.Min( ammoNeeded, ReserveAmmo );

		AmmoInClip += ammoToLoad;
		ReserveAmmo -= ammoToLoad;
	}

	protected virtual void PlayReloadEffects()
	{
		if ( ReloadSound is not null )
			Sound.Play( ReloadSound, WorldPosition );

		ClearOneShotAnimationParams();
		PlayWeaponAnimation( ReloadTrigger );
	}

	private void BroadcastReloadEffects( bool skipLocalOwner = true )
	{
		if ( !Networking.IsHost )
			return;

		PlayReloadEffectsBroadcast( skipLocalOwner );
	}

	[Rpc.Broadcast]
	private void PlayReloadEffectsBroadcast( bool skipLocalOwner )
	{
		if ( Networking.IsHost && IsLocallyControlled() )
			return;

		if ( skipLocalOwner && IsLocallyControlled() )
			return;

		PlayReloadEffects();
	}

	private void UpdateReloadPresentation()
	{
		if ( WasReloadingLastFrame == IsReloading )
			return;

		WasReloadingLastFrame = IsReloading;

		if ( !IsReloading )
			return;

		if ( Networking.IsHost )
			return;
	}

	private void PlayFirstPersonMuzzleFlash()
	{
		if ( !ShouldShowViewModel() )
			return;

		var viewModel = Components.Get<ViewModelWeapon>( FindMode.EverythingInSelfAndDescendants );

		if ( !viewModel.IsValid() )
			return;

		if ( !viewModel.TryGetBoneTransform( MuzzleBoneName, out var muzzleTransform ) )
			return;

		SpawnMuzzleFlash( muzzleTransform );
	}

	private void PlayThirdPersonMuzzleFlash()
	{
		var thirdPersonModel = Components.Get<ThirdPersonWeaponModel>( FindMode.EverythingInSelfAndDescendants );

		if ( !thirdPersonModel.IsValid() )
			return;

		if ( thirdPersonModel.ShouldHideForLocalPlayer() )
			return;

		var muzzleTransform = thirdPersonModel.GetMuzzleTransform();

		SpawnMuzzleFlash( muzzleTransform );
	}

	private void SpawnMuzzleFlash( Transform muzzleTransform )
	{
		var flash = MuzzleFlashPrefab.Clone();

		flash.WorldPosition =
			muzzleTransform.Position +
			muzzleTransform.Rotation.Forward * MuzzleFlashPositionOffset.x +
			muzzleTransform.Rotation.Right * MuzzleFlashPositionOffset.y +
			muzzleTransform.Rotation.Up * MuzzleFlashPositionOffset.z;

		flash.WorldRotation = muzzleTransform.Rotation * MuzzleFlashRotationOffset.ToRotation();

		if ( MuzzleFlashLifeTime > 0f )
		{
			var destroyAfterTime = flash.Components.Create<DestroyAfterTime>();
			destroyAfterTime.LifeTime = MuzzleFlashLifeTime;
		}
	}

	protected virtual void FireBullet( Vector3 start, Vector3 direction )
	{
		var owner = OwnerPlayer;

		if ( !owner.IsValid() )
		{
			Log.Warning( $"{DisplayName} has no valid owner player." );
			return;
		}

		var end = start + direction.Normal * BulletRange;
		var tr = Scene.Trace.Sphere( BulletRadius, start, end )
			.IgnoreGameObjectHierarchy( owner.GameObject )
			.Run();

		if ( DrawDebugTrace )
			DebugOverlay.Trace( tr, 1f );

		if ( !tr.Hit )
			return;

		PlayImpactEffect( tr );
		TryDamageHitObject( tr );
	}

	protected virtual Vector3 GetShootStart()
	{
		return GetOwnerEyePosition();
	}

	protected virtual Vector3 GetShootDirection()
	{
		return GetShootDirection( GetBaseShootDirection() );
	}

	protected virtual Vector3 GetShootDirection( Vector3 baseDirection )
	{
		if ( CurrentSpreadDegrees <= 0f )
			return baseDirection.Normal;

		var randomYaw = Game.Random.Float( -CurrentSpreadDegrees, CurrentSpreadDegrees );
		var randomPitch = Game.Random.Float( -CurrentSpreadDegrees, CurrentSpreadDegrees );

		var rotation = Rotation.LookAt( baseDirection.Normal );
		rotation *= Rotation.FromYaw( randomYaw );
		rotation *= Rotation.FromPitch( randomPitch );

		return rotation.Forward;
	}

	protected virtual Vector3 GetBaseShootDirection()
	{
		return GetOwnerAimDirection();
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

	protected virtual void PlayImpactEffect( SceneTraceResult trace )
	{
		var impactPrefab = GetImpactPrefab( trace );

		if ( !impactPrefab.IsValid() )
			return;

		var impact = impactPrefab.Clone();

		impact.WorldPosition = trace.HitPosition;
		impact.WorldRotation = Rotation.LookAt( trace.Normal );

		if ( ImpactLifeTime > 0f )
		{
			var destroyAfterTime = impact.Components.Create<DestroyAfterTime>();
			destroyAfterTime.LifeTime = ImpactLifeTime;
		}
	}

	protected virtual GameObject GetImpactPrefab( SceneTraceResult trace )
	{
		if ( HasTagInHierarchy( trace.GameObject, "water" ) && WaterImpactPrefab.IsValid() )
			return WaterImpactPrefab;

		if ( IsFleshHit( trace ) && FleshImpactPrefab.IsValid() )
			return FleshImpactPrefab;

		if ( HasTagInHierarchy( trace.GameObject, "glass" ) && GlassImpactPrefab.IsValid() )
			return GlassImpactPrefab;

		if ( HasTagInHierarchy( trace.GameObject, "metal" ) && MetalImpactPrefab.IsValid() )
			return MetalImpactPrefab;

		if ( HasTagInHierarchy( trace.GameObject, "wood" ) && WoodImpactPrefab.IsValid() )
			return WoodImpactPrefab;

		if ( HasTagInHierarchy( trace.GameObject, "brick" ) && BrickImpactPrefab.IsValid() )
			return BrickImpactPrefab;

		if ( HasTagInHierarchy( trace.GameObject, "concrete" ) && BrickImpactPrefab.IsValid() )
			return BrickImpactPrefab;

		return DefaultImpactPrefab;
	}

	private bool HasTagInHierarchy( GameObject gameObject, string tag )
	{
		var current = gameObject;

		while ( current.IsValid() )
		{
			if ( current.Tags.Has( tag ) )
				return true;

			current = current.Parent;
		}

		return false;
	}

	private bool IsFleshHit( SceneTraceResult trace )
	{
		if ( trace.GameObject.Components.Get<FloodPlayer>( FindMode.EverythingInSelfAndAncestors ).IsValid() )
			return true;

		if ( trace.GameObject.Components.Get<PlayerHealth>( FindMode.EverythingInSelfAndAncestors ).IsValid() )
			return true;

		if ( HasTagInHierarchy( trace.GameObject, "player" ) )
			return true;

		if ( HasTagInHierarchy( trace.GameObject, "flesh" ) )
			return true;

		return false;
	}

	protected virtual void AddSpread()
	{
		CurrentSpreadDegrees += SpreadPerShot;
		CurrentSpreadDegrees = CurrentSpreadDegrees.Clamp( BaseSpreadDegrees, MaxSpreadDegrees );
	}

	protected virtual void UpdateSpreadRecovery()
	{
		if ( CurrentSpreadDegrees <= BaseSpreadDegrees )
		{
			CurrentSpreadDegrees = BaseSpreadDegrees;
			return;
		}

		CurrentSpreadDegrees -= SpreadRecoveryRate * Time.Delta;
		CurrentSpreadDegrees = CurrentSpreadDegrees.Clamp( BaseSpreadDegrees, MaxSpreadDegrees );
	}

	protected virtual void ApplyRecoil()
	{
		var camera = OwnerPlayer?.Components.Get<FloodPlayerCamera>();

		if ( !camera.IsValid() )
			return;

		var pitch = -RecoilPitch;
		var yaw = Game.Random.Float( -RecoilYaw, RecoilYaw );

		camera.AddViewPunch( new Angles( pitch, yaw, 0f ) );
	}

	protected virtual void CancelReload()
	{
		if ( !Networking.IsHost )
			return;

		if ( !IsReloading )
			return;

		IsReloading = false;

		ClearOneShotAnimationParams();
	}

	private bool IsAimRequestReasonable( Vector3 start, Vector3 direction )
	{
		var owner = OwnerPlayer;

		if ( !owner.IsValid() )
			return false;

		if ( direction.Length < 0.1f )
			return false;

		var maxEyeDistance = 160f;
		return (start - owner.WorldPosition).Length <= maxEyeDistance;
	}
}
