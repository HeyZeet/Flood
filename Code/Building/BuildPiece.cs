using Sandbox;
using System.Collections.Generic;

public enum BuildPieceMaterial
{
	Wood,
	Metal,
	Plastic,
	Foam
}

public sealed class BuildPiece : Component
{
	private static readonly List<BuildPiece> AllPieces = new();

	public static IReadOnlyList<BuildPiece> All => AllPieces;

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
		if ( !AllPieces.Contains( this ) )
			AllPieces.Add( this );

		CacheComponents();
	}

	protected override void OnDestroy()
	{
		AllPieces.Remove( this );
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

		ApplyHealthData( data );
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

		CacheComponents();
		ValidateRequiredComponents();
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

	private void CacheComponents()
	{
		Health = Components.Get<BoatPieceHealth>( FindMode.EverythingInSelfAndDescendants );
		Rigidbody = Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );

		if ( !Health.IsValid() )
			Health = GameObject.Parent?.Components.Get<BoatPieceHealth>( FindMode.EverythingInSelfAndDescendants );

		if ( !Rigidbody.IsValid() )
			Rigidbody = GameObject.Parent?.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );
	}

	private void ValidateRequiredComponents()
	{
		if ( !Health.IsValid() )
			Log.Warning( $"{GameObject.Name} has BuildPiece but no BoatPieceHealth." );

		if ( !Rigidbody.IsValid() )
			Log.Warning( $"{GameObject.Name} has BuildPiece but no Rigidbody." );
	}

	private void ApplyHealthData( BuildPieceData data )
	{
		CacheComponents();

		if ( !Health.IsValid() )
			return;

		Health.MaxHealth = data.MaxHealth;
		Health.ResetHealth();
	}
}
