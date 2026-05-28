using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class PlayerShopController : Component
{
	public static PlayerShopController Local { get; private set; }

	[Property] public string ShopInputAction { get; set; } = "menu";
	[Property] public bool AutoLoadResourceItems { get; set; } = true;
	[Property] public List<ShopItemData> AvailableItems { get; set; } = new();

	[Property, Group( "Round Rules" )] public bool AllowDuringWaiting { get; set; } = false;
	[Property, Group( "Round Rules" )] public bool AllowDuringBuildPhase { get; set; } = true;
	[Property, Group( "Round Rules" )] public bool AllowDuringFloodPhase { get; set; } = false;
	[Property, Group( "Round Rules" )] public bool AllowDuringCombatPhase { get; set; } = false;
	[Property, Group( "Round Rules" )] public bool AllowDuringRoundEnd { get; set; } = true;

	[Sync( SyncFlags.FromHost )] public string PurchasedItemKeys { get; private set; } = "";

	public bool IsShopOpen { get; private set; }
	public IReadOnlyList<ShopItemData> Items => GetCatalog();

	private List<ShopItemData> CachedCatalog { get; set; } = new();
	private string CachedCatalogKey { get; set; } = "";

	protected override void OnStart()
	{
		RefreshCatalog();
	}

	protected override void OnDestroy()
	{
		if ( Local == this )
			Local = null;
	}

	protected override void OnUpdate()
	{
		if ( !IsLocallyControlled() )
			return;

		Local = this;

		if ( !CanOpenShopNow() )
		{
			CloseShop();
			return;
		}

		if ( ShouldIgnoreShopInput() )
			return;

		if ( WasShopInputPressed() )
			ToggleShop();
	}

	public void ToggleShop()
	{
		if ( !CanOpenShopNow() )
		{
			CloseShop();
			return;
		}

		IsShopOpen = !IsShopOpen;
		Log.Info( $"Shop {(IsShopOpen ? "opened" : "closed")}." );
	}

	public void CloseShop()
	{
		IsShopOpen = false;
	}

	public void RequestPurchase( int itemIndex )
	{
		if ( Networking.IsHost )
		{
			TryPurchaseHost( itemIndex );
			return;
		}

		RequestPurchaseRpc( itemIndex );
	}

	[Rpc.Host]
	private void RequestPurchaseRpc( int itemIndex )
	{
		TryPurchaseHost( itemIndex );
	}

	public bool CanAfford( ShopItemData item )
	{
		var resources = Components.Get<PlayerBuildResources>( FindMode.EverythingInSelfAndAncestors );
		return item is not null && resources.IsValid() && resources.CanAfford( item.Cost );
	}

	public bool IsPurchasedOrOwned( ShopItemData item )
	{
		if ( item is null || !item.UniquePurchase )
			return false;

		if ( GetPurchasedKeySet().Contains( GetItemKey( item ) ) )
			return true;

		if ( item.ItemType != ShopItemType.Weapon || !item.WeaponPrefab.IsValid() )
			return false;

		var prefabCarryable = item.WeaponPrefab.Components.Get<BaseCarryable>( FindMode.EverythingInSelfAndDescendants );

		if ( !prefabCarryable.IsValid() )
			return false;

		var inventory = Components.Get<PlayerInventory>( FindMode.EverythingInSelfAndAncestors );

		if ( !inventory.IsValid() )
			return false;

		return inventory.Carryables.Any( carryable => carryable.IsValid() && carryable.GetType() == prefabCarryable.GetType() );
	}

	private bool TryPurchaseHost( int itemIndex )
	{
		if ( !Networking.IsHost )
			return false;

		if ( !CanOpenShopNow() )
			return false;

		var item = GetItemAtIndex( itemIndex );

		if ( item is null )
			return false;

		if ( IsPurchasedOrOwned( item ) )
		{
			Log.Info( $"Cannot buy {item.DisplayName}: already owned." );
			return false;
		}

		var resources = Components.Get<PlayerBuildResources>( FindMode.EverythingInSelfAndAncestors );

		if ( !resources.IsValid() )
		{
			Log.Warning( "Cannot buy shop item: missing PlayerBuildResources." );
			return false;
		}

		if ( !resources.CanAfford( item.Cost ) )
		{
			Log.Info( $"Cannot buy {item.DisplayName}: costs {item.Cost}, player has {resources.Resources}." );
			return false;
		}

		if ( !TryGrantItemHost( item ) )
			return false;

		if ( !resources.TrySpend( item.Cost ) )
			return false;

		RecordPurchasedItem( item );
		Log.Info( $"Purchased {item.DisplayName} for {item.Cost}." );
		return true;
	}

	private bool TryGrantItemHost( ShopItemData item )
	{
		return item.ItemType switch
		{
			ShopItemType.Weapon => TryGrantWeaponHost( item ),
			ShopItemType.Perk => TryGrantPerkHost( item ),
			ShopItemType.GameModifier => TryGrantGameModifierHost( item ),
			_ => false
		};
	}

	private bool TryGrantWeaponHost( ShopItemData item )
	{
		if ( !item.WeaponPrefab.IsValid() )
		{
			Log.Warning( $"Cannot buy {item.DisplayName}: no weapon prefab assigned." );
			return false;
		}

		var inventory = Components.Get<PlayerInventory>( FindMode.EverythingInSelfAndAncestors );

		if ( !inventory.IsValid() )
		{
			Log.Warning( $"Cannot buy {item.DisplayName}: missing PlayerInventory." );
			return false;
		}

		var slot = item.PreferredInventorySlot;

		if ( slot >= inventory.MaxSlots )
			inventory.MaxSlots = slot + 1;

		if ( slot < 0 && inventory.FindEmptySlot() < 0 )
			inventory.MaxSlots++;

		return inventory.AddCarryableFromPrefab( item.WeaponPrefab, slot, item.EquipOnPurchase );
	}

	private bool TryGrantPerkHost( ShopItemData item )
	{
		Log.Warning( $"{item.DisplayName} is a perk shop item, but perk grants are not implemented yet." );
		return false;
	}

	private bool TryGrantGameModifierHost( ShopItemData item )
	{
		Log.Warning( $"{item.DisplayName} is a modifier shop item, but modifier grants are not implemented yet." );
		return false;
	}

	private ShopItemData GetItemAtIndex( int itemIndex )
	{
		var catalog = GetCatalog();

		if ( itemIndex < 0 || itemIndex >= catalog.Count )
			return null;

		return catalog[itemIndex];
	}

	private IReadOnlyList<ShopItemData> GetCatalog()
	{
		RefreshCatalog();
		return CachedCatalog;
	}

	private void RefreshCatalog()
	{
		var items = new List<ShopItemData>();

		if ( AvailableItems is not null )
			items.AddRange( AvailableItems.Where( item => item is not null ) );

		if ( AutoLoadResourceItems )
			items.AddRange( ResourceLibrary.GetAll<ShopItemData>().Where( item => item is not null ) );

		var distinctItems = items
			.GroupBy( GetItemKey )
			.Select( group => group.First() )
			.OrderBy( item => item.SortOrder )
			.ThenBy( item => item.ItemType )
			.ThenBy( item => item.DisplayName )
			.ToList();

		var key = string.Join( "|", distinctItems.Select( GetItemKey ) );

		if ( key == CachedCatalogKey )
			return;

		CachedCatalogKey = key;
		CachedCatalog = distinctItems;
	}

	private bool CanOpenShopNow()
	{
		var player = Components.Get<FloodPlayer>( FindMode.EverythingInSelfAndAncestors );

		if ( player.IsValid() && player.IsDead )
			return false;

		var roundManager = FloodGameManager.Instance;

		if ( !roundManager.IsValid() )
			return true;

		return roundManager.CurrentPhase switch
		{
			GamePhase.WaitingForPlayers => AllowDuringWaiting,
			GamePhase.BuildPhase => AllowDuringBuildPhase,
			GamePhase.FloodPhase => AllowDuringFloodPhase,
			GamePhase.CombatPhase => AllowDuringCombatPhase,
			GamePhase.RoundEnd => AllowDuringRoundEnd,
			_ => false
		};
	}

	private bool ShouldIgnoreShopInput()
	{
		var inventory = Components.Get<PlayerInventory>( FindMode.EverythingInSelfAndAncestors );
		var activeCarryable = inventory.IsValid() ? inventory.ActiveCarryable : null;

		return activeCarryable is BoatBuilder;
	}

	private bool WasShopInputPressed()
	{
		var inputAction = string.IsNullOrWhiteSpace( ShopInputAction ) ? "menu" : ShopInputAction.Trim();

		if ( Input.Pressed( inputAction ) )
			return true;

		return inputAction != "menu" && Input.Pressed( "menu" );
	}

	private bool IsLocallyControlled()
	{
		var player = Components.Get<FloodPlayer>( FindMode.EverythingInSelfAndAncestors );

		if ( player.IsValid() )
			return player.IsLocalPlayer;

		return !IsProxy;
	}

	private void RecordPurchasedItem( ShopItemData item )
	{
		var keys = GetPurchasedKeySet();
		keys.Add( GetItemKey( item ) );
		PurchasedItemKeys = string.Join( ";", keys.OrderBy( key => key ) );
	}

	private HashSet<string> GetPurchasedKeySet()
	{
		return (PurchasedItemKeys ?? "")
			.Split( ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries )
			.ToHashSet();
	}

	private static string GetItemKey( ShopItemData item )
	{
		if ( item is null )
			return "";

		if ( !string.IsNullOrWhiteSpace( item.ResourcePath ) )
			return item.ResourcePath;

		return $"{item.ItemType}:{item.DisplayName}";
	}
}
