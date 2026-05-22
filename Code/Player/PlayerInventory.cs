using Sandbox;
using System.Collections.Generic;
using System.Linq;

public sealed class PlayerInventory : Component
{
	[Property] public int MaxSlots { get; set; } = 4;
	private bool WasOwnerDead { get; set; }

	[Sync] public int ActiveSlot { get; private set; } = -1;

	public BaseCarryable ActiveCarryable
	{
		get
		{
			if ( ActiveSlot < 0 )
				return null;

			return GetCarryableInSlot( ActiveSlot );
		}
	}

	public string ActiveCarryableName
	{
		get
		{
			if ( !ActiveCarryable.IsValid() )
				return "None";

			return ActiveCarryable.DisplayName;
		}
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
		SetupStartingCarryables();

		if ( Networking.IsHost )
		{
			var firstCarryable = GetCarryableInSlot( 0 );

			if ( firstCarryable.IsValid() )
				SetActiveCarryable( firstCarryable );
		}

		RefreshActiveState();

		Log.Info( $"PlayerInventory started. Host={Networking.IsHost}, Proxy={IsProxy}, Carryables={Carryables.Count}, ActiveSlot={ActiveSlot}" );
	}

	protected override void OnUpdate()
	{
		RefreshActiveState();

		if ( IsProxy )
			return;

		OnControl();
	}

	public T GetActiveCarryable<T>() where T : BaseCarryable
	{
		if ( !ActiveCarryable.IsValid() )
			return null;

		return ActiveCarryable.Components.Get<T>();
	}

	public void OnControl()
	{
		var ownerDead = IsOwnerDead();

		if ( ownerDead )
		{
			if ( !WasOwnerDead )
				ActiveCarryable?.OnHolster();

			WasOwnerDead = true;
			return;
		}

		if ( WasOwnerDead )
		{
			WasOwnerDead = false;
			ActiveCarryable?.OnDeploy();
		}

		ActiveCarryable?.OnPlayerUpdate();

		HandleSlotInput();
		HandleMouseWheelInput();
	}

	private bool IsOwnerDead()
	{
		var player = Components.Get<FloodPlayer>();

		if ( !player.IsValid() )
			return false;

		return player.IsDead;
	}

	public bool AddCarryable( BaseCarryable carryable, int slot = -1, bool makeActive = true )
	{
		if ( !Networking.IsHost )
			return false;

		if ( !carryable.IsValid() )
			return false;

		if ( slot < 0 )
			slot = FindEmptySlot();

		if ( !IsValidSlot( slot ) )
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
		for ( var i = 0; i < MaxSlots; i++ )
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
		var carryables = GetSortedCarryables();

		if ( carryables.Count == 0 )
			return;

		if ( !ActiveCarryable.IsValid() )
		{
			RequestSetActive( carryables[0] );
			return;
		}

		var index = carryables.IndexOf( ActiveCarryable );

		if ( index < 0 )
		{
			RequestSetActive( carryables[0] );
			return;
		}

		var nextIndex = (index + 1) % carryables.Count;
		RequestSetActive( carryables[nextIndex] );
	}

	public void SwitchPrevious()
	{
		var carryables = GetSortedCarryables();

		if ( carryables.Count == 0 )
			return;

		if ( !ActiveCarryable.IsValid() )
		{
			RequestSetActive( carryables[0] );
			return;
		}

		var index = carryables.IndexOf( ActiveCarryable );

		if ( index < 0 )
		{
			RequestSetActive( carryables[0] );
			return;
		}

		var previousIndex = index - 1;

		if ( previousIndex < 0 )
			previousIndex = carryables.Count - 1;

		RequestSetActive( carryables[previousIndex] );
	}

	private void SetupStartingCarryables()
	{
		foreach ( var carryable in Carryables )
		{
			if ( !carryable.IsValid() )
				continue;

			carryable.Inventory = this;
			carryable.OnAddedToInventory( this );

			if ( Networking.IsHost )
				carryable.GameObject.Enabled = false;
		}
	}

	private void HandleSlotInput()
	{
		if ( Input.Pressed( "Slot1" ) ) SwitchToSlot( 0 );
		if ( Input.Pressed( "Slot2" ) ) SwitchToSlot( 1 );
		if ( Input.Pressed( "Slot3" ) ) SwitchToSlot( 2 );
		if ( Input.Pressed( "Slot4" ) ) SwitchToSlot( 3 );
	}

	private void HandleMouseWheelInput()
	{
		if ( Input.MouseWheel.y > 0 )
			SwitchNext();

		if ( Input.MouseWheel.y < 0 )
			SwitchPrevious();
	}

	private List<BaseCarryable> GetSortedCarryables()
	{
		return Carryables
			.Where( x => x.IsValid() )
			.OrderBy( x => x.InventorySlot )
			.ToList();
	}

	private bool IsValidSlot( int slot )
	{
		return slot >= 0 && slot < MaxSlots;
	}

	private void RequestSetActive( BaseCarryable carryable )
	{
		if ( Networking.IsHost )
		{
			SetActiveCarryable( carryable );
			return;
		}

		SetActiveCarryableRpc( carryable.InventorySlot );
	}

	[Rpc.Host]
	private void SetActiveCarryableRpc( int slot )
	{
		var carryable = GetCarryableInSlot( slot );

		if ( !IsCarryableOwnedByInventory( carryable ) )
			return;

		SetActiveCarryable( carryable );
	}

	private void SetActiveCarryable( BaseCarryable carryable )
	{
		if ( !Networking.IsHost )
			return;

		if ( !carryable.IsValid() )
			return;

		if ( !IsCarryableOwnedByInventory( carryable ) )
			return;

		if ( ActiveSlot == carryable.InventorySlot )
			return;

		var oldCarryable = ActiveCarryable;
		ActiveSlot = carryable.InventorySlot;

		if ( oldCarryable.IsValid() )
			oldCarryable.OnHolster();

		carryable.OnDeploy();

		RefreshActiveState();
	}

	private void RefreshActiveState()
	{
		if ( !Networking.IsHost )
			return;

		foreach ( var carryable in Carryables )
		{
			if ( !carryable.IsValid() )
				continue;

			carryable.GameObject.Enabled = carryable == ActiveCarryable;
		}
	}

	private bool IsCarryableOwnedByInventory( BaseCarryable carryable )
	{
		if ( !carryable.IsValid() )
			return false;

		if ( carryable.Inventory != this )
			return false;

		var current = carryable.GameObject;

		while ( current.IsValid() )
		{
			if ( current == GameObject )
				return true;

			current = current.Parent;
		}

		return false;
	}
}
