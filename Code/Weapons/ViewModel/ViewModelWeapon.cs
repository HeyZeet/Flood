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
		CreateViewModel();

		ClearViewModelOneShotParams();
		SetVisible( false );
	}

	protected override void OnUpdate()
	{
		FollowCamera();
	}

	protected override void OnDestroy()
	{
		if ( ViewModelObject.IsValid() )
			ViewModelObject.Destroy();
	}

	public void Show()
	{
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
		if ( !WeaponModel.IsValid() )
		{
			Log.Warning( $"{GameObject.Name} has no WeaponModel assigned." );
			return;
		}

		ViewModelObject = new GameObject( $"{GameObject.Name} ViewModel" );
		ViewModelObject.SetParent( GameObject );

		WeaponRenderer = ViewModelObject.Components.Create<SkinnedModelRenderer>();
		WeaponRenderer.Model = WeaponModel;

		CreateArmsRenderer();
	}

	private void CreateArmsRenderer()
	{
		if ( !ArmsModel.IsValid() )
			return;

		var armsObject = new GameObject( $"{GameObject.Name} ViewModel Arms" );
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