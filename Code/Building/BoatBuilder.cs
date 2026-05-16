using Sandbox;
using System.Collections.Generic;

public sealed class BoatBuilder : BaseCarryable
{
	private BuildPlacementResult CurrentPlacement;

	private BuildPreview Preview => Components.Get<BuildPreview>( FindMode.EverythingInSelfAndDescendants );
	private BuildPlacement Placement => Components.Get<BuildPlacement>( FindMode.EverythingInSelfAndDescendants );
	private BuildPieceFactory Factory => Components.Get<BuildPieceFactory>( FindMode.EverythingInSelfAndDescendants );

	public override string DisplayName => "Boat Builder";

	public BuildPlacementResult PlacementResult => CurrentPlacement;

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

	private PlayerBuildResources BuildResources
	{
		get
		{
			if ( !Inventory.IsValid() )
				return null;

			return Inventory.Components.Get<PlayerBuildResources>();
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

	protected override void OnStart()
	{
		ValidateRequiredComponents();
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
		if ( !IsBuildingAllowed() )
		{
			Preview?.ClearPreview();
			return;
		}

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
		if ( !IsBuildingAllowed() )
		{
			Preview?.ClearPreview();
			return;
		}

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
		CurrentPlacement = ApplyPlacementRules( CurrentPlacement );

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

		if ( !Networking.IsHost )
		{
			RequestPlacePiece( placementResult.Position, placementResult.Rotation );
			return;
		}

		TryPlacePieceHost( placementResult.Position, placementResult.Rotation );
	}

	[Rpc.Host]
	private void RequestPlacePiece( Vector3 position, Rotation rotation )
	{
		TryPlacePieceHost( position, rotation );
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
			return BuildPlacementResult.Invalid( WorldPosition, WorldRotation, "No BuildPlacement component." );

		var ignoreObject = Inventory.IsValid() ? Inventory.GameObject : GameObject;

		return placement.GetPlacementResult(
			PlayerCamera,
			ignoreObject,
			SelectedPiece,
			WorldPosition,
			WorldRotation
		);
	}

	private BuildPlacementResult ApplyPlacementRules( BuildPlacementResult result )
	{
		if ( !result.IsValid )
			return result;

		if ( !IsBuildingAllowed() )
			return BuildPlacementResult.Invalid( result.Position, result.Rotation, "Building disabled." );

		if ( !CanAffordSelectedPiece() )
			return BuildPlacementResult.Invalid( result.Position, result.Rotation, "Cannot afford." );

		return result;
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

	private void ValidateRequiredComponents()
	{
		if ( !Preview.IsValid() )
			Log.Warning( "BoatBuilder needs BuildPreview on the same GameObject." );

		if ( !Placement.IsValid() )
			Log.Warning( "BoatBuilder needs BuildPlacement on the same GameObject." );

		if ( !Factory.IsValid() )
			Log.Warning( "BoatBuilder needs BuildPieceFactory on the same GameObject." );
	}
}