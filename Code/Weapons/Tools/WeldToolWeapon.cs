using Sandbox;
using System.Collections.Generic;
using System.Linq;

public sealed partial class WeldToolWeapon : BaseToolWeapon
{
	public override string DisplayName => "Weld Tool";

	[Property, Group( "Welding" )] public bool RequireBuildPhase { get; set; } = true;
	[Property, Group( "Welding" )] public float WeldLinearStrength { get; set; } = 1800f;
	[Property, Group( "Welding" )] public float WeldAngularStrength { get; set; } = 1200f;
	[Property, Group( "Welding" )] public bool EnableWeldedPieceCollisions { get; set; } = false;
	[Property, Group( "Welding" )] public bool CreatePhysicalJoints { get; set; } = false;
	[Property, Group( "Welding" )] public int MaxSelectedPieces { get; set; } = 12;
	[Property, Group( "Welding" )] public float MaxMultiWeldGap { get; set; } = 128f;
	[Property, Group( "Tool Sounds" )] public SoundEvent SelectSound { get; set; }
	[Property, Group( "Tool Sounds" )] public SoundEvent WeldSound { get; set; }
	[Property, Group( "Tool Sounds" )] public SoundEvent UnweldSound { get; set; }

	[Sync( SyncFlags.FromHost )] public string FirstWeldTargetName { get; private set; } = "No selection";
	[Sync( SyncFlags.FromHost )] public int SelectedPieceCount { get; private set; }

	private readonly List<GameObject> SelectedTargets = new();

	public override void OnPlayerUpdate()
	{
		base.OnPlayerUpdate();

		if ( Input.Pressed( "reload" ) )
			TryWeldCurrentSelection();
	}

	public BuildPiece FirstWeldPiece
	{
		get
		{
			var firstTarget = SelectedTargets.FirstOrDefault( target => target.IsValid() );

			if ( !firstTarget.IsValid() )
				return null;

			return firstTarget.Components.Get<BuildPiece>( FindMode.EverythingInSelfAndAncestors );
		}
	}

	public override void PrimaryAttack()
	{
		base.PrimaryAttack();
		PlayUseSound();

		if ( Networking.IsHost )
		{
			TryPrimaryUseHost( GetOwnerEyePosition(), GetOwnerAimDirection() );
			BroadcastWeaponAnimation( AttackTrigger, true );
			BroadcastToolSound( UseSound, true );
			return;
		}

		RequestPrimaryUse( GetOwnerEyePosition(), GetOwnerAimDirection() );
	}

	public override void SecondaryAttack()
	{
		base.SecondaryAttack();
		PlayUseSound();

		if ( Networking.IsHost )
		{
			TrySecondaryUseHost( GetOwnerEyePosition(), GetOwnerAimDirection() );
			BroadcastWeaponAnimation( AttackTrigger, true );
			BroadcastToolSound( UseSound, true );
			return;
		}

		RequestSecondaryUse( GetOwnerEyePosition(), GetOwnerAimDirection() );
	}

	public override void OnHolster()
	{
		if ( Networking.IsHost )
			ClearSelection();

		base.OnHolster();
	}

	[Rpc.Host]
	private void RequestPrimaryUse( Vector3 start, Vector3 direction )
	{
		if ( !IsToolAimRequestReasonable( start, direction ) )
			return;

		TryPrimaryUseHost( start, direction );
		BroadcastWeaponAnimation( AttackTrigger, true );
		BroadcastToolSound( UseSound, true );
	}

	[Rpc.Host]
	private void RequestSecondaryUse( Vector3 start, Vector3 direction )
	{
		if ( !IsToolAimRequestReasonable( start, direction ) )
			return;

		TrySecondaryUseHost( start, direction );
		BroadcastWeaponAnimation( AttackTrigger, true );
		BroadcastToolSound( UseSound, true );
	}

	private void TryPrimaryUseHost( Vector3 start, Vector3 direction )
	{
		if ( !CanUseToolNow() )
		{
			Fail( "Weld Tool can only be used during build phase." );
			return;
		}

		var trace = TraceTool( start, direction );
		var target = GetBuildPieceFromTrace( trace );

		if ( !target.IsValid() )
		{
			if ( SelectedPieceCount >= 2 )
			{
				TryWeldSelectedPieces();
				return;
			}

			Fail( "Weld Tool needs a build piece target." );
			return;
		}

		if ( IsSelected( target ) )
		{
			if ( SelectedPieceCount >= 2 )
				TryWeldSelectedPieces();
			else
				RemoveSelectedTarget( target );

			return;
		}

		AddSelectedTarget( target );
	}

	private void TrySecondaryUseHost( Vector3 start, Vector3 direction )
	{
		if ( !CanUseToolNow() )
		{
			Fail( "Weld Tool can only be used during build phase." );
			return;
		}

		var trace = TraceTool( start, direction );
		var target = GetBuildPieceFromTrace( trace );

		if ( !target.IsValid() )
		{
			ClearSelection();
			Fail( "Weld Tool selection cleared." );
			return;
		}

		if ( !CanModifyPiece( target ) )
		{
			Fail( "Cannot unweld a piece you do not own." );
			return;
		}

		target.BreakWelds( "manually unwelded" );
		ClearSelection();
		PlayToolSound( UnweldSound );
		BroadcastToolSound( UnweldSound, true );

		Log.Info( $"{DisplayName} unwelded {target.DisplayName}." );
	}

	private void AddSelectedTarget( BuildPiece target )
	{
		if ( !CanModifyPiece( target ) )
		{
			Fail( "Cannot weld a piece you do not own." );
			return;
		}

		PruneSelection();

		if ( SelectedTargets.Count >= MaxSelectedPieces.Clamp( 2, 64 ) )
		{
			Fail( "Too many pieces selected." );
			return;
		}

		SelectedTargets.Add( target.GameObject );
		target.SetWeldSelected( true );
		UpdateSelectionText();
		PlayToolSound( SelectSound );
		BroadcastToolSound( SelectSound, true );

		Log.Info( $"{DisplayName} selected {target.DisplayName}. Selected: {SelectedPieceCount}" );
	}

	private void RemoveSelectedTarget( BuildPiece target )
	{
		if ( !target.IsValid() )
			return;

		SelectedTargets.RemoveAll( gameObject => gameObject == target.GameObject );
		target.SetWeldSelected( false );
		UpdateSelectionText();
		PlayToolSound( SelectSound );
		BroadcastToolSound( SelectSound, true );

		Log.Info( $"{DisplayName} deselected {target.DisplayName}. Selected: {SelectedPieceCount}" );
	}

	private void TryWeldSelectedPieces()
	{
		PruneSelection();

		var selectedPieces = SelectedTargets
			.Select( target => target.Components.Get<BuildPiece>( FindMode.EverythingInSelfAndAncestors ) )
			.Where( piece => piece.IsValid() )
			.Distinct()
			.ToList();

		if ( selectedPieces.Count < 2 )
		{
			Fail( "Select at least two pieces to weld." );
			return;
		}

		if ( selectedPieces.Any( piece => !CanModifyPiece( piece ) ) )
		{
			Fail( "Cannot weld pieces you do not own." );
			return;
		}

		if ( !TryWeldSelectedPiecesIntoSingleRaft( selectedPieces, out var weldedCount ) )
		{
			Fail( "Failed to weld every selected piece into one raft." );
			return;
		}

		ClearSelection();
		PlayToolSound( WeldSound );
		PlaySuccessSound();
		BroadcastToolSound( WeldSound, true );
		BroadcastToolSound( SuccessSound, true );

		var finalRoot = selectedPieces[0].GetWeldRoot();
		var finalPieceCount = finalRoot.IsValid() ? finalRoot.GetWeldedPieces().Count : 0;

		Log.Info( $"{DisplayName} created {weldedCount} welds from {selectedPieces.Count} selected pieces. Final raft root: {finalRoot?.DisplayName ?? "None"}, raft pieces: {finalPieceCount}." );
	}

	private void TryWeldCurrentSelection()
	{
		if ( !CanUseToolNow() )
		{
			Fail( "Weld Tool can only be used during build phase." );
			return;
		}

		if ( Networking.IsHost )
		{
			TryWeldSelectedPieces();
			BroadcastWeaponAnimation( AttackTrigger, true );
			return;
		}

		RequestWeldCurrentSelection();
	}

	[Rpc.Host]
	private void RequestWeldCurrentSelection()
	{
		TryWeldSelectedPieces();
		BroadcastWeaponAnimation( AttackTrigger, true );
	}

	private BuildPiece GetBuildPieceFromTrace( SceneTraceResult trace )
	{
		if ( !trace.Hit || !trace.GameObject.IsValid() )
			return null;

		return trace.GameObject.Components.Get<BuildPiece>( FindMode.EverythingInSelfAndAncestors );
	}

	private bool CanModifyPiece( BuildPiece buildPiece )
	{
		if ( !buildPiece.IsValid() )
			return false;

		var owner = Inventory.IsValid() ? Inventory.GameObject : GameObject;
		return buildPiece.CanPlayerModify( owner );
	}

	private bool CanUseToolNow()
	{
		if ( !RequireBuildPhase )
			return true;

		var roundManager = FloodGameManager.Instance;

		if ( !roundManager.IsValid() )
			return true;

		return roundManager.IsBuildPhase();
	}

	private void ClearSelection()
	{
		foreach ( var target in SelectedTargets.ToArray() )
		{
			var buildPiece = target.Components.Get<BuildPiece>( FindMode.EverythingInSelfAndAncestors );

			if ( buildPiece.IsValid() )
				buildPiece.SetWeldSelected( false );
		}

		SelectedTargets.Clear();
		UpdateSelectionText();
	}

	private bool IsSelected( BuildPiece buildPiece )
	{
		if ( !buildPiece.IsValid() )
			return false;

		return SelectedTargets.Any( target => target == buildPiece.GameObject );
	}

	private void PruneSelection()
	{
		SelectedTargets.RemoveAll( target => !target.IsValid() );
		UpdateSelectionText();
	}

	private void UpdateSelectionText()
	{
		SelectedPieceCount = SelectedTargets.Count( target => target.IsValid() );
		FirstWeldTargetName = SelectedPieceCount > 0
			? $"{SelectedPieceCount} selected"
			: "No selection";
	}

	private void Fail( string reason )
	{
		PlayFailSound();
		Log.Info( $"{DisplayName}: {reason}" );
	}
}
