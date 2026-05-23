using Sandbox;

public abstract class BaseCarryable : Component
{
	[Property, Sync( SyncFlags.FromHost )] public int InventorySlot { get; set; } = -1;

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
	}

	public virtual void OnDeploy()
	{
		GameObject.Enabled = true;
	}

	public virtual void OnHolster()
	{
		GameObject.Enabled = false;
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
