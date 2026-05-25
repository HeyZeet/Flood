using Sandbox;
using Sandbox.Physics;
using System;
using System.Collections.Generic;
using System.Linq;

public enum BuildPieceMaterial
{
	Wood,
	Metal,
	Plastic,
	Armor,
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

	private readonly struct StructuralBodyState
	{
		public bool Enabled { get; init; }
		public bool MotionEnabled { get; init; }
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
	[Sync( SyncFlags.FromHost ), Change( nameof( OnWeldSelectionChanged ) )]
	public bool IsSelectedForWelding { get; private set; }
	public GameObject AttachedTo { get; private set; }
	public bool AttachmentIsWelded { get; private set; }

	public BoatPieceHealth Health { get; private set; }
	public Rigidbody Rigidbody { get; private set; }
	public bool IsAttached => AttachedTo.IsValid();

	private readonly List<WeldConnection> WeldConnections = new();
	private readonly Dictionary<Rigidbody, StructuralBodyState> StructuralWeldBodyStates = new();
	private GameObject OriginalParent { get; set; }
	private Transform StructuralWeldLocalTransform { get; set; }
	private bool HadMotionEnabledBeforeWeld { get; set; } = true;
	private bool HasStructuralWeld { get; set; }
	private BoatPieceHealth SubscribedHealth;
	private bool HasStoredRendererTint { get; set; }
	private Color StoredRendererTint { get; set; } = Color.White;
	private static readonly Color WeldSelectionTint = new( 0.1f, 0.85f, 1f, 1f );

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

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost )
			return;

		EnforceStructuralWeld();
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

		SetWeldSelected( false );
		RemoveWeldJoints();
		DetachChildren();

		IsPlaced = false;
		AttachedTo = null;
		AttachmentIsWelded = false;
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

		if ( WouldCreateAttachmentCycle( other ) )
			return false;

		return true;
	}

	public void ClearSoftAttachment()
	{
		if ( !Networking.IsHost )
			return;

		if ( AttachmentIsWelded )
			return;

		AttachedTo = null;
		AttachmentIsWelded = false;
	}

	public BuildPiece GetWeldRoot()
	{
		var current = this;

		for ( var i = 0; i < 32; i++ )
		{
			if ( !current.IsValid() )
				return this;

			if ( !current.AttachmentIsWelded || !current.AttachedTo.IsValid() )
				return current;

			var parentPiece = current.AttachedTo.Components.Get<BuildPiece>( FindMode.EverythingInSelfAndAncestors );

			if ( !parentPiece.IsValid() )
				return current;

			current = parentPiece;
		}

		return current;
	}

	public List<BuildPiece> GetWeldedPieces()
	{
		var root = GetWeldRoot();
		var pieces = new List<BuildPiece>();

		foreach ( var piece in AllPieces.ToArray() )
		{
			if ( !piece.IsValid() )
				continue;

			if ( piece.GetWeldRoot() == root )
				pieces.Add( piece );
		}

		return pieces;
	}

	public void AttachTo( BuildPiece other, bool welded = false )
	{
		if ( !Networking.IsHost )
			return;

		if ( !CanAttachTo( other ) )
			return;

		AttachedTo = other.GameObject;
		AttachmentIsWelded = welded;

		var attachmentType = welded ? "welded" : "attached";
		Log.Info( $"{DisplayName} {attachmentType} to {other.DisplayName}." );
	}

	public bool WeldTo(
		BuildPiece other,
		Vector3 weldPosition,
		float linearStrength,
		float angularStrength,
		bool enableCollisions,
		bool createPhysicalJoint = false )
	{
		if ( !Networking.IsHost )
			return false;

		if ( !other.IsValid() )
			return false;

		var ownRoot = GetWeldRoot();
		var otherRoot = other.GetWeldRoot();

		if ( !ownRoot.IsValid() || !otherRoot.IsValid() )
			return false;

		if ( ownRoot == otherRoot )
			return true;

		if ( !ownRoot.CanAttachTo( otherRoot ) )
			return false;

		ownRoot.CacheComponents();
		otherRoot.CacheComponents();

		if ( !ownRoot.Rigidbody.IsValid() || !otherRoot.Rigidbody.IsValid() )
			return false;

		ownRoot.ClearSoftAttachment();
		otherRoot.ClearSoftAttachment();

		if ( !createPhysicalJoint )
		{
			ownRoot.AttachStructurallyTo( otherRoot );
			return true;
		}

		var body = ownRoot.Rigidbody.PhysicsBody;
		var otherBody = otherRoot.Rigidbody.PhysicsBody;

		if ( body is null || otherBody is null )
			return false;

		var resolvedWeldPosition = ownRoot.ResolveWeldPosition( otherRoot, weldPosition );
		var point = PhysicsPoint.World( body, resolvedWeldPosition, ownRoot.WorldRotation );
		var otherPoint = PhysicsPoint.World( otherBody, resolvedWeldPosition, otherRoot.WorldRotation );

		// The physics joint keeps the pieces together; AttachedTo keeps raft ownership/stability cheap to query.
		var joint = PhysicsJoint.CreateFixed( point, otherPoint );

		if ( joint is null )
			return false;

		joint.Strength = linearStrength;
		joint.AngularStrength = angularStrength;
		joint.Collisions = enableCollisions;
		joint.SpringLinear = new PhysicsSpring( linearStrength * 0.2f, 10f );
		joint.SpringAngular = new PhysicsSpring( angularStrength * 0.2f, 12f );

		ownRoot.WeldConnections.Add( new WeldConnection
		{
			Joint = joint,
			OtherPiece = otherRoot,
			BaseLinearStrength = linearStrength,
			BaseAngularStrength = angularStrength
		} );

		ownRoot.RefreshConnectedWeldStrengths();
		ownRoot.AttachTo( otherRoot, true );

		Log.Info( $"Created fixed weld between {ownRoot.DisplayName} and {otherRoot.DisplayName}." );

		return true;
	}

	private void AttachStructurallyTo( BuildPiece other )
	{
		if ( !CanAttachTo( other ) )
			return;

		CacheComponents();

		if ( OriginalParent is null )
			OriginalParent = GameObject.Parent;

		if ( Rigidbody.IsValid() )
			HadMotionEnabledBeforeWeld = Rigidbody.MotionEnabled;

		DisableStructuralChildPhysics();

		var worldTransform = GameObject.WorldTransform;
		GameObject.SetParent( other.GameObject );
		GameObject.WorldTransform = worldTransform;
		StructuralWeldLocalTransform = GameObject.LocalTransform;
		HasStructuralWeld = true;

		AttachTo( other, true );

		Log.Info( $"Structurally welded {DisplayName} to {other.DisplayName}." );
	}

	private void DisableStructuralChildPhysics()
	{
		StructuralWeldBodyStates.Clear();

		foreach ( var body in Components.GetAll<Rigidbody>( FindMode.EverythingInSelfAndDescendants ).ToArray() )
		{
			if ( !body.IsValid() )
				continue;

			StructuralWeldBodyStates[body] = new StructuralBodyState
			{
				Enabled = body.Enabled,
				MotionEnabled = body.MotionEnabled
			};

			body.Velocity = Vector3.Zero;
			body.AngularVelocity = Vector3.Zero;
			body.MotionEnabled = false;
			body.Enabled = false;
		}
	}

	private void EnforceStructuralWeld()
	{
		if ( !HasStructuralWeld )
			return;

		if ( !AttachmentIsWelded || !AttachedTo.IsValid() )
		{
			RestoreStructuralWeldState();
			return;
		}

		if ( GameObject.Parent != AttachedTo )
			GameObject.SetParent( AttachedTo );

		GameObject.LocalTransform = StructuralWeldLocalTransform;

		foreach ( var body in StructuralWeldBodyStates.Keys.ToArray() )
		{
			if ( !body.IsValid() )
				continue;

			body.Velocity = Vector3.Zero;
			body.AngularVelocity = Vector3.Zero;
			body.MotionEnabled = false;
			body.Enabled = false;
		}
	}

	private Vector3 ResolveWeldPosition( BuildPiece other, Vector3 requestedWeldPosition )
	{
		if ( !Rigidbody.IsValid() || !other.IsValid() || !other.Rigidbody.IsValid() )
			return requestedWeldPosition;

		var ownBounds = Rigidbody.GetWorldBounds();
		var otherBounds = other.Rigidbody.GetWorldBounds();
		var ownContact = ownBounds.ClosestPoint( requestedWeldPosition );
		var otherContact = otherBounds.ClosestPoint( requestedWeldPosition );
		var contactMidpoint = (ownContact + otherContact) * 0.5f;

		if ( contactMidpoint.IsNaN )
			return requestedWeldPosition;

		return contactMidpoint;
	}

	public void Detach()
	{
		if ( !Networking.IsHost )
			return;

		RemoveWeldJoints();
		RestoreStructuralWeldState();
		AttachedTo = null;
		AttachmentIsWelded = false;
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

	private bool WouldCreateAttachmentCycle( BuildPiece other )
	{
		var current = other;

		for ( var i = 0; i < 32; i++ )
		{
			if ( !current.IsValid() )
				return false;

			if ( current == this )
				return true;

			if ( !current.AttachmentIsWelded || !current.AttachedTo.IsValid() )
				return false;

			current = current.AttachedTo.Components.Get<BuildPiece>( FindMode.EverythingInSelfAndAncestors );
		}

		return true;
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
		RestoreStructuralWeldState();
		AttachedTo = null;
		AttachmentIsWelded = false;
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

	private void RestoreStructuralWeldState()
	{
		if ( !HasStructuralWeld )
			return;

		var worldTransform = GameObject.WorldTransform;

		if ( OriginalParent.IsValid() )
			GameObject.SetParent( OriginalParent );
		else
			GameObject.SetParent( null );

		GameObject.WorldTransform = worldTransform;

		foreach ( var entry in StructuralWeldBodyStates.ToArray() )
		{
			if ( entry.Key.IsValid() )
			{
				entry.Key.Enabled = entry.Value.Enabled;
				entry.Key.MotionEnabled = entry.Value.MotionEnabled;
			}
		}

		StructuralWeldBodyStates.Clear();

		if ( Rigidbody.IsValid() && !HadMotionEnabledBeforeWeld )
			Rigidbody.MotionEnabled = HadMotionEnabledBeforeWeld;

		OriginalParent = null;
		StructuralWeldLocalTransform = default;
		HasStructuralWeld = false;
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
