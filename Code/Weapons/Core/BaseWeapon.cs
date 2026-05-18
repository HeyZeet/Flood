using Sandbox;

public abstract class BaseWeapon : BaseCarryable
{
	[Property] public float Damage { get; set; } = 10f;
	[Property] public float PrimaryFireRate { get; set; } = 0.5f;
	[Property] public float SecondaryFireRate { get; set; } = 0.5f;

	[Header( "Animation" )]
	[Property] public SkinnedModelRenderer AnimationRenderer { get; set; }
	[Property] public string HoldType { get; set; } = "holditem";
	[Property] public bool PlayDeployAnimation { get; set; } = true;
	[Property] public bool PlayAttackAnimation { get; set; } = true;

	private TimeSince TimeSincePrimaryAttack { get; set; }
	private TimeSince TimeSinceSecondaryAttack { get; set; }

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

		if ( PlayDeployAnimation )
			TriggerAnimationBool( "b_deploy" );
	}

	public override void OnHolster()
	{
		HideWeaponVisuals();

		ClearOneShotAnimationParams();

		base.OnHolster();
	}

	public override void OnPlayerUpdate()
	{
		if ( Input.Down( "attack1" ) )
			TryPrimaryAttack();

		if ( Input.Down( "attack2" ) )
			TrySecondaryAttack();
	}

	public void TryPrimaryAttack()
	{
		if ( !CanPrimaryAttack() )
			return;

		TimeSincePrimaryAttack = 0f;
		PrimaryAttack();
	}

	public void TrySecondaryAttack()
	{
		if ( !CanSecondaryAttack() )
			return;

		TimeSinceSecondaryAttack = 0f;
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
			TriggerAnimationBool( "b_attack" );

		var viewModel = Components.Get<ViewModelWeapon>( FindMode.EverythingInSelfAndDescendants );

		if ( viewModel.IsValid() )
			viewModel.PlayAttack();

		var thirdPersonModel = Components.Get<ThirdPersonWeaponModel>( FindMode.EverythingInSelfAndDescendants );

		if ( thirdPersonModel.IsValid() )
			thirdPersonModel.PlayAttack();

		Log.Info( $"{DisplayName} primary attack." );
	}

	public override void SecondaryAttack()
	{
		Log.Info( $"{DisplayName} secondary attack." );
	}

	protected void ShowWeaponVisuals()
	{
		var viewModel = Components.Get<ViewModelWeapon>( FindMode.EverythingInSelfAndDescendants );

		if ( viewModel.IsValid() )
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

	protected void ClearOneShotAnimationParams()
	{
		var renderer = GetAnimationRenderer();

		if ( !renderer.IsValid() )
			return;

		renderer.Set( "b_attack", false );
		renderer.Set( "b_deploy", false );
		renderer.Set( "b_reload", false );
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
}