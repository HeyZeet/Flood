using Sandbox;
using System;

public sealed partial class BuildPiece
{
	private bool HasStoredRendererTint { get; set; }
	private Color StoredRendererTint { get; set; } = Color.White;
	private static readonly Color WeldSelectionTint = new( 0.1f, 0.85f, 1f, 1f );

	public bool CanPlayerModify( GameObject player )
	{
		if ( !player.IsValid() )
			return false;

		if ( !HasOwner() )
			return false;

		var playerOwnerId = GetOwnerConnectionId( player );

		if ( OwnerConnectionId != Guid.Empty && playerOwnerId != Guid.Empty )
			return OwnerConnectionId == playerOwnerId;

		return Owner == player;
	}

	public void SetWeldSelected( bool selected )
	{
		if ( Networking.IsHost )
			IsSelectedForWelding = selected;

		ApplyWeldSelectionHighlight();
	}

	public bool HasOwner()
	{
		return Owner.IsValid() || OwnerConnectionId != Guid.Empty;
	}

	private bool HasSameOwner( BuildPiece other )
	{
		if ( !other.IsValid() )
			return false;

		if ( OwnerConnectionId != Guid.Empty && other.OwnerConnectionId != Guid.Empty )
			return OwnerConnectionId == other.OwnerConnectionId;

		return Owner.IsValid() && other.Owner.IsValid() && Owner == other.Owner;
	}

	private Guid GetOwnerConnectionId( GameObject owner )
	{
		if ( !owner.IsValid() )
			return Guid.Empty;

		var current = owner;

		while ( current.IsValid() )
		{
			if ( current.Network.Active && current.Network.OwnerId != Guid.Empty )
				return current.Network.OwnerId;

			current = current.Parent;
		}

		return Guid.Empty;
	}

	private void OnWeldSelectionChanged( bool oldValue, bool newValue )
	{
		ApplyWeldSelectionHighlight();
	}

	private void ApplyWeldSelectionHighlight()
	{
		var renderer = Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndDescendants );

		if ( !renderer.IsValid() )
			return;

		if ( IsSelectedForWelding )
		{
			if ( !HasStoredRendererTint )
			{
				StoredRendererTint = renderer.Tint;
				HasStoredRendererTint = true;
			}

			renderer.Tint = WeldSelectionTint;
			return;
		}

		if ( !HasStoredRendererTint )
			return;

		renderer.Tint = StoredRendererTint;
		HasStoredRendererTint = false;
	}
}
