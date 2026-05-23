using Sandbox;

public abstract class BaseToolWeapon : BaseWeapon
{
	[Header( "Tool Trace" )]
	[Property] public float ToolRange { get; set; } = 350f;
	[Property] public float ToolTraceRadius { get; set; } = 4f;
	[Property] public bool DrawDebugToolTrace { get; set; } = true;

	[Header( "Tool Sounds" )]
	[Property] public SoundEvent UseSound { get; set; }
	[Property] public SoundEvent SuccessSound { get; set; }
	[Property] public SoundEvent FailSound { get; set; }

	protected override void OnStart()
	{
		base.OnStart();

		RepeatPrimaryAttackWhileHeld = false;
		RepeatSecondaryAttackWhileHeld = false;
	}

	protected SceneTraceResult TraceTool()
	{
		return TraceTool( GetOwnerEyePosition(), GetOwnerAimDirection() );
	}

	protected SceneTraceResult TraceTool( Vector3 start, Vector3 direction )
	{
		var trace = TraceFromAim( start, direction, ToolRange, ToolTraceRadius );

		if ( DrawDebugToolTrace )
			DebugOverlay.Trace( trace, 1f );

		return trace;
	}

	protected bool IsToolAimRequestReasonable( Vector3 start, Vector3 direction )
	{
		var owner = OwnerPlayer;

		if ( !owner.IsValid() )
			return false;

		if ( direction.Length < 0.1f )
			return false;

		var maxEyeDistance = 160f;
		return (start - owner.WorldPosition).Length <= maxEyeDistance;
	}

	protected void PlayUseSound()
	{
		PlayToolSound( UseSound );
	}

	protected void PlaySuccessSound()
	{
		PlayToolSound( SuccessSound );
	}

	protected void PlayFailSound()
	{
		PlayToolSound( FailSound );
	}

	protected void PlayToolSound( SoundEvent sound )
	{
		if ( sound is null )
			return;

		Sound.Play( sound, WorldPosition );
	}

	protected void BroadcastToolSound( SoundEvent sound, bool skipLocalOwner = true )
	{
		if ( !Networking.IsHost )
			return;

		if ( sound is null )
			return;

		PlayToolSoundBroadcast( sound, skipLocalOwner );
	}

	[Rpc.Broadcast]
	private void PlayToolSoundBroadcast( SoundEvent sound, bool skipLocalOwner )
	{
		if ( skipLocalOwner && IsLocallyControlled() )
			return;

		PlayToolSound( sound );
	}
}
