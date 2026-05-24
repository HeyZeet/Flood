using Sandbox;

public sealed class WeldToolWeapon : BaseToolWeapon
{
	public override string DisplayName => "Weld Tool";

	[Header( "Welding" )]
	[Property] public bool RequireBuildPhase { get; set; } = true;
	[Property] public float WeldLinearStrength { get; set; } = 7000f;
	[Property] public float WeldAngularStrength { get; set; } = 5000f;
	[Property] public bool EnableWeldedPieceCollisions { get; set; } = false;
	[Property] public SoundEvent SelectSound { get; set; }
	[Property] public SoundEvent WeldSound { get; set; }
	[Property] public SoundEvent UnweldSound { get; set; }

	[Sync( SyncFlags.FromHost )] public string FirstWeldTargetName { get; private set; } = "No selection";

	private GameObject FirstWeldTarget { get; set; }

	public BuildPiece FirstWeldPiece
	{
		get
		{
			if ( !FirstWeldTarget.IsValid() )
				return null;

			return FirstWeldTarget.Components.Get<BuildPiece>( FindMode.EverythingInSelfAndAncestors );
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
			Fail( "Weld Tool needs a build piece target." );
			return;
		}

		var firstPiece = FirstWeldPiece;

		if ( !firstPiece.IsValid() )
		{
			SelectFirstTarget( target );
			return;
		}

		if ( firstPiece == target )
		{
			ClearSelection();
			Fail( "Weld Tool selection cleared." );
			return;
		}

		TryWeldSelectedPieces( firstPiece, target, trace.HitPosition );
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

	private void SelectFirstTarget( BuildPiece target )
	{
		if ( !CanModifyPiece( target ) )
		{
			Fail( "Cannot weld a piece you do not own." );
			return;
		}

		FirstWeldTarget = target.GameObject;
		FirstWeldTargetName = target.DisplayName;
		PlayToolSound( SelectSound );
		BroadcastToolSound( SelectSound, true );

		Log.Info( $"{DisplayName} selected {target.DisplayName}." );
	}

	private void TryWeldSelectedPieces( BuildPiece firstPiece, BuildPiece secondPiece, Vector3 weldPosition )
	{
		if ( !CanModifyPiece( firstPiece ) || !CanModifyPiece( secondPiece ) )
		{
			Fail( "Cannot weld pieces you do not own." );
			return;
		}

		if ( !firstPiece.WeldTo( secondPiece, weldPosition, WeldLinearStrength, WeldAngularStrength, EnableWeldedPieceCollisions ) )
		{
			ClearSelection();
			Fail( "Failed to weld selected pieces." );
			return;
		}

		ClearSelection();
		PlayToolSound( WeldSound );
		PlaySuccessSound();
		BroadcastToolSound( WeldSound, true );
		BroadcastToolSound( SuccessSound, true );

		Log.Info( $"{DisplayName} welded {firstPiece.DisplayName} to {secondPiece.DisplayName}." );
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
		FirstWeldTarget = null;
		FirstWeldTargetName = "No selection";
	}

	private void Fail( string reason )
	{
		PlayFailSound();
		Log.Info( $"{DisplayName}: {reason}" );
	}
}
