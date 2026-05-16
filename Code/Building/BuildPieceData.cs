using Sandbox;

[AssetType( Name = "Build Piece Data", Extension = "bpiece", Category = "Flood 2.0" )]
public sealed class BuildPieceData : GameResource
{
	[Property] public string DisplayName { get; set; } = "Build Piece";

	[Property] public GameObject Prefab { get; set; }

	[Property] public BuildPieceMaterial Material { get; set; } = BuildPieceMaterial.Wood;

	[Property] public int Cost { get; set; } = 10;

	[Property] public float Weight { get; set; } = 10f;

	[Property] public float MaxHealth { get; set; } = 100f;

	[Property] public bool CanBeWelded { get; set; } = true;

	[Property] public bool CountsAsBoatPart { get; set; } = true;

	[Property] public string Description { get; set; } = "";

	[Property] public float PlacementSurfaceOffset { get; set; } = 32f;

	[Property] public Vector3 PlacementBounds { get; set; } = new Vector3( 50f, 50f, 50f );

	[Property] public float OverlapPadding { get; set; } = 2f;

}