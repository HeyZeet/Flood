using Sandbox;
using System.Collections.Generic;
using System.Linq;

public sealed class PlayerInventory : Component
{
	[Property] public int MaxSlots { get; set; } = 4;

	[Sync] public BaseCarryable ActiveCarryable { get; private set; }

	public string ActiveCarryableName
	{
		get
		{
			if ( !ActiveCarryable.IsValid() )
				return "None";

			return ActiveCarryable.DisplayName;
		}
	}

	public T GetActiveCarryable<T>() where T : BaseCarryable
	{
		if ( !ActiveCarryable.IsValid() )
			return null;

		return ActiveCarryable.Components.Get<T>();
	}

	public IReadOnlyList<BaseCarryable> Carryables
	{
		get
		{
			return Components
				.GetAll<BaseCarryable>( FindMode.EverythingInChildren )
				.OrderBy( x => x.InventorySlot )
				.ToList();
		}
	}

	protected override void OnStart()
    {
	    if ( Networking.IsHost )
	    {
		    foreach ( var carryable in Carryables )
		    {
			    carryable.Inventory = this;
			    carryable.OnAddedToInventory( this );
			    carryable.GameObject.Enabled = false;
		    }

		    var firstCarryable = GetCarryableInSlot( 0 );

		    if ( firstCarryable.IsValid() )
		    {
			SetActiveCarryable( firstCarryable );
		    }

		    RefreshActiveState();
	    }
    }

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		OnControl();
	}

	public void OnControl()
	{
		if ( ActiveCarryable.IsValid() )
		{
			ActiveCarryable.OnPlayerUpdate();
		}

		if ( Input.Pressed( "Slot1" ) )
			SwitchToSlot( 0 );

		if ( Input.Pressed( "Slot2" ) )
			SwitchToSlot( 1 );

		if ( Input.Pressed( "Slot3" ) )
			SwitchToSlot( 2 );

		if ( Input.Pressed( "Slot4" ) )
			SwitchToSlot( 3 );

		if ( Input.MouseWheel.y > 0 )
			SwitchNext();

		if ( Input.MouseWheel.y < 0 )
			SwitchPrevious();
	}

	public bool AddCarryable( BaseCarryable carryable, int slot = -1, bool makeActive = true )
	{
		if ( !Networking.IsHost )
			return false;

		if ( !carryable.IsValid() )
			return false;

		if ( slot < 0 )
			slot = FindEmptySlot();

		if ( slot < 0 || slot >= MaxSlots )
			return false;

		if ( GetCarryableInSlot( slot ).IsValid() )
			return false;

		carryable.InventorySlot = slot;
		carryable.Inventory = this;

		carryable.GameObject.SetParent( GameObject );
		carryable.OnAddedToInventory( this );

		if ( makeActive )
			SetActiveCarryable( carryable );
		else
			carryable.GameObject.Enabled = false;

		return true;
	}

	public bool AddCarryableFromPrefab( GameObject prefab, int slot = -1, bool makeActive = true )
	{
		if ( !Networking.IsHost )
			return false;

		if ( !prefab.IsValid() )
			return false;

		var obj = prefab.Clone();
		obj.SetParent( GameObject );

		var carryable = obj.Components.Get<BaseCarryable>( FindMode.EverythingInSelfAndDescendants );

		if ( !carryable.IsValid() )
		{
			Log.Warning( $"Prefab {prefab.Name} does not have a BaseCarryable component." );
			obj.Destroy();
			return false;
		}

		return AddCarryable( carryable, slot, makeActive );
	}

	public BaseCarryable GetCarryableInSlot( int slot )
	{
		return Carryables.FirstOrDefault( x => x.InventorySlot == slot );
	}

	public int FindEmptySlot()
	{
		for ( int i = 0; i < MaxSlots; i++ )
		{
			if ( !GetCarryableInSlot( i ).IsValid() )
				return i;
		}

		return -1;
	}

	public void SwitchToSlot( int slot )
	{
		var carryable = GetCarryableInSlot( slot );

		if ( !carryable.IsValid() )
			return;

		RequestSetActive( carryable );
	}

	public void SwitchNext()
	{
		var carryables = Carryables
			.Where( x => x.IsValid() )
			.OrderBy( x => x.InventorySlot )
			.ToList();

		if ( carryables.Count == 0 )
			return;

		if ( !ActiveCarryable.IsValid() )
		{
			RequestSetActive( carryables[0] );
			return;
		}

		var index = carryables.IndexOf( ActiveCarryable );
		var nextIndex = (index + 1) % carryables.Count;

		RequestSetActive( carryables[nextIndex] );
	}

	public void SwitchPrevious()
	{
		var carryables = Carryables
			.Where( x => x.IsValid() )
			.OrderBy( x => x.InventorySlot )
			.ToList();

		if ( carryables.Count == 0 )
			return;

		if ( !ActiveCarryable.IsValid() )
		{
			RequestSetActive( carryables[0] );
			return;
		}

		var index = carryables.IndexOf( ActiveCarryable );
		var previousIndex = index - 1;

		if ( previousIndex < 0 )
			previousIndex = carryables.Count - 1;

		RequestSetActive( carryables[previousIndex] );
	}

	private void RequestSetActive( BaseCarryable carryable )
	{
		if ( Networking.IsHost )
		{
			SetActiveCarryable( carryable );
			return;
		}

		SetActiveCarryableRpc( carryable );
	}

	[Rpc.Host]
	private void SetActiveCarryableRpc( BaseCarryable carryable )
	{
		SetActiveCarryable( carryable );
	}

	private void SetActiveCarryable( BaseCarryable carryable )
	{
		if ( !Networking.IsHost )
			return;

		if ( !carryable.IsValid() )
			return;

		if ( ActiveCarryable == carryable )
			return;

		var oldCarryable = ActiveCarryable;

		ActiveCarryable = carryable;

		if ( oldCarryable.IsValid() )
			oldCarryable.OnHolster();

		carryable.OnDeploy();

		RefreshActiveState();
	}

	private void RefreshActiveState()
	{
		foreach ( var carryable in Carryables )
		{
			if ( !carryable.IsValid() )
				continue;

			carryable.GameObject.Enabled = carryable == ActiveCarryable;
		}
	}

	private void OnActiveCarryableChanged( BaseCarryable oldCarryable, BaseCarryable newCarryable )
	{
		if ( oldCarryable.IsValid() )
		{
			oldCarryable.GameObject.Enabled = false;
			oldCarryable.OnHolster();
		}

		if ( newCarryable.IsValid() )
		{
			newCarryable.GameObject.Enabled = true;
			newCarryable.OnDeploy();
		}
	}
}