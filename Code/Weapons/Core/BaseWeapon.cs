using Sandbox;

public abstract class BaseWeapon : BaseCarryable
{
	[Property, Group( "Damage" )] public float Damage { get; set; } = 10f;
	[Property, Group( "Damage" )] public bool IgnoreRoundDamageRules { get; set; } = false;
	[Property, Group( "Fire Rates" )] public float PrimaryFireRate { get; set; } = 0.5f;
	[Property, Group( "Fire Rates" )] public float SecondaryFireRate { get; set; } = 0.5f;

	[Property, Group( "Sounds" )] public SoundEvent DeploySound { get; set; }

	[Property, Group( "Input" )] public bool RepeatPrimaryAttackWhileHeld { get; set; } = true;
	[Property, Group( "Input" )] public bool RepeatSecondaryAttackWhileHeld { get; set; } = true;

	[Property, Group( "Animation" )] public SkinnedModelRenderer AnimationRenderer { get; set; }
	[Property, Group( "Animation" )] public string HoldType { get; set; } = "holditem";
	[Property, Group( "Animation" )] public bool PlayDeployAnimation { get; set; } = true;
	[Property, Group( "Animation" )] public bool PlayAttackAnimation { get; set; } = true;

	[Property, Group( "Animation Parameters" )] public string DeployTrigger { get; set; } = "b_deploy";
	[Property, Group( "Animation Parameters" )] public string AttackTrigger { get; set; } = "b_attack";
	[Property, Group( "Animation Parameters" )] public string DryAttackTrigger { get; set; } = "b_attack_dry";
	[Property, Group( "Animation Parameters" )] public string ReloadTrigger { get; set; } = "b_reload";

	protected TimeSince TimeSincePrimaryAttack { get; private set; }
	protected TimeSince TimeSinceSecondaryAttack { get; private set; }
	
	protected FloodPlayer OwnerPlayer
	{
		get
		{
			if ( !Inventory.IsValid() )
				return null;

			return Inventory.Components.Get<FloodPlayer>();
		}
	}

	protected PlayerController OwnerController
	{
		get
		{
			if ( !Inventory.IsValid() )
				return null;

			return Inventory.Components.Get<PlayerController>();
		}
	}

	protected void PlayWeaponAnimation( string triggerName )
	{
		if ( string.IsNullOrWhiteSpace( triggerName ) )
			return;

		TriggerAnimationBool( triggerName );
		TriggerViewModelAnimation( triggerName );
		TriggerThirdPersonAnimation( triggerName );
	}

	protected void BroadcastWeaponAnimation( string triggerName, bool skipLocalOwner = true )
	{
		if ( !Networking.IsHost )
			return;

		if ( string.IsNullOrWhiteSpace( triggerName ) )
			return;

		PlayWeaponAnimationBroadcast( triggerName, skipLocalOwner );
	}

	[Rpc.Broadcast]
	private void PlayWeaponAnimationBroadcast( string triggerName, bool skipLocalOwner )
	{
		if ( skipLocalOwner && IsLocallyControlled() )
			return;

		PlayWeaponAnimation( triggerName );
	}

	protected void TriggerViewModelAnimation( string triggerName )
	{
		if ( !ShouldShowViewModel() )
			return;

		var viewModel = Components.Get<ViewModelWeapon>( FindMode.EverythingInSelfAndDescendants );

		if ( viewModel.IsValid() )
			viewModel.PlayAnimation( triggerName );
	}

	protected void TriggerThirdPersonAnimation( string triggerName )
	{
		var thirdPersonModel = Components.Get<ThirdPersonWeaponModel>( FindMode.EverythingInSelfAndDescendants );

		if ( thirdPersonModel.IsValid() )
			thirdPersonModel.PlayAnimation( triggerName );
	}

	public override void OnAddedToInventory( PlayerInventory inventory )
	{
		base.OnAddedToInventory( inventory );

		TimeSincePrimaryAttack = 999f;
		TimeSinceSecondaryAttack = 999f;
	}

	public override void OnDeploy()
	{
		base.OnDeploy();

		ShowWeaponVisuals();

		ClearOneShotAnimationParams();
		ApplyHoldType();
		PlayDeploySound();

		if ( PlayDeployAnimation )
			PlayWeaponAnimation( DeployTrigger );
	}

	public override void OnHolster()
	{
		HideWeaponVisuals();

		ClearOneShotAnimationParams();

		base.OnHolster();
	}

	public override void OnPlayerUpdate()
	{
		if ( IsPrimaryAttackInputActive() )
			TryPrimaryAttack();

		if ( IsSecondaryAttackInputActive() )
			TrySecondaryAttack();
	}

	public void TryPrimaryAttack()
	{
		if ( !CanPrimaryAttack() )
			return;

		ResetPrimaryAttackCooldown();
		PrimaryAttack();
	}

	public void TrySecondaryAttack()
	{
		if ( !CanSecondaryAttack() )
			return;

		ResetSecondaryAttackCooldown();
		SecondaryAttack();
	}

	public override bool CanPrimaryAttack()
	{
		if ( !base.CanPrimaryAttack() )
			return false;

		if ( PrimaryFireRate <= 0f )
			return true;

		return TimeSincePrimaryAttack >= PrimaryFireRate;
	}

	public override bool CanSecondaryAttack()
	{
		if ( !base.CanSecondaryAttack() )
			return false;

		if ( SecondaryFireRate <= 0f )
			return true;

		return TimeSinceSecondaryAttack >= SecondaryFireRate;
	}

	public override void PrimaryAttack()
	{
		if ( PlayAttackAnimation )
			PlayWeaponAnimation( AttackTrigger );
	}

	public override void SecondaryAttack()
	{
		Log.Info( $"{DisplayName} secondary attack." );
	}

	protected virtual void PlayDeploySound()
	{
		if ( DeploySound is null )
			return;

		Sound.Play( DeploySound, WorldPosition );
	}

	protected void ShowWeaponVisuals()
	{
		var viewModel = Components.Get<ViewModelWeapon>( FindMode.EverythingInSelfAndDescendants );

		if ( viewModel.IsValid() && ShouldShowViewModel() )
		{
			viewModel.GameObject.Enabled = true;
			viewModel.Show();
		}

		var thirdPersonModel = Components.Get<ThirdPersonWeaponModel>( FindMode.EverythingInSelfAndDescendants );

		if ( thirdPersonModel.IsValid() )
		{
			thirdPersonModel.GameObject.Enabled = true;
			thirdPersonModel.Show();
		}
	}

	protected void HideWeaponVisuals()
	{
		var viewModel = Components.Get<ViewModelWeapon>( FindMode.EverythingInSelfAndDescendants );

		if ( viewModel.IsValid() )
			viewModel.Hide();

		var thirdPersonModel = Components.Get<ThirdPersonWeaponModel>( FindMode.EverythingInSelfAndDescendants );

		if ( thirdPersonModel.IsValid() )
			thirdPersonModel.Hide();
	}

	protected bool ShouldShowViewModel()
	{
		return IsLocallyControlled();
	}

	protected bool IsLocallyControlled()
	{
		var owner = OwnerPlayer;

		if ( owner.IsValid() )
			return owner.IsLocalPlayer;

		return Inventory.IsValid() && !Inventory.IsProxy;
	}

	protected void ClearOneShotAnimationParams()
	{
		var renderer = GetAnimationRenderer();

		if ( !renderer.IsValid() )
			return;

		renderer.Set( AttackTrigger, false );
		renderer.Set( DryAttackTrigger, false );
		renderer.Set( DeployTrigger, false );
		renderer.Set( ReloadTrigger, false );
	}

	protected void TriggerAnimationBool( string parameterName )
	{
		var renderer = GetAnimationRenderer();

		if ( !renderer.IsValid() )
			return;

		renderer.Set( parameterName, false );
		renderer.Set( parameterName, true );
	}

	protected void ApplyHoldType()
	{
		if ( string.IsNullOrWhiteSpace( HoldType ) )
			return;

		var renderer = GetAnimationRenderer();

		if ( !renderer.IsValid() )
			return;

		ClearHoldTypeTags( renderer );

		renderer.GameObject.Tags.Add( HoldType );
	}

	private void ClearHoldTypeTags( SkinnedModelRenderer renderer )
	{
		if ( !renderer.IsValid() )
			return;

		renderer.GameObject.Tags.Remove( "none" );
		renderer.GameObject.Tags.Remove( "pistol" );
		renderer.GameObject.Tags.Remove( "rifle" );
		renderer.GameObject.Tags.Remove( "shotgun" );
		renderer.GameObject.Tags.Remove( "holditem" );
		renderer.GameObject.Tags.Remove( "melee_punch" );
		renderer.GameObject.Tags.Remove( "melee_weapons" );
		renderer.GameObject.Tags.Remove( "rpg" );
		renderer.GameObject.Tags.Remove( "physgun" );
	}

	protected SkinnedModelRenderer GetAnimationRenderer()
	{
		if ( AnimationRenderer.IsValid() )
			return AnimationRenderer;

		var owner = OwnerPlayer;

		if ( !owner.IsValid() )
			return null;

		AnimationRenderer = owner.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );

		return AnimationRenderer;
	}

	protected Vector3 GetOwnerEyePosition()
	{
		var camera = OwnerPlayer?.Components.Get<FloodPlayerCamera>();

		if ( camera.IsValid() )
			return camera.EyePosition;

		var sceneCamera = Scene.Camera;

		if ( sceneCamera.IsValid() )
			return sceneCamera.WorldPosition;

		return WorldPosition;
	}

	protected Vector3 GetOwnerAimDirection()
	{
		var camera = OwnerPlayer?.Components.Get<FloodPlayerCamera>();

		if ( camera.IsValid() )
			return camera.AimForward;

		var sceneCamera = Scene.Camera;

		if ( sceneCamera.IsValid() )
			return sceneCamera.WorldRotation.Forward;

		return WorldRotation.Forward;
	}

	protected SceneTraceResult TraceFromOwnerAim( float range, float radius )
	{
		return TraceFromAim( GetOwnerEyePosition(), GetOwnerAimDirection(), range, radius );
	}

	protected SceneTraceResult TraceFromAim( Vector3 start, Vector3 direction, float range, float radius )
	{
		direction = direction.Normal;

		if ( direction.Length.AlmostEqual( 0f ) )
			direction = WorldRotation.Forward;

		var end = start + direction * range;
		var trace = Scene.Trace.Sphere( radius, start, end );

		var owner = OwnerPlayer;

		if ( owner.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( owner.GameObject );

		return trace.Run();
	}

	protected void ResetPrimaryAttackCooldown()
	{
		TimeSincePrimaryAttack = 0f;
	}

	protected void ResetSecondaryAttackCooldown()
	{
		TimeSinceSecondaryAttack = 0f;
	}

	private bool IsPrimaryAttackInputActive()
	{
		return RepeatPrimaryAttackWhileHeld ? Input.Down( "attack1" ) : Input.Pressed( "attack1" );
	}

	private bool IsSecondaryAttackInputActive()
	{
		return RepeatSecondaryAttackWhileHeld ? Input.Down( "attack2" ) : Input.Pressed( "attack2" );
	}
}
