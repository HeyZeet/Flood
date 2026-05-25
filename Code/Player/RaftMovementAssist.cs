using Sandbox;

public sealed class RaftMovementAssist : Component
{
	[Property] public bool AssistEnabled { get; set; } = false;
	[Property] public bool EnabledDuringBuildPhase { get; set; } = false;
	[Property] public float GroundTraceStartHeight { get; set; } = 8f;
	[Property] public float GroundTraceDistance { get; set; } = 18f;
	[Property] public float RaftVelocityInfluence { get; set; } = 0.85f;
	[Property] public float MaxInheritedVelocity { get; set; } = 260f;
	[Property] public bool InheritVerticalVelocity { get; set; } = false;
	[Property] public float GroundStickVelocity { get; set; } = 35f;
	[Property] public bool DrawDebug { get; set; } = false;

	private FloodPlayer Player { get; set; }
	private Rigidbody Body { get; set; }
	private Vector3 LastInheritedRaftVelocity { get; set; }
	private BuildPiece LastGroundPiece { get; set; }

	protected override void OnStart()
	{
		CacheComponents();
	}

	protected override void OnFixedUpdate()
	{
		if ( !ShouldAssistMovement() )
		{
			ClearRaftState();
			return;
		}

		if ( !Body.IsValid() )
			CacheComponents();

		if ( !Body.IsValid() )
			return;

		var groundPiece = TraceGroundBuildPiece();

		if ( !groundPiece.IsValid() )
		{
			ClearRaftState();
			return;
		}

		var raftBody = GetRaftCarrierBody( groundPiece );

		if ( !raftBody.IsValid() )
		{
			ClearRaftState();
			return;
		}

		var inheritedVelocity = raftBody.Velocity.ClampLength( MaxInheritedVelocity ) * RaftVelocityInfluence.Clamp( 0f, 1f );

		if ( !InheritVerticalVelocity )
			inheritedVelocity = inheritedVelocity.WithZ( 0f );

		if ( LastGroundPiece != groundPiece )
			LastInheritedRaftVelocity = inheritedVelocity;

		var velocityDelta = inheritedVelocity - LastInheritedRaftVelocity;
		Body.Velocity += velocityDelta;

		if ( InheritVerticalVelocity && Body.Velocity.z < inheritedVelocity.z )
			Body.Velocity = Body.Velocity.WithZ( inheritedVelocity.z );

		if ( GroundStickVelocity > 0f && Body.Velocity.z > -GroundStickVelocity )
			Body.Velocity -= Vector3.Up * GroundStickVelocity * Time.Delta;

		LastInheritedRaftVelocity = inheritedVelocity;
		LastGroundPiece = groundPiece;

		if ( DrawDebug )
			DebugOverlay.Line( WorldPosition, WorldPosition + inheritedVelocity * 0.25f, Color.Green, 0f );
	}

	private void CacheComponents()
	{
		Player = Components.Get<FloodPlayer>( FindMode.EverythingInSelfAndAncestors );
		Body = Components.Get<Rigidbody>( FindMode.EverythingInSelfAndAncestors );
	}

	private bool ShouldAssistMovement()
	{
		if ( !AssistEnabled )
			return false;

		if ( !Player.IsValid() )
			CacheComponents();

		if ( Player.IsValid() && !Player.IsLocalPlayer && !Networking.IsHost )
			return false;

		if ( Player.IsValid() && Player.IsDead )
			return false;

		if ( EnabledDuringBuildPhase )
			return true;

		var roundManager = FloodGameManager.Instance;

		if ( !roundManager.IsValid() )
			return true;

		return !roundManager.IsBuildPhase();
	}

	private BuildPiece TraceGroundBuildPiece()
	{
		var start = WorldPosition + Vector3.Up * GroundTraceStartHeight;
		var end = start + Vector3.Down * GroundTraceDistance;
		var trace = Scene.Trace
			.Ray( start, end )
			.WithoutTags( "trigger", "water" )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( DrawDebug )
			DebugOverlay.Trace( trace, 0f );

		if ( !trace.Hit || !trace.GameObject.IsValid() )
			return null;

		return trace.GameObject.Components.Get<BuildPiece>( FindMode.EverythingInSelfAndAncestors );
	}

	private Rigidbody GetRaftCarrierBody( BuildPiece groundPiece )
	{
		var current = groundPiece;

		for ( var i = 0; i < 16; i++ )
		{
			if ( !current.IsValid() )
				break;

			if ( current.Rigidbody.IsValid() && current.Rigidbody.MotionEnabled )
				return current.Rigidbody;

			if ( !current.AttachedTo.IsValid() )
				break;

			current = current.AttachedTo.Components.Get<BuildPiece>( FindMode.EverythingInSelfAndAncestors );
		}

		if ( groundPiece.Rigidbody.IsValid() )
			return groundPiece.Rigidbody;

		return null;
	}

	private void ClearRaftState()
	{
		LastInheritedRaftVelocity = Vector3.Zero;
		LastGroundPiece = null;
	}
}
