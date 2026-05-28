using Sandbox;

public enum ShopItemType
{
	Weapon,
	Perk,
	GameModifier
}

[AssetType( Name = "Shop Item Data", Extension = "shopitem", Category = "Flood 2.0" )]
public sealed class ShopItemData : GameResource
{
	[Property, Group( "Info" )]
	public string DisplayName { get; set; } = "Shop Item";

	[Property, Group( "Info" )]
	public string Description { get; set; } = "";

	[Property, Group( "Info" )]
	public ShopItemType ItemType { get; set; } = ShopItemType.Weapon;

	[Property, Group( "Info" )]
	public int SortOrder { get; set; }

	[Property, Group( "Economy" )]
	public int Cost { get; set; } = 100;

	[Property, Group( "Rules" )]
	public bool UniquePurchase { get; set; } = true;

	[Property, Group( "Weapon Grant" )]
	public GameObject WeaponPrefab { get; set; }

	[Property, Group( "Weapon Grant" )]
	public int PreferredInventorySlot { get; set; } = -1;

	[Property, Group( "Weapon Grant" )]
	public bool EquipOnPurchase { get; set; } = true;

	[Property, Group( "Presentation" )]
	public Model PreviewModel { get; set; }
}
