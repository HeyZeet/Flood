using Sandbox;

[AssetType( Name = "Build Piece Data", Extension = "bpiece", Category = "Flood 2.0" )]
public sealed class BuildPieceData : GameResource
{
	[Property, Group( "Info" )]
	public string DisplayName { get; set; } = "Build Piece";

	[Property, Group( "Info" )]
	public string Description { get; set; } = "";

	[Property, Group( "Setup" )]
	public GameObject Prefab { get; set; }

	[Property, Group( "Stats" )]
	public BuildPieceMaterial Material { get; set; } = BuildPieceMaterial.Wood;

	[Property, Group( "Stats" )]
	public int Cost { get; set; } = 10;

	[Property, Group( "Stats" )]
	public float Weight { get; set; } = 10f;

	[Property, Group( "Stats" )]
	public float MaxHealth { get; set; } = 100f;

	[Property, Group( "Rules" )]
	public bool CanBeWelded { get; set; } = true;

	[Property, Group( "Rules" )]
	public bool CountsAsBoatPart { get; set; } = true;

	[Property, Group( "Placement" )]
	public float PlacementSurfaceOffset { get; set; } = 32f;

	[Property, Group( "Placement" )]
	public Vector3 PlacementBounds { get; set; } = new Vector3( 50f, 50f, 50f );

	[Property, Group( "Placement" )]
	public float OverlapPadding { get; set; } = 2f;
}