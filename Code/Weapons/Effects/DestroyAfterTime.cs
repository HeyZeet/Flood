using Sandbox;

public sealed class DestroyAfterTime : Component
{
	[Property] public float LifeTime { get; set; } = 0.1f;

	private TimeSince TimeAlive { get; set; }

	protected override void OnStart()
	{
		TimeAlive = 0f;
	}

	protected override void OnUpdate()
	{
		if ( LifeTime <= 0f )
			return;

		if ( TimeAlive >= LifeTime )
			GameObject.Destroy();
	}
}