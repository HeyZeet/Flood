using Sandbox;

public abstract class BaseCarryable : Component
{
	[Property, Group( "Inventory" ), Sync( SyncFlags.FromHost )] public int InventorySlot { get; set; } = -1;
	[Property, Group( "Inventory" ), Sync( SyncFlags.FromHost )] public bool IsUnlocked { get; set; } = true;
	[Sync( SyncFlags.FromHost )] public GameObject InventoryObject { get; private set; }

	public PlayerInventory Inventory { get; set; }

	public bool IsActive
	{
		get
		{
			if ( !Inventory.IsValid() )
				return false;

			return Inventory.ActiveCarryable == this;
		}
	}

	public virtual string DisplayName => GameObject.Name;

	public virtual void OnAddedToInventory( PlayerInventory inventory )
	{
		Inventory = inventory;

		if ( Networking.IsHost )
			InventoryObject = inventory.IsValid() ? inventory.GameObject : null;
	}

	public void SetUnlocked( bool unlocked )
	{
		if ( !Networking.IsHost )
			return;

		IsUnlocked = unlocked;
	}

	public virtual void OnDeploy()
	{
	}

	public virtual void OnHolster()
	{
	}

	public virtual void OnPlayerUpdate()
	{
	}

	public virtual void PrimaryAttack()
	{
	}

	public virtual void SecondaryAttack()
	{
	}

	public virtual bool CanPrimaryAttack()
	{
		return IsActive;
	}

	public virtual bool CanSecondaryAttack()
	{
		return IsActive;
	}
}
