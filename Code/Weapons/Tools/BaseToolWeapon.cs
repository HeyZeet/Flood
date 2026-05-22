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
		var trace = TraceFromOwnerAim( ToolRange, ToolTraceRadius );

		if ( DrawDebugToolTrace )
			DebugOverlay.Trace( trace, 1f );

		return trace;
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
}
