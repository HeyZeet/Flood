using Sandbox;

public sealed class ShotgunWeapon : BaseGunWeapon
{
	[Property, Group( "Shotgun" )] public int PelletCount { get; set; } = 8;
	[Property, Group( "Shotgun" )] public float PelletSpreadDegrees { get; set; } = 5.5f;
	[Property, Group( "Shotgun Reload" )] public float EmptyFirstShellInsertInterval { get; set; } = 1.15f;
	[Property, Group( "Shotgun Reload" )] public float ShellInsertInterval { get; set; } = 1f;
	[Property, Group( "Shotgun Reload" )] public string ReloadingBool { get; set; } = "b_reloading";
	[Property, Group( "Shotgun Reload" )] public string ReloadingShellTrigger { get; set; } = "b_reloading_shell";
	[Property, Group( "Shotgun Reload" )] public string ReloadingFirstShellTrigger { get; set; } = "b_reloading_first_shell";
	[Property, Group( "Shotgun Reload" )] public string ReloadBodygroupBool { get; set; } = "reload_bodygroup";

	public override string DisplayName => "Shotgun";

	private TimeSince TimeSinceShellStarted { get; set; }
	private bool HasInsertedFirstReloadShell { get; set; }
	private bool LastShellUsedEmptyFirstAnimation { get; set; }
	private bool ReloadStartedEmpty { get; set; }
	private bool WaitingForReloadEnd { get; set; }

	public override void OnDeploy()
	{
		base.OnDeploy();

		Log.Info( "Shotgun deployed." );
	}

	protected override void OnStart()
	{
		base.OnStart();

		Damage = 9f;
		PrimaryFireRate = 0.85f;

		BulletRange = 2800f;
		BulletRadius = 2f;

		ClipSize = 6;
		AmmoInClip = ClipSize;
		ReserveAmmo = 24;
		InfiniteAmmo = false;
		HasReserveAmmo = true;
		ReloadTime = 2.1f;

		BaseSpreadDegrees = 2f;
		MaxSpreadDegrees = 8f;
		SpreadPerShot = 1.2f;
		SpreadRecoveryRate = 2.5f;

		RecoilPitch = 3.4f;
		RecoilYaw = 1.1f;

		HoldType = "shotgun";
	}

	public override void OnHolster()
	{
		StopShotgunReloadEffects();

		base.OnHolster();

		Log.Info( "Shotgun holstered." );
	}

	protected override bool CanPrimaryAttackWhileReloading()
	{
		return HasAmmo();
	}

	public override void PrimaryAttack()
	{
		if ( !Networking.IsHost && IsReloading )
			StopShotgunReloadEffects();

		base.PrimaryAttack();
	}

	protected override void PrimaryAttackHost( Vector3 start, Vector3 baseDirection )
	{
		if ( IsReloading )
			FinishShotgunReload();

		base.PrimaryAttackHost( start, baseDirection );
	}

	protected override bool CanReload()
	{
		return base.CanReload();
	}

	protected override void StartReload( bool ownerAlreadyPredicted = true )
	{
		if ( !Networking.IsHost )
		{
			if ( CanReload() )
				PlayShotgunReloadStart();

			RequestShotgunReload();
			return;
		}

		if ( !CanReload() )
			return;

		IsReloading = true;
		HasInsertedFirstReloadShell = false;
		LastShellUsedEmptyFirstAnimation = false;
		ReloadStartedEmpty = AmmoInClip <= 0;
		WaitingForReloadEnd = false;
		TimeSinceShellStarted = 0f;

		PlayShotgunReloadStart();
		BroadcastShotgunReloadStart( ownerAlreadyPredicted );
	}

	[Rpc.Host]
	private void RequestShotgunReload()
	{
		StartReload();
	}

	protected override void UpdateReload()
	{
		if ( !IsReloading )
			return;

		if ( WaitingForReloadEnd )
		{
			if ( TimeSinceShellStarted >= GetCurrentShellInsertInterval() )
				FinishShotgunReload();

			return;
		}

		var insertInterval = GetCurrentShellInsertInterval();

		if ( TimeSinceShellStarted < insertInterval )
			return;

		InsertShell();

		if ( AmmoInClip >= ClipSize || (HasReserveAmmo && ReserveAmmo <= 0) )
		{
			WaitingForReloadEnd = true;
			TimeSinceShellStarted = 0f;
			return;
		}

		TimeSinceShellStarted = 0f;
	}

	protected override void FinishReload()
	{
		FinishShotgunReload();
	}

	protected override void CancelReload()
	{
		if ( !Networking.IsHost )
			return;

		if ( !IsReloading )
			return;

		FinishShotgunReload();
	}

	protected override void FireBullet( Vector3 start, Vector3 direction )
	{
		var pelletCount = PelletCount.Clamp( 1, 32 );
		var spread = PelletSpreadDegrees.Clamp( 0f, 45f );
		var baseRotation = Rotation.LookAt( direction.Normal );

		for ( var i = 0; i < pelletCount; i++ )
		{
			var yaw = Game.Random.Float( -spread, spread );
			var pitch = Game.Random.Float( -spread, spread );
			var pelletRotation = baseRotation * Rotation.FromYaw( yaw ) * Rotation.FromPitch( pitch );

			base.FireBullet( start, pelletRotation.Forward );
		}
	}

	private void InsertShell()
	{
		if ( !Networking.IsHost )
			return;

		if ( AmmoInClip >= ClipSize )
			return;

		if ( HasReserveAmmo && ReserveAmmo <= 0 )
			return;

		var shouldUseEmptyFirstShellAnimation = ReloadStartedEmpty && !HasInsertedFirstReloadShell;

		AmmoInClip++;

		if ( HasReserveAmmo )
			ReserveAmmo--;

		HasInsertedFirstReloadShell = true;
		LastShellUsedEmptyFirstAnimation = shouldUseEmptyFirstShellAnimation;

		PlayShotgunShellInsert( shouldUseEmptyFirstShellAnimation );
		BroadcastShotgunShellInsert( shouldUseEmptyFirstShellAnimation );
	}

	private void FinishShotgunReload()
	{
		if ( !Networking.IsHost )
			return;

		if ( !IsReloading )
			return;

		IsReloading = false;
		HasInsertedFirstReloadShell = false;
		LastShellUsedEmptyFirstAnimation = false;
		ReloadStartedEmpty = false;
		WaitingForReloadEnd = false;
		TimeSinceShellStarted = 0f;

		PlayShotgunReloadEnd();
		BroadcastShotgunReloadEnd();
	}

	private float GetCurrentShellInsertInterval()
	{
		if ( LastShellUsedEmptyFirstAnimation )
			return System.MathF.Max( EmptyFirstShellInsertInterval, 0.05f );

		if ( ReloadStartedEmpty && !HasInsertedFirstReloadShell )
			return System.MathF.Max( EmptyFirstShellInsertInterval, 0.05f );

		return System.MathF.Max( ShellInsertInterval, 0.05f );
	}

	private void PlayShotgunReloadStart()
	{
		if ( ReloadSound is not null )
			Sound.Play( ReloadSound, WorldPosition );

		var viewModel = Components.Get<ViewModelWeapon>( FindMode.EverythingInSelfAndDescendants );

		if ( !viewModel.IsValid() )
			return;

		viewModel.SetAnimationBool( ReloadingBool, true );
		viewModel.SetAnimationBool( ReloadBodygroupBool, true );
	}

	private void PlayShotgunShellInsert( bool firstShell )
	{
		var viewModel = Components.Get<ViewModelWeapon>( FindMode.EverythingInSelfAndDescendants );

		if ( !viewModel.IsValid() )
			return;

		viewModel.PlayAnimation( firstShell ? ReloadingFirstShellTrigger : ReloadingShellTrigger );
	}

	private void PlayShotgunReloadEnd()
	{
		StopShotgunReloadEffects();
	}

	private void StopShotgunReloadEffects()
	{
		var viewModel = Components.Get<ViewModelWeapon>( FindMode.EverythingInSelfAndDescendants );

		if ( !viewModel.IsValid() )
			return;

		viewModel.SetAnimationBool( ReloadingBool, false );
		viewModel.SetAnimationBool( ReloadingShellTrigger, false );
		viewModel.SetAnimationBool( ReloadingFirstShellTrigger, false );
		viewModel.SetAnimationBool( ReloadBodygroupBool, false );
		viewModel.SetAnimationBool( ReloadTrigger, false );
	}

	private void BroadcastShotgunReloadStart( bool skipLocalOwner = true )
	{
		if ( !Networking.IsHost )
			return;

		PlayShotgunReloadStartBroadcast( skipLocalOwner );
	}

	[Rpc.Broadcast]
	private void PlayShotgunReloadStartBroadcast( bool skipLocalOwner )
	{
		if ( skipLocalOwner && IsLocallyControlled() )
			return;

		PlayShotgunReloadStart();
	}

	private void BroadcastShotgunShellInsert( bool firstShell )
	{
		if ( !Networking.IsHost )
			return;

		PlayShotgunShellInsertBroadcast( firstShell );
	}

	[Rpc.Broadcast]
	private void PlayShotgunShellInsertBroadcast( bool firstShell )
	{
		PlayShotgunShellInsert( firstShell );
	}

	private void BroadcastShotgunReloadEnd()
	{
		if ( !Networking.IsHost )
			return;

		PlayShotgunReloadEndBroadcast();
	}

	[Rpc.Broadcast]
	private void PlayShotgunReloadEndBroadcast()
	{
		PlayShotgunReloadEnd();
	}
}
