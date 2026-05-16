using Sandbox;

public sealed class FloodPlayer : Component, PlayerController.IEvents
{
	public PlayerController Controller { get; private set; }
	public PlayerHealth Health { get; private set; }
	public PlayerInventory Inventory { get; private set; }
	public PlayerBuildResources BuildResources { get; private set; }

	public static FloodPlayer Local { get; private set; }

	protected override void OnStart()
	{
		CacheComponents();
		ValidateRequiredComponents();
		UpdateLocalPlayerReference();

		Log.Info( "FloodPlayer started." );
	}

	protected override void OnUpdate()
	{
		UpdateLocalPlayerReference();
	}

	public void PostCameraSetup( CameraComponent camera )
	{
		// Camera setup is handled by FloodPlayerCamera.
		// This is kept here because FloodPlayer implements PlayerController.IEvents.
	}

	private void CacheComponents()
	{
		Controller = Components.Get<PlayerController>();
		Health = Components.Get<PlayerHealth>();
		Inventory = Components.Get<PlayerInventory>();
		BuildResources = Components.Get<PlayerBuildResources>();
	}

	private void ValidateRequiredComponents()
	{
		if ( !Controller.IsValid() )
			Log.Warning( "FloodPlayer needs a PlayerController on the same GameObject." );

		if ( !Health.IsValid() )
			Log.Warning( "FloodPlayer needs PlayerHealth on the same GameObject." );

		if ( !Inventory.IsValid() )
			Log.Warning( "FloodPlayer needs PlayerInventory on the same GameObject." );

		if ( !BuildResources.IsValid() )
			Log.Warning( "FloodPlayer needs PlayerBuildResources on the same GameObject." );
	}

	private void UpdateLocalPlayerReference()
	{
		if ( IsProxy )
			return;

		Local = this;
	}
}
