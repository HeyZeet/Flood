using Sandbox;

public sealed class ShotgunWeapon : BaseGunWeapon
{
	[Property, Group( "Shotgun" )] public int PelletCount { get; set; } = 8;
	[Property, Group( "Shotgun" )] public float PelletSpreadDegrees { get; set; } = 5.5f;
	[Property, Group( "Shotgun Reload Timing" )] public float EmptyFirstShellInsertInterval { get; set; } = 1.15f;
	[Property, Group( "Shotgun Reload Timing" )] public float ShellInsertInterval { get; set; } = 1f;
	[Property, Group( "Shotgun Reload Sounds" )] public SoundEvent EmptyFirstShellInsertSound { get; set; }
	[Property, Group( "Shotgun Reload Sounds" )] public SoundEvent ShellInsertSound { get; set; }
	[Property, Group( "Shotgun Reload Animation" )] public string ReloadingBool { get; set; } = "b_reloading";
	[Property, Group( "Shotgun Reload Animation" )] public string ReloadingShellTrigger { get; set; } = "b_reloading_shell";
	[Property, Group( "Shotgun Reload Animation" )] public string ReloadingFirstShellTrigger { get; set; } = "b_reloading_first_shell";
	[Property, Group( "Shotgun Reload Animation" )] public string ReloadBodygroupBool { get; set; } = "reload_bodygroup";

	public override string DisplayName => "Shotgun";

	private TimeSince TimeSinceShellStarted { get; set; }
	private bool HasInsertedFirstReloadShell { get; set; }
	private bool LastShellUsedEmptyFirstAnimation { get; set; }
	private bool ReloadStartedEmpty { get; set; }
	private bool WaitingForReloadEnd { get; set; }

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

	protected override void StartReload( bool ownerAlreadyPredicted = true )
	{
		if ( !Networking.IsHost )
		{
			if ( CanReload() )
				PlayShotgunReloadStart( AmmoInClip <= 0 );

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

		PlayShotgunReloadStart( ReloadStartedEmpty );
		BroadcastShotgunReloadStart( ReloadStartedEmpty, ownerAlreadyPredicted );
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

		if ( LastShellUsedEmptyFirstAnimation )
		{
			PlayShotgunReloadLoopStart();
			BroadcastShotgunReloadLoopStart( IsLocallyControlled() );
		}

		TimeSinceShellStarted = 0f;
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

		if ( !shouldUseEmptyFirstShellAnimation )
		{
			PlayShotgunShellInsert( false );
			BroadcastShotgunShellInsert( false, IsLocallyControlled() );
		}
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

		StopShotgunReloadEffects();
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

	private void PlayShotgunReloadStart( bool startsEmpty = false )
	{
		if ( ReloadSound is not null )
			Sound.Play( ReloadSound, WorldPosition );

		if ( startsEmpty )
		{
			PlayShotgunShellInsert( true );
			return;
		}

		PlayShotgunReloadLoopStart();
	}

	private void PlayShotgunReloadLoopStart()
	{
		var viewModel = Components.Get<ViewModelWeapon>( FindMode.EverythingInSelfAndDescendants );

		if ( viewModel.IsValid() )
		{
			viewModel.SetAnimationBool( ReloadingBool, true );
			viewModel.SetAnimationBool( ReloadBodygroupBool, true );
		}

		PlayThirdPersonReloadAnimation();
	}

	private void PlayShotgunShellInsert( bool firstShell )
	{
		PlayShotgunShellInsertSound( firstShell );

		var viewModel = Components.Get<ViewModelWeapon>( FindMode.EverythingInSelfAndDescendants );

		if ( viewModel.IsValid() )
			viewModel.PlayAnimation( firstShell ? ReloadingFirstShellTrigger : ReloadingShellTrigger );

		PlayThirdPersonReloadAnimation();
	}

	private void PlayShotgunShellInsertSound( bool firstShell )
	{
		var sound = firstShell ? EmptyFirstShellInsertSound : ShellInsertSound;

		if ( sound is not null )
			Sound.Play( sound, WorldPosition );
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

	private void PlayThirdPersonReloadAnimation()
	{
		var thirdPersonModel = Components.Get<ThirdPersonWeaponModel>( FindMode.EverythingInSelfAndDescendants );

		if ( !thirdPersonModel.IsValid() )
			return;

		if ( thirdPersonModel.ShouldHideForLocalPlayer() )
			return;

		thirdPersonModel.PlayAnimation( ReloadTrigger );
	}

	private void BroadcastShotgunReloadStart( bool startsEmpty, bool skipLocalOwner = true )
	{
		if ( !Networking.IsHost )
			return;

		PlayShotgunReloadStartBroadcast( startsEmpty, skipLocalOwner );
	}

	[Rpc.Broadcast]
	private void PlayShotgunReloadStartBroadcast( bool startsEmpty, bool skipLocalOwner )
	{
		if ( skipLocalOwner && IsLocallyControlled() )
			return;

		PlayShotgunReloadStart( startsEmpty );
	}

	private void BroadcastShotgunReloadLoopStart( bool skipLocalOwner )
	{
		if ( !Networking.IsHost )
			return;

		PlayShotgunReloadLoopStartBroadcast( skipLocalOwner );
	}

	[Rpc.Broadcast]
	private void PlayShotgunReloadLoopStartBroadcast( bool skipLocalOwner )
	{
		if ( skipLocalOwner && IsLocallyControlled() )
			return;

		PlayShotgunReloadLoopStart();
	}

	private void BroadcastShotgunShellInsert( bool firstShell, bool skipLocalOwner )
	{
		if ( !Networking.IsHost )
			return;

		PlayShotgunShellInsertBroadcast( firstShell, skipLocalOwner );
	}

	[Rpc.Broadcast]
	private void PlayShotgunShellInsertBroadcast( bool firstShell, bool skipLocalOwner )
	{
		if ( skipLocalOwner && IsLocallyControlled() )
			return;

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
		StopShotgunReloadEffects();
	}
}
