using Sandbox;
using Sandbox.Physics;
using System;
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
	private sealed class WeldConnection
	{
		public PhysicsJoint Joint { get; init; }
		public BuildPiece OtherPiece { get; init; }
		public float BaseLinearStrength { get; init; }
		public float BaseAngularStrength { get; init; }
	}

	public static IReadOnlyList<BuildPiece> All => AllPieces;

	[Property] public string DisplayName { get; set; } = "Build Piece";
	[Property] public BuildPieceMaterial Material { get; set; } = BuildPieceMaterial.Wood;
	[Property] public int Cost { get; set; } = 10;
	[Property] public float Weight { get; set; } = 10f;

	[Property] public bool CountsAsBoatPart { get; set; } = true;
	[Property] public bool CanBeWelded { get; set; } = true;

	[Property, Group( "Welding" )]
	public bool WeakenWeldsWhenDamaged { get; set; } = true;

	[Property, Group( "Welding" )]
	public float MinDamagedWeldStrengthMultiplier { get; set; } = 0.35f;

	[Property, Group( "Welding" )]
	public bool BreakWeldsWhenCriticallyDamaged { get; set; } = true;

	[Property, Group( "Welding" )]
	public float CriticalWeldHealthFraction { get; set; } = 0.2f;

	[Property, Group( "Welding" )]
	public bool BreakWeldsWhenDestroyed { get; set; } = true;

	public GameObject Owner { get; private set; }
	[Sync( SyncFlags.FromHost )] public Guid OwnerConnectionId { get; private set; }
	[Sync( SyncFlags.FromHost )] public string OwnerName { get; private set; } = "";
	[Sync( SyncFlags.FromHost )] public bool IsPlaced { get; private set; }
	public GameObject AttachedTo { get; private set; }

	public BoatPieceHealth Health { get; private set; }
	public Rigidbody Rigidbody { get; private set; }
	public bool IsAttached => AttachedTo.IsValid();

	private readonly List<WeldConnection> WeldConnections = new();
	private BoatPieceHealth SubscribedHealth;

	protected override void OnStart()
	{
		if ( !AllPieces.Contains( this ) )
			AllPieces.Add( this );

		CacheComponents();
		SubscribeToHealthEvents();
	}

	protected override void OnDestroy()
	{
		UnsubscribeFromHealthEvents();
		RemoveWeldJoints();
		DetachChildren();
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
		OwnerConnectionId = GetOwnerConnectionId( owner );
		OwnerName = owner.IsValid() ? owner.Name : "";

		if ( OwnerConnectionId == Guid.Empty )
			Log.Warning( $"{DisplayName} was assigned an owner without a network owner id: {OwnerName}" );
	}

	public void MarkPlaced()
	{
		if ( !Networking.IsHost )
			return;

		IsPlaced = true;

		CacheComponents();
		SubscribeToHealthEvents();
		ValidateRequiredComponents();
	}

	public void MarkUnplaced()
	{
		if ( !Networking.IsHost )
			return;

		RemoveWeldJoints();
		DetachChildren();

		IsPlaced = false;
		AttachedTo = null;
	}

	public bool CanAttachTo( BuildPiece other )
	{
		if ( !Networking.IsHost )
			return false;

		if ( !IsPlaced || !other.IsValid() || !other.IsPlaced )
			return false;

		if ( !CanBeWelded || !other.CanBeWelded )
			return false;

		if ( other == this )
			return false;

		if ( !HasOwner() || !other.HasOwner() )
			return false;

		if ( !HasSameOwner( other ) )
			return false;

		return true;
	}

	public void AttachTo( BuildPiece other )
	{
		if ( !Networking.IsHost )
			return;

		if ( !CanAttachTo( other ) )
			return;

		AttachedTo = other.GameObject;

		Log.Info( $"{DisplayName} attached to {other.DisplayName}." );
	}

	public bool WeldTo(
		BuildPiece other,
		Vector3 weldPosition,
		float linearStrength,
		float angularStrength,
		bool enableCollisions )
	{
		if ( !Networking.IsHost )
			return false;

		if ( !CanAttachTo( other ) )
			return false;

		CacheComponents();
		other.CacheComponents();

		if ( !Rigidbody.IsValid() || !other.Rigidbody.IsValid() )
			return false;

		var body = Rigidbody.PhysicsBody;
		var otherBody = other.Rigidbody.PhysicsBody;

		if ( body is null || otherBody is null )
			return false;

		var point = PhysicsPoint.World( body, weldPosition, WorldRotation );
		var otherPoint = PhysicsPoint.World( otherBody, weldPosition, other.WorldRotation );

		// The physics joint keeps the pieces together; AttachedTo keeps raft ownership/stability cheap to query.
		var joint = PhysicsJoint.CreateFixed( point, otherPoint );

		if ( joint is null )
			return false;

		joint.Strength = linearStrength;
		joint.AngularStrength = angularStrength;
		joint.Collisions = enableCollisions;

		WeldConnections.Add( new WeldConnection
		{
			Joint = joint,
			OtherPiece = other,
			BaseLinearStrength = linearStrength,
			BaseAngularStrength = angularStrength
		} );

		RefreshConnectedWeldStrengths();
		AttachTo( other );

		Log.Info( $"Created fixed weld between {DisplayName} and {other.DisplayName}." );

		return true;
	}

	public void Detach()
	{
		if ( !Networking.IsHost )
			return;

		RemoveWeldJoints();
		AttachedTo = null;
	}

	public void BreakWelds( string reason = "manually unwelded" )
	{
		if ( !Networking.IsHost )
			return;

		BreakConnectedWelds( reason );
	}

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
		SubscribeToHealthEvents();

		if ( !Health.IsValid() )
			return;

		Health.MaxHealth = data.MaxHealth;
		Health.ResetHealth();
	}

	private void RemoveWeldJoints()
	{
		if ( !Networking.IsHost )
			return;

		foreach ( var connection in WeldConnections.ToArray() )
		{
			connection.Joint?.Remove();
		}

		WeldConnections.Clear();
	}

	private void DetachChildren()
	{
		if ( !Networking.IsHost )
			return;

		foreach ( var piece in AllPieces.ToArray() )
		{
			if ( !piece.IsValid() )
				continue;

			if ( piece.AttachedTo != GameObject )
				continue;

			piece.Detach();
			Log.Info( $"{piece.DisplayName} detached because {DisplayName} was destroyed." );
		}
	}

	private void SubscribeToHealthEvents()
	{
		if ( !Networking.IsHost )
			return;

		if ( SubscribedHealth == Health )
			return;

		UnsubscribeFromHealthEvents();

		if ( !Health.IsValid() )
			return;

		SubscribedHealth = Health;
		SubscribedHealth.Damaged += OnBoatPieceDamaged;
		SubscribedHealth.Died += OnBoatPieceDied;
	}

	private void UnsubscribeFromHealthEvents()
	{
		if ( !SubscribedHealth.IsValid() )
			return;

		SubscribedHealth.Damaged -= OnBoatPieceDamaged;
		SubscribedHealth.Died -= OnBoatPieceDied;
		SubscribedHealth = null;
	}

	private void OnBoatPieceDamaged( BoatPieceHealth health, DamageInfo damageInfo )
	{
		if ( !Networking.IsHost )
			return;

		if ( BreakWeldsWhenCriticallyDamaged && health.HealthFraction > 0f && health.HealthFraction <= CriticalWeldHealthFraction )
		{
			BreakConnectedWelds( "critically damaged" );
			return;
		}

		RefreshConnectedWeldStrengths();
	}

	private void OnBoatPieceDied( BoatPieceHealth health, DamageInfo damageInfo )
	{
		if ( !Networking.IsHost || !BreakWeldsWhenDestroyed )
			return;

		BreakConnectedWelds( "destroyed" );
	}

	private void BreakConnectedWelds( string reason )
	{
		RemoveWeldJoints();
		AttachedTo = null;
		DetachChildren();

		Log.Info( $"{DisplayName} broke connected welds because it was {reason}." );
	}

	private void RefreshConnectedWeldStrengths()
	{
		if ( !Networking.IsHost || !WeakenWeldsWhenDamaged )
			return;

		UpdateWeldStrengths();

		foreach ( var piece in AllPieces.ToArray() )
		{
			if ( !piece.IsValid() )
				continue;

			if ( piece.AttachedTo != GameObject )
				continue;

			piece.UpdateWeldStrengths();
		}
	}

	private void UpdateWeldStrengths()
	{
		foreach ( var connection in WeldConnections.ToArray() )
		{
			if ( connection.Joint is null )
				continue;

			var multiplier = GetWeldHealthMultiplier( connection.OtherPiece );

			connection.Joint.Strength = connection.BaseLinearStrength * multiplier;
			connection.Joint.AngularStrength = connection.BaseAngularStrength * multiplier;
		}
	}

	private float GetWeldHealthMultiplier( BuildPiece otherPiece )
	{
		var ownHealth = Health.IsValid() ? Health.HealthFraction : 1f;
		var otherHealth = otherPiece.IsValid() && otherPiece.Health.IsValid() ? otherPiece.Health.HealthFraction : 1f;
		var healthFraction = MathF.Min( ownHealth, otherHealth );
		var minMultiplier = MinDamagedWeldStrengthMultiplier.Clamp( 0f, 1f );

		return minMultiplier + (1f - minMultiplier) * healthFraction;
	}
}
