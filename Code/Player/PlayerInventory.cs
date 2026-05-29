using Sandbox;
using System.Collections.Generic;
using System.Linq;

public sealed class PlayerInventory : Component
{
	private sealed class PendingCarryableBind
	{
		public GameObject CarryableObject { get; init; }
		public int Slot { get; init; }
		public TimeUntil TimeUntilNextBroadcast { get; set; }
		public TimeUntil TimeUntilExpired { get; set; }
	}

	[Property, Sync( SyncFlags.FromHost )] public int MaxSlots { get; set; } = 4;
	[Property, Group( "Networking" )] public float CarryableBindRetrySeconds { get; set; } = 3f;
	[Property, Group( "Networking" )] public float CarryableBindRetryInterval { get; set; } = 0.2f;

	private bool WasOwnerDead { get; set; }
	private int LastPresentedActiveSlot { get; set; } = int.MinValue;
	private int LastKnownCarryableCount { get; set; } = -1;
	private BaseCarryable PresentedCarryable { get; set; }
	private readonly List<PendingCarryableBind> PendingCarryableBinds = new();

	[Sync( SyncFlags.FromHost )] public int ActiveSlot { get; private set; } = -1;

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
			return GetCarryablesSnapshot()
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

		UpdateActiveCarryablePresentation( true );

		Log.Info( $"PlayerInventory started. Host={Networking.IsHost}, Proxy={IsProxy}, Carryables={Carryables.Count}, ActiveSlot={ActiveSlot}" );
	}

	protected override void OnUpdate()
	{
		UpdatePendingCarryableBinds();
		RefreshCarryableBindings();
		UpdateActiveCarryablePresentation();

		if ( !IsLocallyControlled() )
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
				HolsterPresentedCarryable();

			WasOwnerDead = true;
			return;
		}

		if ( WasOwnerDead )
		{
			WasOwnerDead = false;
			UpdateActiveCarryablePresentation( true );
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

	private bool IsLocallyControlled()
	{
		var player = Components.Get<FloodPlayer>();

		if ( player.IsValid() )
			return player.IsLocalPlayer;

		return !IsProxy;
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
		carryable.SetUnlocked( true );

		carryable.GameObject.SetParent( GameObject );
		carryable.OnAddedToInventory( this );

		if ( makeActive )
			SetActiveCarryable( carryable );

		return true;
	}

	public bool AddCarryableFromPrefab( GameObject prefab, int slot = -1, bool makeActive = true )
	{
		if ( !Networking.IsHost )
			return false;

		if ( !prefab.IsValid() )
			return false;

		if ( TryUnlockExistingCarryableFromPrefab( prefab, slot, makeActive ) )
			return true;

		var obj = prefab.Clone();
		obj.SetParent( GameObject );

		var carryable = obj.Components.Get<BaseCarryable>( FindMode.EverythingInSelfAndDescendants );

		if ( !carryable.IsValid() )
		{
			Log.Warning( $"Prefab {prefab.Name} does not have a BaseCarryable component." );
			obj.Destroy();
			return false;
		}

		if ( !AddCarryable( carryable, slot, makeActive ) )
		{
			obj.Destroy();
			return false;
		}

		if ( !obj.Network.Active )
		{
			var owner = GameObject.Network.Owner;
			var spawned = owner is not null
				? obj.NetworkSpawn( owner )
				: obj.NetworkSpawn();

			if ( !spawned )
				Log.Warning( $"Failed to network spawn carryable {carryable.DisplayName}." );
		}

		obj.SetParent( GameObject );
		Log.Info( $"Added carryable {carryable.DisplayName} to inventory slot {carryable.InventorySlot}. ActiveSlot={ActiveSlot}, Networked={obj.Network.Active}." );
		QueueCarryableBindBroadcast( obj, carryable.InventorySlot );

		return true;
	}

	[Rpc.Broadcast]
	private void BindNetworkCarryableBroadcast( GameObject carryableObject, int slot, GameObject inventoryObject )
	{
		if ( inventoryObject != GameObject )
			return;

		if ( !carryableObject.IsValid() )
		{
			Log.Warning( $"Inventory bind received for slot {slot}, but the carryable object is not valid on this client yet." );
			return;
		}

		var carryable = carryableObject.Components.Get<BaseCarryable>( FindMode.EverythingInSelfAndDescendants );

		if ( !carryable.IsValid() )
		{
			Log.Warning( $"Inventory bind received {carryableObject.Name}, but it has no BaseCarryable." );
			return;
		}

		BindCarryableToInventory( carryable );
		LastPresentedActiveSlot = int.MinValue;

		if ( carryable.InventorySlot != ActiveSlot )
			carryable.OnHolster();

		Log.Info( $"Bound network carryable {carryable.DisplayName} to inventory slot {carryable.InventorySlot}. ActiveSlot={ActiveSlot}, Local={IsLocallyControlled()}." );
	}

	private void QueueCarryableBindBroadcast( GameObject carryableObject, int slot )
	{
		if ( !Networking.IsHost )
			return;

		if ( !carryableObject.IsValid() )
			return;

		PendingCarryableBinds.Add( new PendingCarryableBind
		{
			CarryableObject = carryableObject,
			Slot = slot,
			TimeUntilNextBroadcast = 0f,
			TimeUntilExpired = CarryableBindRetrySeconds.Clamp( 0.2f, 10f )
		} );
	}

	private void UpdatePendingCarryableBinds()
	{
		if ( !Networking.IsHost )
			return;

		foreach ( var pendingBind in PendingCarryableBinds.ToArray() )
		{
			if ( !pendingBind.CarryableObject.IsValid() || pendingBind.TimeUntilExpired )
			{
				PendingCarryableBinds.Remove( pendingBind );
				continue;
			}

			if ( !pendingBind.TimeUntilNextBroadcast )
				continue;

			pendingBind.TimeUntilNextBroadcast = CarryableBindRetryInterval.Clamp( 0.05f, 1f );
			BindNetworkCarryableBroadcast( pendingBind.CarryableObject, pendingBind.Slot, GameObject );
		}
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
		foreach ( var carryable in GetCarryablesSnapshot( true ) )
		{
			if ( !carryable.IsValid() )
				continue;

			if ( !carryable.IsUnlocked )
			{
				carryable.OnHolster();
				continue;
			}

			BindCarryableToInventory( carryable );
			carryable.OnHolster();
		}

		LastKnownCarryableCount = GetCarryablesSnapshot().Count;
		LastPresentedActiveSlot = int.MinValue;
	}

	private void HandleSlotInput()
	{
		var maxCarryableSlot = Carryables.Count > 0 ? Carryables.Max( carryable => carryable.InventorySlot ) + 1 : 0;
		var maxNumberKeySlots = System.Math.Min( System.Math.Max( MaxSlots, maxCarryableSlot ), 9 );

		for ( var slot = 0; slot < maxNumberKeySlots; slot++ )
		{
			if ( Input.Pressed( $"Slot{slot + 1}" ) )
				SwitchToSlot( slot );
		}
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
		return GetCarryablesSnapshot()
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

		ActiveSlot = carryable.InventorySlot;

		UpdateActiveCarryablePresentation( true );
	}

	private void UpdateActiveCarryablePresentation( bool force = false )
	{
		if ( IsOwnerDead() )
		{
			HolsterPresentedCarryable();
			return;
		}

		var nextCarryable = ActiveCarryable;

		if ( !force && LastPresentedActiveSlot == ActiveSlot && PresentedCarryable == nextCarryable )
			return;

		if ( PresentedCarryable.IsValid() && PresentedCarryable != nextCarryable )
			PresentedCarryable.OnHolster();

		foreach ( var carryable in Carryables )
		{
			if ( !carryable.IsValid() )
				continue;

			if ( carryable == nextCarryable )
				continue;

			if ( carryable != PresentedCarryable )
				carryable.OnHolster();
		}

		PresentedCarryable = nextCarryable;
		LastPresentedActiveSlot = ActiveSlot;

		if ( PresentedCarryable.IsValid() )
			PresentedCarryable.OnDeploy();
	}

	private void HolsterPresentedCarryable()
	{
		if ( PresentedCarryable.IsValid() )
			PresentedCarryable.OnHolster();

		PresentedCarryable = null;
		LastPresentedActiveSlot = int.MinValue;
	}

	private bool IsCarryableOwnedByInventory( BaseCarryable carryable )
	{
		if ( !carryable.IsValid() )
			return false;

		if ( carryable.Inventory != this )
			return false;

		if ( carryable.InventoryObject == GameObject )
			return true;

		var current = carryable.GameObject;

		while ( current.IsValid() )
		{
			if ( current == GameObject )
				return true;

			current = current.Parent;
		}

		return false;
	}

	private List<BaseCarryable> GetCarryablesSnapshot( bool includeLocked = false )
	{
		var carryables = Components
			.GetAll<BaseCarryable>( FindMode.EverythingInChildren )
			.Where( carryable => carryable.IsValid() )
			.Where( carryable => includeLocked || carryable.IsUnlocked )
			.ToList();

		if ( Scene.IsValid() )
		{
			foreach ( var gameObject in Scene.GetAllObjects( true ) )
			{
				if ( !gameObject.IsValid() )
					continue;

				var carryable = gameObject.Components.Get<BaseCarryable>();

				if ( !carryable.IsValid() )
					continue;

				if ( !includeLocked && !carryable.IsUnlocked )
					continue;

				if ( carryable.InventoryObject != GameObject )
					continue;

				if ( carryables.Contains( carryable ) )
					continue;

				carryables.Add( carryable );
			}
		}

		return carryables;
	}

	private void RefreshCarryableBindings()
	{
		var carryables = GetCarryablesSnapshot( true );
		var shouldForcePresentation = carryables.Count != LastKnownCarryableCount;

		LastKnownCarryableCount = carryables.Count;

		foreach ( var carryable in carryables )
		{
			if ( !carryable.IsValid() )
				continue;

			if ( !carryable.IsUnlocked )
			{
				if ( carryable.Inventory == this )
					carryable.OnHolster();

				continue;
			}

			if ( carryable.Inventory == this )
				continue;

			BindCarryableToInventory( carryable );
			shouldForcePresentation = true;

			if ( carryable.InventorySlot != ActiveSlot )
				carryable.OnHolster();
		}

		if ( shouldForcePresentation )
			LastPresentedActiveSlot = int.MinValue;
	}

	private void BindCarryableToInventory( BaseCarryable carryable )
	{
		if ( !carryable.IsValid() )
			return;

		carryable.Inventory = this;
		carryable.OnAddedToInventory( this );
	}

	private bool TryUnlockExistingCarryableFromPrefab( GameObject prefab, int slot, bool makeActive )
	{
		var prefabCarryable = prefab.Components.Get<BaseCarryable>( FindMode.EverythingInSelfAndDescendants );

		if ( !prefabCarryable.IsValid() )
			return false;

		var carryable = GetCarryablesSnapshot( true )
			.FirstOrDefault( candidate => candidate.IsValid() && candidate.GetType() == prefabCarryable.GetType() );

		if ( !carryable.IsValid() )
			return false;

		if ( carryable.IsUnlocked )
			return false;

		if ( slot < 0 )
			slot = FindEmptySlot();

		if ( slot >= MaxSlots )
			MaxSlots = slot + 1;

		if ( !IsValidSlot( slot ) )
			return false;

		carryable.InventorySlot = slot;
		carryable.SetUnlocked( true );
		BindCarryableToInventory( carryable );

		if ( makeActive )
			SetActiveCarryable( carryable );

		LastPresentedActiveSlot = int.MinValue;
		Log.Info( $"Unlocked existing carryable {carryable.DisplayName} in inventory slot {carryable.InventorySlot}. ActiveSlot={ActiveSlot}." );

		return true;
	}
}
