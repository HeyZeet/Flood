using Sandbox;

public sealed class PlayerBuildResources : Component
{
	[Property] public int StartingResources { get; set; } = 500;

	[Sync( SyncFlags.FromHost )] public int Resources { get; private set; }
	[Sync( SyncFlags.FromHost )] public int AwardedResources { get; private set; }

	protected override void OnStart()
	{
		if ( Networking.IsHost )
			ResetResources();
	}

	public bool CanAfford( int amount )
	{
		if ( amount <= 0 )
			return true;

		return Resources >= amount;
	}

	public bool TrySpend( int amount )
	{
		if ( !Networking.IsHost )
			return false;

		if ( amount <= 0 )
			return true;

		if ( !CanAfford( amount ) )
			return false;

		Resources -= amount;
		return true;
	}

	public void AddResources( int amount )
	{
		if ( !Networking.IsHost )
			return;

		if ( amount <= 0 )
			return;

		Resources += amount;
	}

	public void AddRoundAward( int amount )
	{
		if ( !Networking.IsHost )
			return;

		if ( amount <= 0 )
			return;

		AwardedResources += amount;
		Resources += amount;
	}

	public void ResetResources()
	{
		if ( !Networking.IsHost )
			return;

		Resources = StartingResources + AwardedResources;
	}
}
