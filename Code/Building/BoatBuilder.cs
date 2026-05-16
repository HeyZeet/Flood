using Sandbox;
using System.Collections.Generic;

public sealed class BoatBuilder : BaseCarryable
{
	private BuildPlacementResult CurrentPlacement;
	private BuildPreview Preview => Components.Get<BuildPreview>();
	private BuildPlacement Placement => Components.Get<BuildPlacement>();
	private BuildPieceFactory Factory => Components.Get<BuildPieceFactory>();

	public BuildPlacementResult PlacementResult
	{
		get
		{
			return CurrentPlacement;
		}
	}

	private PlayerBuildResources BuildResources
	{
		get
		{
			if ( !Inventory.IsValid() )
				return null;

			return Inventory.Components.Get<PlayerBuildResources>();
		}
	}

	public override string DisplayName => "Boat Builder";

	[Property] public List<BuildPieceData> AvailablePieces { get; set; } = new();

	[Property] public int SelectedPieceIndex { get; set; } = 0;

	public BuildPieceData SelectedPiece
	{
		get
		{
			if ( AvailablePieces is null || AvailablePieces.Count == 0 )
				return null;

			SelectedPieceIndex = SelectedPieceIndex.Clamp( 0, AvailablePieces.Count - 1 );

			return AvailablePieces[SelectedPieceIndex];
		}
	}

	private FloodPlayerCamera PlayerCamera
	{
		get
		{
			if ( !Inventory.IsValid() )
				return null;

			return Inventory.Components.Get<FloodPlayerCamera>();
		}
	}

	private bool IsBuildingAllowed()
	{
		var roundManager = FloodRoundManager.Instance;

		if ( !roundManager.IsValid() )
			return true;

		return roundManager.IsBuildPhase();
	}

	private bool CanAffordSelectedPiece()
	{
		var selectedPiece = SelectedPiece;

		if ( selectedPiece is null )
			return false;

		var resources = BuildResources;

		if ( !resources.IsValid() )
			return false;

		return resources.CanAfford( selectedPiece.Cost );
	}

	private void SelectNextPiece()
	{
		if ( AvailablePieces is null || AvailablePieces.Count == 0 )
			return;

		SelectedPieceIndex++;

		if ( SelectedPieceIndex >= AvailablePieces.Count )
			SelectedPieceIndex = 0;

		OnSelectedPieceChanged();
	}

	private void SelectPreviousPiece()
	{
		if ( AvailablePieces is null || AvailablePieces.Count == 0 )
			return;

		SelectedPieceIndex--;

		if ( SelectedPieceIndex < 0 )
			SelectedPieceIndex = AvailablePieces.Count - 1;

		OnSelectedPieceChanged();
	}

	private void OnSelectedPieceChanged()
	{
		var selected = SelectedPiece;

		if ( selected is null )
		{
			Log.Warning( "No build piece selected." );
			Preview?.ClearPreview();
			return;
		}

		Log.Info( $"Selected build piece: {selected.DisplayName}" );

		Preview?.ClearPreview();
	}

	public override void OnDeploy()
	{
		base.OnDeploy();

		Log.Info( "Boat Builder equipped." );
	}

	public override void OnHolster()
	{
		Preview?.ClearPreview();

		base.OnHolster();

		Log.Info( "Boat Builder holstered." );
	}

	public override void OnPlayerUpdate()
	{
		UpdatePreview();

		if ( Input.Pressed( "attack1" ) )
			TryPlacePiece();

		if ( Input.Pressed( "attack2" ) )
			SelectNextPiece();

		if ( Input.Pressed( "reload" ) )
			Placement?.RotatePlacement();
	}

	private void UpdatePreview()
	{
		if ( !CanUseSelectedPiece() )
		{
			Preview?.ClearPreview();
			return;
		}

		var preview = Preview;
		var placement = Placement;

		if ( !preview.IsValid() || !placement.IsValid() )
			return;

		CurrentPlacement = GetPlacementResult();

		if ( CurrentPlacement.IsValid && !IsBuildingAllowed() )
		{
			CurrentPlacement = BuildPlacementResult.Invalid(
				CurrentPlacement.Position,
				CurrentPlacement.Rotation,
				"Building disabled."
			);
		}
		else if ( CurrentPlacement.IsValid && !CanAffordSelectedPiece() )
		{
			CurrentPlacement = BuildPlacementResult.Invalid(
				CurrentPlacement.Position,
				CurrentPlacement.Rotation,
				"Cannot afford."
			);
		}

		placement.DrawDebug( CurrentPlacement, PlayerCamera );
		preview.UpdatePreview( SelectedPiece, CurrentPlacement );
	}

	private void TryPlacePiece()
	{
		if ( !CanUseSelectedPiece() )
		return;

		if ( !IsBuildingAllowed() )
	{
		Log.Info( "Cannot place piece: building is disabled." );
		return;
	}

	var placementResult = GetPlacementResult();

	if ( !placementResult.IsValid )
	{
		Log.Info( $"Cannot place piece: {placementResult.Reason}" );
		return;
	}

	if ( !CanAffordSelectedPiece() )
	{
		Log.Info( "Cannot place piece: cannot afford." );
		return;
	}

		var selectedPiece = SelectedPiece;

		if ( selectedPiece is null )
			return;

		if ( !Networking.IsHost )
		{
			RequestPlacePiece( placementResult.Position, placementResult.Rotation );
			return;
		}

		TryPlacePieceHost( placementResult.Position, placementResult.Rotation );
	}

	private void TryPlacePieceHost( Vector3 position, Rotation rotation )
	{
		if ( !CanUseSelectedPiece() )
			return;

			if ( !IsBuildingAllowed() )
		{
			Log.Info( "Cannot place piece: building is disabled." );
			return;
		}

		var selectedPiece = SelectedPiece;

		if ( selectedPiece is null )
			return;

		var resources = BuildResources;

		if ( !resources.IsValid() )
		{
			Log.Warning( "Cannot place piece: player has no PlayerBuildResources component." );
			return;
		}

		if ( !resources.TrySpend( selectedPiece.Cost ) )
		{
			Log.Info( $"Cannot afford {selectedPiece.DisplayName}. Cost: {selectedPiece.Cost}, Resources: {resources.Resources}" );
			return;
		}

		PlacePiece( position, rotation );
	}

	[Rpc.Host]
	private void RequestPlacePiece( Vector3 position, Rotation rotation )
	{
		TryPlacePieceHost( position, rotation );
	}

	private void PlacePiece( Vector3 position, Rotation rotation )
	{
		if ( !CanUseSelectedPiece() )
			return;

		var factory = Factory;

		if ( !factory.IsValid() )
			return;

		var owner = Inventory.IsValid() ? Inventory.GameObject : GameObject;

		factory.SpawnPiece( SelectedPiece, position, rotation, owner );
	}

	private BuildPlacementResult GetPlacementResult()
	{
		var placement = Placement;

		if ( !placement.IsValid() )
		{
			return BuildPlacementResult.Invalid(
				WorldPosition,
				WorldRotation,
				"No BuildPlacement component."
			);
		}

		var ignoreObject = Inventory.IsValid() ? Inventory.GameObject : GameObject;

		return placement.GetPlacementResult(
			PlayerCamera,
			ignoreObject,
			SelectedPiece,
			WorldPosition,
			WorldRotation
		);
	}

	private bool CanUseSelectedPiece()
	{
		var selectedPiece = SelectedPiece;

		if ( selectedPiece is null )
		{
			Log.Warning( "BoatBuilder has no selected piece. Add pieces to AvailablePieces." );
			return false;
		}

		if ( !selectedPiece.Prefab.IsValid() )
		{
			Log.Warning( $"Selected piece {selectedPiece.DisplayName} has no valid prefab." );
			return false;
		}

		return true;
	}	

protected override void OnStart()
{
	if ( !Components.Get<BuildPreview>().IsValid() )
		Log.Warning( "BoatBuilder needs BuildPreview on the same GameObject." );

	if ( !Components.Get<BuildPlacement>().IsValid() )
		Log.Warning( "BoatBuilder needs BuildPlacement on the same GameObject." );

	if ( !Components.Get<BuildPieceFactory>().IsValid() )
		Log.Warning( "BoatBuilder needs BuildPieceFactory on the same GameObject." );
}

}