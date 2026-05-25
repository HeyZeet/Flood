using Sandbox;
using System;
using System.Collections.Generic;

public sealed class BoatBuilder : BaseCarryable
{
	public static BoatBuilder LocalBuildMenuBuilder { get; private set; }
	public static bool IsLocalBuildMenuOpen => LocalBuildMenuBuilder.IsValid() && LocalBuildMenuBuilder.IsBuildMenuOpen;

	private BuildPlacementResult CurrentPlacement;

	private BuildPreview Preview => Components.Get<BuildPreview>( FindMode.EverythingInSelfAndDescendants );
	private BuildPlacement Placement => Components.Get<BuildPlacement>( FindMode.EverythingInSelfAndDescendants );
	private BuildPieceFactory Factory => Components.Get<BuildPieceFactory>( FindMode.EverythingInSelfAndDescendants );

	public override string DisplayName => "Boat Builder";

	public BuildPlacementResult PlacementResult => CurrentPlacement;

	[Property] public List<BuildPieceData> AvailablePieces { get; set; } = new();
	[Property, Sync( SyncFlags.FromHost )] public int SelectedPieceIndex { get; set; } = 0;
	[Property, Group( "Build Menu" )] public string BuildMenuInputAction { get; set; } = "menu";

	[Property, Group( "Selling" )] public bool CanSellPieces { get; set; } = true;
	[Property, Group( "Selling" )] public string SellInputAction { get; set; } = "use";
	[Property, Group( "Selling" )] public float SellTraceDistance { get; set; } = 350f;
	[Property, Group( "Selling" )] public float SellRefundFraction { get; set; } = 0.75f;

	public bool IsBuildMenuOpen { get; private set; }

	public BuildPieceData SelectedPiece
	{
		get
		{
			if ( AvailablePieces is null || AvailablePieces.Count == 0 )
				return null;

			var clampedIndex = SelectedPieceIndex.Clamp( 0, AvailablePieces.Count - 1 );
			return AvailablePieces[clampedIndex];
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
		if ( LocalBuildMenuBuilder == this )
			LocalBuildMenuBuilder = null;

		CloseBuildMenu();
		Preview?.ClearPreview();

		base.OnHolster();

		Log.Info( "Boat Builder holstered." );
	}

	public override void OnPlayerUpdate()
	{
		LocalBuildMenuBuilder = this;

		if ( !IsBuildingAllowed() )
		{
			CloseBuildMenu();
			Preview?.ClearPreview();
			return;
		}

		if ( WasBuildMenuPressed() )
			ToggleBuildMenu();

		if ( IsBuildMenuOpen )
		{
			UpdatePreview();
			return;
		}

		UpdatePreview();

		if ( Input.Pressed( "attack1" ) )
			TryPlacePiece();

		if ( Input.Pressed( "attack2" ) )
			SelectNextPiece();

		if ( Input.Pressed( "reload" ) )
			Placement?.RotatePlacement();

		if ( CanSellPieces && Input.Pressed( SellInputAction ) )
			TrySellLookedAtPiece();
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
			RequestPlacePiece( SelectedPieceIndex, placementResult.Position, placementResult.Rotation );
			return;
		}

		TryPlacePieceHost( SelectedPieceIndex, placementResult.Position, placementResult.Rotation );
	}

	[Rpc.Host]
	private void RequestPlacePiece( int selectedPieceIndex, Vector3 position, Rotation rotation )
	{
		TryPlacePieceHost( selectedPieceIndex, position, rotation );
	}

	private void TryPlacePieceHost( int selectedPieceIndex, Vector3 position, Rotation rotation )
	{
		if ( !IsBuildingAllowed() )
		{
			Log.Info( "Cannot place piece: building is disabled." );
			return;
		}

		if ( !IsActive )
		{
			Log.Info( "Cannot place piece: boat builder is not active." );
			return;
		}

		if ( !TrySelectPieceHost( selectedPieceIndex ) )
			return;

		var selectedPiece = GetPieceAtIndex( selectedPieceIndex );

		if ( selectedPiece is null )
			return;

		var placementResult = ValidateRequestedPlacementHost( selectedPiece, position, rotation );

		if ( !placementResult.IsValid )
		{
			Log.Info( $"Cannot place piece: {placementResult.Reason}" );
			return;
		}

		var resources = BuildResources;

		if ( !resources.IsValid() )
		{
			Log.Warning( "Cannot place piece: player has no PlayerBuildResources component." );
			return;
		}

		if ( !resources.CanAfford( selectedPiece.Cost ) )
		{
			Log.Info( $"Cannot afford {selectedPiece.DisplayName}. Cost: {selectedPiece.Cost}, Resources: {resources.Resources}" );
			return;
		}

		var spawnResult = PlacePiece( selectedPiece, placementResult.Position, placementResult.Rotation );

		if ( !spawnResult.Success )
		{
			Log.Warning( $"Failed to place {selectedPiece.DisplayName}: {spawnResult.Reason}" );
			return;
		}

		if ( !resources.TrySpend( selectedPiece.Cost ) )
		{
			if ( spawnResult.PieceObject.IsValid() )
				spawnResult.PieceObject.Destroy();

			Log.Warning( $"Failed to spend resources for {selectedPiece.DisplayName}; removed spawned piece." );
		}
	}

	private BuildPieceSpawnResult PlacePiece( BuildPieceData pieceData, Vector3 position, Rotation rotation )
	{
		if ( pieceData is null )
			return BuildPieceSpawnResult.Failed( "No selected piece." );

		var factory = Factory;

		if ( !factory.IsValid() )
			return BuildPieceSpawnResult.Failed( "No BuildPieceFactory component." );

		var owner = Inventory.IsValid() ? Inventory.GameObject : GameObject;

		return factory.SpawnPiece( pieceData, position, rotation, owner );
	}

	private void TrySellLookedAtPiece()
	{
		if ( !IsBuildingAllowed() )
		{
			Log.Info( "Cannot sell piece: building is disabled." );
			return;
		}

		var buildPiece = FindLookedAtBuildPiece();

		if ( !buildPiece.IsValid() )
		{
			Log.Info( "Cannot sell piece: no build piece targeted." );
			return;
		}

		if ( !Networking.IsHost )
		{
			RequestSellPiece( buildPiece.GameObject );
			return;
		}

		TrySellPieceHost( buildPiece );
	}

	private BuildPiece FindLookedAtBuildPiece()
	{
		var camera = PlayerCamera;

		if ( !camera.IsValid() )
			return null;

		var trace = camera.TraceAim( SellTraceDistance );

		if ( !trace.Hit || !trace.GameObject.IsValid() )
			return null;

		return trace.GameObject.Components.Get<BuildPiece>( FindMode.EverythingInSelfAndAncestors );
	}

	[Rpc.Host]
	private void RequestSellPiece( GameObject buildPieceObject )
	{
		if ( !buildPieceObject.IsValid() )
			return;

		var buildPiece = buildPieceObject.Components.Get<BuildPiece>( FindMode.EverythingInSelfAndAncestors );

		TrySellPieceHost( buildPiece );
	}

	private void TrySellPieceHost( BuildPiece buildPiece )
	{
		if ( !IsBuildingAllowed() )
		{
			Log.Info( "Cannot sell piece: building is disabled." );
			return;
		}

		if ( !IsActive )
		{
			Log.Info( "Cannot sell piece: boat builder is not active." );
			return;
		}

		if ( !CanSellBuildPiece( buildPiece ) )
			return;

		var resources = BuildResources;

		if ( !resources.IsValid() )
		{
			Log.Warning( "Cannot sell piece: player has no PlayerBuildResources component." );
			return;
		}

		var refund = GetSellRefund( buildPiece );

		if ( refund > 0 )
			resources.AddResources( refund );

		Log.Info( $"Sold {buildPiece.DisplayName} for {refund} resources." );

		buildPiece.MarkUnplaced();
		buildPiece.GameObject.Destroy();
	}

	private bool CanSellBuildPiece( BuildPiece buildPiece )
	{
		if ( !buildPiece.IsValid() )
		{
			Log.Info( "Cannot sell piece: invalid build piece." );
			return false;
		}

		if ( !buildPiece.IsPlaced )
		{
			Log.Info( "Cannot sell piece: piece is not placed." );
			return false;
		}

		var owner = Inventory.IsValid() ? Inventory.GameObject : GameObject;

		if ( !buildPiece.CanPlayerModify( owner ) )
		{
			Log.Info( "Cannot sell piece: you do not own this piece." );
			return false;
		}

		if ( !IsBuildPieceInSellRange( buildPiece ) )
		{
			Log.Info( "Cannot sell piece: piece is too far away." );
			return false;
		}

		return true;
	}

	private bool IsBuildPieceInSellRange( BuildPiece buildPiece )
	{
		var builderPosition = Inventory.IsValid() ? Inventory.WorldPosition : WorldPosition;
		var distance = (builderPosition - buildPiece.WorldPosition).Length;

		return distance <= SellTraceDistance;
	}

	private int GetSellRefund( BuildPiece buildPiece )
	{
		var refundFraction = SellRefundFraction.Clamp( 0f, 1f );
		return (int)MathF.Round( buildPiece.Cost * refundFraction );
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
		var roundManager = FloodGameManager.Instance;

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
		return CanUsePiece( SelectedPiece );
	}

	private bool CanUsePiece( BuildPieceData pieceData )
	{
		if ( pieceData is null )
		{
			Log.Warning( "BoatBuilder has no selected piece. Add pieces to AvailablePieces." );
			return false;
		}

		if ( !pieceData.Prefab.IsValid() )
		{
			Log.Warning( $"Selected piece {pieceData.DisplayName} has no valid prefab." );
			return false;
		}

		return true;
	}

	private BuildPieceData GetPieceAtIndex( int pieceIndex )
	{
		if ( AvailablePieces is null || AvailablePieces.Count == 0 )
			return null;

		if ( pieceIndex < 0 || pieceIndex >= AvailablePieces.Count )
			return null;

		return AvailablePieces[pieceIndex];
	}

	private bool TrySelectPieceHost( int pieceIndex )
	{
		if ( !Networking.IsHost )
			return false;

		var pieceData = GetPieceAtIndex( pieceIndex );

		if ( !CanUsePiece( pieceData ) )
			return false;

		SelectedPieceIndex = pieceIndex;
		return true;
	}

	private BuildPlacementResult ValidateRequestedPlacementHost(
		BuildPieceData selectedPiece,
		Vector3 position,
		Rotation rotation )
	{
		var placement = Placement;

		if ( !placement.IsValid() )
			return BuildPlacementResult.Invalid( position, rotation, "No BuildPlacement component." );

		var ignoreObject = Inventory.IsValid() ? Inventory.GameObject : GameObject;
		var builderPosition = Inventory.IsValid() ? Inventory.WorldPosition : WorldPosition;

		return placement.ValidateRequestedPlacement(
			position,
			rotation,
			ignoreObject,
			selectedPiece,
			builderPosition
		);
	}

	private void SelectNextPiece()
	{
		if ( AvailablePieces is null || AvailablePieces.Count == 0 )
			return;

		SelectedPieceIndex++;

		if ( SelectedPieceIndex >= AvailablePieces.Count )
			SelectedPieceIndex = 0;

		RequestSelectedPieceIndex( SelectedPieceIndex );
		OnSelectedPieceChanged();
	}

	private void SelectPreviousPiece()
	{
		if ( AvailablePieces is null || AvailablePieces.Count == 0 )
			return;

		SelectedPieceIndex--;

		if ( SelectedPieceIndex < 0 )
			SelectedPieceIndex = AvailablePieces.Count - 1;

		RequestSelectedPieceIndex( SelectedPieceIndex );
		OnSelectedPieceChanged();
	}

	private void RequestSelectedPieceIndex( int pieceIndex )
	{
		if ( Networking.IsHost )
		{
			TrySelectPieceHost( pieceIndex );
			return;
		}

		SetSelectedPieceIndexRpc( pieceIndex );
	}

	[Rpc.Host]
	private void SetSelectedPieceIndexRpc( int pieceIndex )
	{
		TrySelectPieceHost( pieceIndex );
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

	public void SelectPieceFromBuildMenu( int pieceIndex )
	{
		if ( GetPieceAtIndex( pieceIndex ) is null )
			return;

		SelectedPieceIndex = pieceIndex;
		RequestSelectedPieceIndex( pieceIndex );
		OnSelectedPieceChanged();
		CloseBuildMenu();
	}

	public void ToggleBuildMenu()
	{
		if ( !IsBuildingAllowed() )
		{
			CloseBuildMenu();
			return;
		}

		IsBuildMenuOpen = !IsBuildMenuOpen;
		Log.Info( $"Build menu {(IsBuildMenuOpen ? "opened" : "closed")}." );
	}

	public void CloseBuildMenu()
	{
		IsBuildMenuOpen = false;
	}

	private bool WasBuildMenuPressed()
	{
		var inputAction = GetBuildMenuInputAction();

		if ( Input.Pressed( inputAction ) )
			return true;

		return inputAction != "menu" && Input.Pressed( "menu" );
	}

	private string GetBuildMenuInputAction()
	{
		if ( string.IsNullOrWhiteSpace( BuildMenuInputAction ) )
			return "menu";

		return BuildMenuInputAction.Trim();
	}

	private void ValidateRequiredComponents()
	{
		if ( !Preview.IsValid() )
			Log.Warning( "BoatBuilder needs BuildPreview on itself or a child GameObject." );

		if ( !Placement.IsValid() )
			Log.Warning( "BoatBuilder needs BuildPlacement on itself or a child GameObject." );

		if ( !Factory.IsValid() )
			Log.Warning( "BoatBuilder needs BuildPieceFactory on itself or a child GameObject." );
	}
}
