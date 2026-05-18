using Sandbox;
using System;

public abstract class BaseGunWeapon : BaseWeapon
{
	[Header( "Gun" )]
	[Property] public float BulletRange { get; set; } = 5000f;
	[Property] public float BulletRadius { get; set; } = 1.5f;
	[Property] public bool DrawDebugTrace { get; set; } = true;

    [Header( "Ammo" )]
    [Property] public int ReserveAmmo { get; set; } = 48;
    [Property] public bool HasReserveAmmo { get; set; } = true;
    [Property] public bool InfiniteAmmo { get; set; } = true;
	[Property] public int ClipSize { get; set; } = 12;
	[Property, Sync] public int AmmoInClip { get; set; } = 12;

    [Header( "Reload" )]
    [Property] public float ReloadTime { get; set; } = 1.4f;
    [Property] public SoundEvent ReloadSound { get; set; }

    public bool IsReloading { get; private set; }
    private TimeSince TimeSinceReloadStarted { get; set; }

	[Header( "Effects" )]
	[Property] public SoundEvent FireSound { get; set; }
	[Property] public SoundEvent DryFireSound { get; set; }

	[Header( "Muzzle Flash" )]
	[Property] public GameObject MuzzleFlashPrefab { get; set; }
	[Property] public string MuzzleBoneName { get; set; } = "muzzle";
	[Property] public Vector3 MuzzleFlashPositionOffset { get; set; } = Vector3.Zero;
	[Property] public Angles MuzzleFlashRotationOffset { get; set; } = Angles.Zero;
	[Property] public float MuzzleFlashLifeTime { get; set; } = 0.08f;

    protected virtual Vector3 GetBaseShootDirection()
    {
	    var camera = OwnerPlayer?.Components.Get<FloodPlayerCamera>();

	    if ( camera.IsValid() )
		    return camera.AimForward;

	    var sceneCamera = Scene.Camera;

	    if ( sceneCamera.IsValid() )
		    return sceneCamera.WorldRotation.Forward;

	    return WorldRotation.Forward;
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
	    var pitch = -RecoilPitch;
	    var yaw = Game.Random.Float( -RecoilYaw, RecoilYaw );

	    ViewRecoilOffset += new Angles( pitch, yaw, 0f );
    }

    protected virtual void UpdateRecoilRecovery()
    {
	    if ( ViewRecoilOffset == Angles.Zero )
		    return;

	    ViewRecoilOffset = Angles.Lerp( ViewRecoilOffset, Angles.Zero, RecoilRecoveryRate * Time.Delta );
    }

    public BaseGunWeapon ActiveGun
    {
	    get
	    {
		    var player = FloodPlayer.Local;

		    if ( !player.IsValid() )
			    return null;

		    var inventory = player.Inventory;

		    if ( !inventory.IsValid() )
			return null;

		    var activeCarryable = inventory.ActiveCarryable;

		    if ( !activeCarryable.IsValid() )
			return null;

		    if ( activeCarryable is BaseGunWeapon activeGun )
			return activeGun;

		    return activeCarryable.GameObject.Components.Get<BaseGunWeapon>( FindMode.EverythingInSelfAndDescendants );
	    }
    }

    public string AmmoDisplay
    {
	    get
	    {
		    if ( InfiniteAmmo )
			    return $"{AmmoInClip} / ∞";

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

	    UpdateReload();
	    UpdateSpreadRecovery();
	    UpdateRecoilRecovery();
    }

    public override void PrimaryAttack()
    {
	    if ( IsReloading )
		    return;

	    if ( !HasAmmo() )
	    {
		    PlayDryFireEffects();
		    return;
	    }

	    ConsumeAmmo();

	    PlayFireEffects();
	    FireBullet();
        AddSpread();
        ApplyRecoil();

	    base.PrimaryAttack();

	    if ( AmmoInClip <= 0 && HasReserveAmmo )
		    StartReload();
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

    protected virtual void StartReload()
    {
	    if ( !CanReload() )
		    return;

	    IsReloading = true;
	    TimeSinceReloadStarted = 0f;

	    PlayReloadEffects();
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
	    TriggerAnimationBool( "b_reload" );

	    var viewModel = Components.Get<ViewModelWeapon>( FindMode.EverythingInSelfAndDescendants );

	    if ( viewModel.IsValid() )
		    viewModel.PlayReload();
    }

    private void PlayFirstPersonMuzzleFlash()
    {
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
	    var baseDirection = GetBaseShootDirection();

	    if ( CurrentSpreadDegrees <= 0f )
		    return baseDirection;

	    var spread = CurrentSpreadDegrees.DegreeToRadian();

	    var randomYaw = Game.Random.Float( -spread, spread );
	    var randomPitch = Game.Random.Float( -spread, spread );

	    var rotation = Rotation.LookAt( baseDirection );
	    rotation *= Rotation.FromYaw( randomYaw.RadianToDegree() );
	    rotation *= Rotation.FromPitch( randomPitch.RadianToDegree() );

	    return rotation.Forward;
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

    protected virtual void CancelReload()
    {
	    if ( !IsReloading )
		    return;

	    IsReloading = false;

	    ClearOneShotAnimationParams();
    }
}