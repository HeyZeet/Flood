using Sandbox;
using System.Collections.Generic;

public sealed class FloodPlayer : Component, PlayerController.IEvents
{
	private static readonly List<FloodPlayer> AllPlayers = new();

	public static IReadOnlyList<FloodPlayer> All => AllPlayers;
	public static FloodPlayer Local { get; private set; }

	public PlayerController Controller { get; private set; }
	public PlayerHealth Health { get; private set; }
	public PlayerInventory Inventory { get; private set; }
	public PlayerBuildResources BuildResources { get; private set; }

	protected override void OnStart()
	{
		if ( !AllPlayers.Contains( this ) )
			AllPlayers.Add( this );

		CacheComponents();
		ValidateRequiredComponents();
		UpdateLocalPlayerReference();

		Log.Info( "FloodPlayer started." );
	}

	public bool IsAlive
	{
		get
		{
			if ( !Health.IsValid() )
				return true;

			return Health.IsAlive;
		}
	}

	public bool IsDead => !IsAlive;

	public bool IsEliminated
	{
		get
		{
			if ( !Health.IsValid() )
				return false;

			return Health.IsEliminated;
		}
	}

	protected override void OnDestroy()
	{
		AllPlayers.Remove( this );

		if ( Local == this )
			Local = null;
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
		Controller = Components.Get<PlayerController>( FindMode.EverythingInSelfAndDescendants );
		Health = Components.Get<PlayerHealth>( FindMode.EverythingInSelfAndDescendants );
		Inventory = Components.Get<PlayerInventory>( FindMode.EverythingInSelfAndDescendants );
		BuildResources = Components.Get<PlayerBuildResources>( FindMode.EverythingInSelfAndDescendants );
	}

	private void ValidateRequiredComponents()
	{
		if ( !Controller.IsValid() )
			Log.Warning( "FloodPlayer needs a PlayerController on itself or a child GameObject." );

		if ( !Health.IsValid() )
			Log.Warning( "FloodPlayer needs PlayerHealth on itself or a child GameObject." );

		if ( !Inventory.IsValid() )
			Log.Warning( "FloodPlayer needs PlayerInventory on itself or a child GameObject." );

		if ( !BuildResources.IsValid() )
			Log.Warning( "FloodPlayer needs PlayerBuildResources on itself or a child GameObject." );
	}

	private void UpdateLocalPlayerReference()
	{
		if ( IsProxy )
			return;

		Local = this;
	}
}
