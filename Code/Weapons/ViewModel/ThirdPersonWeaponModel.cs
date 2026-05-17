using Sandbox;

public sealed class ThirdPersonWeaponModel : Component
{
	[Property, Group( "Model" )]
	public Model WorldModel { get; set; }

	[Property, Group( "Body" )]
	public string HandBoneName { get; set; } = "hold_R";

	[Property, Group( "Body Animation" )]
    public int HoldType { get; set; } = 6; // 6 = melee_weapons

    [Property, Group( "Body Animation" )]
    public int HoldTypeHandedness { get; set; } = 0; // 0 = 2H

	[Property, Group( "Body Animation" )]
	public float HoldTypePose { get; set; } = 0f;

	[Property, Group( "Body Animation" )]
	public float HoldTypeAttack { get; set; } = 0f;

	[Property, Group( "Body Animation" )]
	public string AttackTrigger { get; set; } = "b_attack";

	[Property, Group( "Visibility" )]
	public bool HideForLocalPlayer { get; set; } = true;

	[Property, Group( "Offset" )]
	public Vector3 PositionOffset { get; set; } = Vector3.Zero;

	[Property, Group( "Offset" )]
	public Angles RotationOffset { get; set; } = Angles.Zero;

	private GameObject WorldModelObject { get; set; }
	private ModelRenderer WorldRenderer { get; set; }
	private SkinnedModelRenderer BodyRenderer { get; set; }

	protected override void OnStart()
	{
		CreateWorldModel();
		SetVisible( false );
	}

	protected override void OnUpdate()
	{
		EnsureBodyRenderer();

		UpdateBodyHoldType();
		UpdateVisibilityForOwner();
		FollowHandBone();
	}

	protected override void OnDestroy()
	{
		if ( WorldModelObject.IsValid() )
			WorldModelObject.Destroy();
	}

	public void Show()
	{
		SetVisible( true );
		UpdateBodyHoldType();
	}

	public void Hide()
	{
		SetVisible( false );
		ClearBodyHoldType();
	}

	public void PlayAttack()
	{
		if ( !EnsureBodyRenderer() )
			return;

		BodyRenderer.Set( "holdtype_attack", HoldTypeAttack );
		BodyRenderer.Set( AttackTrigger, true );
	}

	private void CreateWorldModel()
	{
		if ( !WorldModel.IsValid() )
		{
			Log.Warning( $"{GameObject.Name} has no third-person WorldModel assigned." );
			return;
		}

		WorldModelObject = new GameObject( $"{GameObject.Name} ThirdPersonModel" );
		WorldModelObject.SetParent( GameObject );

		WorldRenderer = WorldModelObject.Components.Create<ModelRenderer>();
		WorldRenderer.Model = WorldModel;
	}

	private void UpdateBodyHoldType()
    {
	    if ( !EnsureBodyRenderer() )
		    return;

	    BodyRenderer.Set( "holdtype", HoldType );
	    BodyRenderer.Set( "holdtype_handedness", HoldTypeHandedness );
	    BodyRenderer.Set( "holdtype_pose", HoldTypePose );
    }

	private void ClearBodyHoldType()
    {
	    if ( !EnsureBodyRenderer() )
		    return;

	    BodyRenderer.Set( "holdtype", 0 ); // 0 = none
	    BodyRenderer.Set( "holdtype_pose", -1f );
    }

	private void FollowHandBone()
	{
		if ( !WorldModelObject.IsValid() )
			return;

		if ( !EnsureBodyRenderer() )
			return;

		if ( !BodyRenderer.TryGetBoneTransform( HandBoneName, out var boneTransform ) )
			return;

		var handRotation = boneTransform.Rotation;

		WorldModelObject.WorldPosition =
			boneTransform.Position +
			handRotation.Forward * PositionOffset.x +
			handRotation.Right * PositionOffset.y +
			handRotation.Up * PositionOffset.z;

		WorldModelObject.WorldRotation = handRotation * RotationOffset.ToRotation();
	}

	private void UpdateVisibilityForOwner()
	{
		if ( !WorldModelObject.IsValid() )
			return;

		if ( !HideForLocalPlayer )
			return;

		var inventory = Inventory;

		if ( !inventory.IsValid() )
			return;

		if ( !inventory.IsProxy )
			WorldModelObject.Enabled = false;
	}

	private bool EnsureBodyRenderer()
	{
		if ( BodyRenderer.IsValid() )
			return true;

		BodyRenderer = FindBodyRenderer();

		return BodyRenderer.IsValid();
	}

	private SkinnedModelRenderer FindBodyRenderer()
	{
		var inventory = Inventory;

		if ( !inventory.IsValid() )
			return null;

		return inventory.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
	}

	private PlayerInventory Inventory
	{
		get
		{
			var carryable = Components.Get<BaseCarryable>( FindMode.EverythingInSelfAndAncestors );

			if ( !carryable.IsValid() )
				return null;

			return carryable.Inventory;
		}
	}

	private void SetVisible( bool visible )
	{
		if ( !WorldModelObject.IsValid() )
			return;

		WorldModelObject.Enabled = visible;
	}
}