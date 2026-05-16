using Sandbox;

public enum BuildPieceMaterial
{
	Wood,
	Metal,
	Plastic,
	Foam
}

public sealed class BuildPiece : Component
{
	[Property] public string DisplayName { get; set; } = "Build Piece";

	[Property] public BuildPieceMaterial Material { get; set; } = BuildPieceMaterial.Wood;

	[Property] public int Cost { get; set; } = 10;

	[Property] public float Weight { get; set; } = 10f;

	[Property] public bool CountsAsBoatPart { get; set; } = true;

	[Property] public bool CanBeWelded { get; set; } = true;

	[Sync] public GameObject Owner { get; private set; }

	[Sync] public bool IsPlaced { get; private set; }

	public BoatPieceHealth Health { get; private set; }
	public Rigidbody Rigidbody { get; private set; }

	protected override void OnStart()
	{
		Health = Components.Get<BoatPieceHealth>();
		Rigidbody = Components.Get<Rigidbody>();

		if ( !Health.IsValid() )
			Log.Warning( $"{GameObject.Name} has BuildPiece but no BoatPieceHealth." );

		if ( !Rigidbody.IsValid() )
			Log.Warning( $"{GameObject.Name} has BuildPiece but no Rigidbody." );
	}

    public void ApplyData( BuildPieceData data )
    {
	    if ( data is null )
		    return;

	    DisplayName = data.DisplayName;
	    Material = data.Material;
	    Cost = data.Cost;
	    Weight = data.Weight;
	    CanBeWelded = data.CanBeWelded;
	    CountsAsBoatPart = data.CountsAsBoatPart;

	    var health = Components.Get<BoatPieceHealth>();

	    if ( health.IsValid() )
	    {
		    health.MaxHealth = data.MaxHealth;
	    }
    }

	public void SetOwner( GameObject owner )
	{
		if ( !Networking.IsHost )
			return;

		Owner = owner;
	}

	public void MarkPlaced()
	{
		if ( !Networking.IsHost )
			return;

		IsPlaced = true;
	}

	public void MarkUnplaced()
	{
		if ( !Networking.IsHost )
			return;

		IsPlaced = false;
	}

	public bool CanPlayerModify( GameObject player )
	{
		if ( !player.IsValid() )
			return false;

		if ( !Owner.IsValid() )
			return true;

		return Owner == player;
	}
}
