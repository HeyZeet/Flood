using Sandbox;

public sealed class PlayerBuildResources : Component
{
	[Property] public int StartingResources { get; set; } = 500;

	[Sync] public int Resources { get; private set; }

	protected override void OnStart()
	{
		if ( Networking.IsHost )
		{
			Resources = StartingResources;
		}
	}

	public bool CanAfford( int amount )
	{
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
}