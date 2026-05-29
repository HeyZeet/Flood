public sealed class FloodScoreboardRow
{
	public string Name { get; init; }
	public string Status { get; init; }
	public int Kills { get; init; }
	public int Deaths { get; init; }
	public string Resources { get; init; }
	public string CssClass { get; init; }

	public string StateKey => $"{Name}|{Status}|{Kills}|{Deaths}|{Resources}|{CssClass}";
}

public sealed class FloodBuildMenuGroup
{
	public BuildPieceMaterial Material { get; init; }
	public string Name { get; init; }
	public string TabCssClass { get; init; }

	public string StateKey => $"{Material}|{Name}|{TabCssClass}";
}

public sealed class FloodBuildMenuEntry
{
	public int Index { get; init; }
	public string Name { get; init; }
	public string ModelPath { get; init; }
	public int Cost { get; init; }
	public int Health { get; init; }
	public string CssClass { get; init; }

	public string StateKey => $"{Index}|{Name}|{ModelPath}|{Cost}|{Health}|{CssClass}";
}

public sealed class FloodShopTab
{
	public ShopItemType ItemType { get; init; }
	public string Name { get; init; }
	public string CssClass { get; init; }

	public string StateKey => $"{ItemType}|{Name}|{CssClass}";
}

public sealed class FloodShopEntry
{
	public int Index { get; init; }
	public string Name { get; init; }
	public string Description { get; init; }
	public string ModelPath { get; init; }
	public int Cost { get; init; }
	public string Status { get; init; }
	public string ButtonText { get; init; }
	public string CssClass { get; init; }

	public string StateKey => $"{Index}|{Name}|{Description}|{ModelPath}|{Cost}|{Status}|{ButtonText}|{CssClass}";
}

public sealed class FloodCarryableSelectorItem
{
	public string SlotText { get; init; }
	public string Name { get; init; }
	public string CssClass { get; init; }

	public string StateKey => $"{SlotText}|{Name}|{CssClass}";
}
