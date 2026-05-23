using Sandbox;

public sealed class ViewModelWeapon : Component
{
	[Property, Group( "Models" )]
	public Model WeaponModel { get; set; }

	[Property, Group( "Models" )]
	public Model ArmsModel { get; set; }

	[Property, Group( "View" )]
	public Vector3 PositionOffset { get; set; } = new Vector3( 8f, -8f, -8f );

	[Property, Group( "View" )]
	public Angles RotationOffset { get; set; } = new Angles( 0f, 0f, 0f );

	[Property, Group( "Animation" )]
	public string DeployTrigger { get; set; } = "b_deploy";

	[Property, Group( "Animation" )]
	public string AttackTrigger { get; set; } = "b_attack";

	[Property, Group( "Animation" )]
	public string ReloadTrigger { get; set; } = "b_reload";

	[Property, Group( "Animation" )]
	public string DryAttackTrigger { get; set; } = "b_attack_dry";

	private GameObject ViewModelObject { get; set; }
	private SkinnedModelRenderer WeaponRenderer { get; set; }
	private SkinnedModelRenderer ArmsRenderer { get; set; }

	protected override void OnStart()
	{
		if ( ShouldUseViewModel() )
			CreateViewModel();

		ClearViewModelOneShotParams();
		SetVisible( false );
	}

	protected override void OnUpdate()
	{
		if ( !ShouldUseViewModel() )
		{
			DestroyViewModel();
			return;
		}

		if ( !IsCarryableActive() )
		{
			SetVisible( false );
			return;
		}

		EnsureViewModelCreated();
		FollowCamera();
	}

	protected override void OnDestroy()
	{
		DestroyViewModel();
	}

	public void Show()
	{
		if ( !ShouldUseViewModel() )
		{
			SetVisible( false );
			return;
		}

		EnsureViewModelCreated();
		SetVisible( true );

		ClearViewModelOneShotParams();
		PlayDeploy();
	}

	public void Hide()
	{
		ClearViewModelOneShotParams();

		SetVisible( false );
	}

	public void PlayDeploy()
	{
		PlayAnimation( DeployTrigger );
	}

	public void PlayAttack()
	{
		PlayAnimation( AttackTrigger );
	}

	public void PlayReload()
	{
		PlayAnimation( ReloadTrigger );
	}

	public void PlayDryAttack()
	{
		PlayAnimation( DryAttackTrigger );
	}

	public void PlayAnimation( string triggerName )
	{
		if ( !ShouldUseViewModel() )
			return;

		SetAnimationTrigger( triggerName );
	}

	public bool TryGetBoneTransform( string boneName, out Transform boneTransform )
	{
		boneTransform = default;

		if ( !WeaponRenderer.IsValid() )
			return false;

		return WeaponRenderer.TryGetBoneTransform( boneName, out boneTransform );
	}

	private void CreateViewModel()
	{
		if ( ViewModelObject.IsValid() )
			return;

		if ( !WeaponModel.IsValid() )
		{
			Log.Warning( $"{GameObject.Name} has no WeaponModel assigned." );
			return;
		}

		ViewModelObject = new GameObject( true, $"{GameObject.Name} ViewModel" );
		ViewModelObject.NetworkMode = NetworkMode.Never;

		WeaponRenderer = ViewModelObject.Components.Create<SkinnedModelRenderer>();
		WeaponRenderer.Model = WeaponModel;

		CreateArmsRenderer();
	}

	private void EnsureViewModelCreated()
	{
		if ( ViewModelObject.IsValid() )
			return;

		CreateViewModel();
		SetVisible( false );
	}

	private void DestroyViewModel()
	{
		if ( !ViewModelObject.IsValid() )
			return;

		ViewModelObject.Destroy();
		ViewModelObject = null;
		WeaponRenderer = null;
		ArmsRenderer = null;
	}

	private void CreateArmsRenderer()
	{
		if ( !ArmsModel.IsValid() )
			return;

		var armsObject = new GameObject( true, $"{GameObject.Name} ViewModel Arms" );
		armsObject.NetworkMode = NetworkMode.Never;
		armsObject.SetParent( ViewModelObject );

		ArmsRenderer = armsObject.Components.Create<SkinnedModelRenderer>();
		ArmsRenderer.Model = ArmsModel;

		// This is the important part:
		// the arms renderer follows the weapon renderer's animated bones.
		ArmsRenderer.BoneMergeTarget = WeaponRenderer;
	}

	private void FollowCamera()
	{
		if ( !ViewModelObject.IsValid() )
			return;

		var camera = Scene.Camera;

		if ( !camera.IsValid() )
			return;

		var cameraRotation = camera.WorldRotation;

		ViewModelObject.WorldPosition =
			camera.WorldPosition +
			cameraRotation.Forward * PositionOffset.x +
			cameraRotation.Right * PositionOffset.y +
			cameraRotation.Up * PositionOffset.z;

		ViewModelObject.WorldRotation = cameraRotation * RotationOffset.ToRotation();
	}

	private void SetVisible( bool visible )
	{
		if ( !ViewModelObject.IsValid() )
			return;

		ViewModelObject.Enabled = visible;
	}

	private void SetAnimationTrigger( string triggerName )
	{
		if ( string.IsNullOrWhiteSpace( triggerName ) )
			return;

		if ( !WeaponRenderer.IsValid() )
			return;

		WeaponRenderer.Set( triggerName, false );
		WeaponRenderer.Set( triggerName, true );
	}

	private bool ShouldUseViewModel()
	{
		var carryable = Components.Get<BaseCarryable>( FindMode.EverythingInSelfAndAncestors );

		if ( !carryable.IsValid() )
			return false;

		var inventory = carryable.Inventory;

		if ( !inventory.IsValid() )
			return false;

		var player = inventory.Components.Get<FloodPlayer>();

		if ( player.IsValid() )
			return player.IsLocalPlayer;

		return !inventory.IsProxy;
	}

	private bool IsCarryableActive()
	{
		var carryable = Components.Get<BaseCarryable>( FindMode.EverythingInSelfAndAncestors );

		if ( !carryable.IsValid() )
			return false;

		return carryable.IsActive;
	}

	private void ClearViewModelOneShotParams()
	{
		if ( !WeaponRenderer.IsValid() )
			return;

		WeaponRenderer.Set( AttackTrigger, false );
		WeaponRenderer.Set( DryAttackTrigger, false );
		WeaponRenderer.Set( DeployTrigger, false );
		WeaponRenderer.Set( ReloadTrigger, false );
	}
}
