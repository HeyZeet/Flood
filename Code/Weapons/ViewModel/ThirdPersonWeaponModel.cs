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

	[Property, Group( "Muzzle" )]
	public Vector3 MuzzleLocalOffset { get; set; } = new Vector3( 8f, 0f, 0f );

	[Property, Group( "Muzzle Debug" )]
	public bool ShowDebugMuzzleFlash { get; set; } = false;

	[Property, Group( "Muzzle Debug" )]
	public GameObject DebugMuzzleFlashPrefab { get; set; }

	[Property, Group( "Muzzle Debug" )]
	public float DebugMuzzleFlashScale { get; set; } = 1f;

	[Property, Group( "Muzzle" )]
	public Angles MuzzleRotationOffset { get; set; } = Angles.Zero;

	private GameObject WorldModelObject { get; set; }
	private ModelRenderer WorldRenderer { get; set; }
	private SkinnedModelRenderer BodyRenderer { get; set; }
	private GameObject DebugMuzzleFlashObject { get; set; }

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
		UpdateDebugMuzzleFlash();
	}

	protected override void OnDestroy()
	{
		DestroyDebugMuzzleFlash();

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
		BodyRenderer.Set( AttackTrigger, false );
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

	public bool ShouldHideForLocalPlayer()
	{
		if ( !HideForLocalPlayer )
			return false;

		var inventory = Inventory;

		if ( !inventory.IsValid() )
			return false;

		return !inventory.IsProxy;
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

	public Transform GetMuzzleTransform()
	{
		if ( !WorldModelObject.IsValid() )
			return WorldTransform;

		var rotation = WorldModelObject.WorldRotation;

		var position =
			WorldModelObject.WorldPosition +
			rotation.Forward * MuzzleLocalOffset.x +
			rotation.Right * MuzzleLocalOffset.y +
			rotation.Up * MuzzleLocalOffset.z;

		return new Transform( position, rotation * MuzzleRotationOffset.ToRotation() );
	}

	private void UpdateDebugMuzzleFlash()
	{
		if ( !ShowDebugMuzzleFlash )
		{
			DestroyDebugMuzzleFlash();
			return;
		}

		if ( !DebugMuzzleFlashPrefab.IsValid() )
			return;

		if ( !WorldModelObject.IsValid() )
			return;

		if ( !DebugMuzzleFlashObject.IsValid() )
		{
			DebugMuzzleFlashObject = DebugMuzzleFlashPrefab.Clone();
			DebugMuzzleFlashObject.Name = $"{GameObject.Name} Debug Muzzle Flash";
		}

		var muzzleTransform = GetMuzzleTransform();

		DebugMuzzleFlashObject.WorldPosition = muzzleTransform.Position;
		DebugMuzzleFlashObject.WorldRotation = muzzleTransform.Rotation;
		DebugMuzzleFlashObject.WorldScale = new Vector3( DebugMuzzleFlashScale );
	}

	private void DestroyDebugMuzzleFlash()
	{
		if ( !DebugMuzzleFlashObject.IsValid() )
			return;

		DebugMuzzleFlashObject.Destroy();
		DebugMuzzleFlashObject = null;
	}

}