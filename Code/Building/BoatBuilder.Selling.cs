using Sandbox;
using System;

public sealed partial class BoatBuilder
{
	[Property, Group( "Selling" )] public bool CanSellPieces { get; set; } = true;
	[Property, Group( "Selling" )] public string SellInputAction { get; set; } = "use";
	[Property, Group( "Selling" )] public float SellTraceDistance { get; set; } = 350f;
	[Property, Group( "Selling" )] public float SellRefundFraction { get; set; } = 0.75f;

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
}
