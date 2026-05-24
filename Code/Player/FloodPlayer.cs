using Sandbox;
using System;
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
	public bool IsLocalPlayer => GameObject.Network.IsOwner;

	[Sync( SyncFlags.FromHost | SyncFlags.Query )] public string PlayerName { get; private set; } = "Player";
	[Sync( SyncFlags.FromHost | SyncFlags.Query )] public Guid PlayerConnectionId { get; private set; }
	[Sync( SyncFlags.FromHost | SyncFlags.Query )] public int Kills { get; private set; }
	[Sync( SyncFlags.FromHost | SyncFlags.Query )] public int Deaths { get; private set; }

	[Sync( SyncFlags.FromHost | SyncFlags.Query ), Change( nameof( OnSyncedRoundPhaseChanged ) )]
	public GamePhase SyncedRoundPhase { get; private set; } = GamePhase.WaitingForPlayers;

	[Sync( SyncFlags.FromHost | SyncFlags.Query )] public int SyncedRoundSecondsRemaining { get; private set; }
	[Sync( SyncFlags.FromHost | SyncFlags.Query )] public float SyncedRoundDuration { get; private set; }

	public event Action<GamePhase, GamePhase> OnSyncedRoundPhaseUpdated;

	private bool? LastLocalPresentationState { get; set; }

	protected override void OnStart()
	{
		if ( !AllPlayers.Contains( this ) )
			AllPlayers.Add( this );

		CacheComponents();
		ValidateRequiredComponents();
		UpdateLocalPlayerReference();
		UpdateLocalPresentationState( true );

		if ( Networking.IsHost )
		{
			SetPlayerIdentityFromNetwork();
			FloodGameManager.Instance?.RegisterPlayer( this );
			Log.Info( $"FloodPlayer registered with round manager. Name={GameObject.Name}" );
		}

		Log.Info( $"FloodPlayer started. Host={Networking.IsHost}, Proxy={IsProxy}, Name={GameObject.Name}" );
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

	public bool IsRoundBuildPhase => SyncedRoundPhase == GamePhase.BuildPhase;
	public bool IsRoundCombatPhase => SyncedRoundPhase == GamePhase.CombatPhase;

	public void SetPlayerName( string playerName )
	{
		if ( !Networking.IsHost )
			return;

		PlayerName = string.IsNullOrWhiteSpace( playerName ) ? GameObject.Name : playerName;
	}

	public void SetPlayerIdentity( string playerName, Guid connectionId )
	{
		if ( !Networking.IsHost )
			return;

		SetPlayerName( playerName );
		PlayerConnectionId = connectionId;
	}

	public void RecordKill()
	{
		if ( !Networking.IsHost )
			return;

		Kills++;
	}

	public void RecordDeath()
	{
		if ( !Networking.IsHost )
			return;

		Deaths++;
	}

	public void SetSyncedRoundState( GamePhase phase, int secondsRemaining, float duration )
	{
		if ( !Networking.IsHost )
			return;

		SyncedRoundPhase = phase;
		SyncedRoundSecondsRemaining = secondsRemaining.Clamp( 0, int.MaxValue );
		SyncedRoundDuration = duration.Clamp( 0f, float.MaxValue );
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
		UpdateLocalPresentationState();
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
		if ( !IsLocalPlayer )
			return;

		Local = this;
	}

	private void SetPlayerIdentityFromNetwork()
	{
		SetPlayerName( GameObject.Name );

		if ( GameObject.Network.Active )
			PlayerConnectionId = GameObject.Network.OwnerId;
	}

	private void OnSyncedRoundPhaseChanged( GamePhase previousPhase, GamePhase newPhase )
	{
		if ( Networking.IsHost )
			return;

		Log.Info( $"{GameObject.Name} round state synced: {previousPhase} -> {newPhase} ({SyncedRoundSecondsRemaining}s)" );
		OnSyncedRoundPhaseUpdated?.Invoke( previousPhase, newPhase );
	}

	private void UpdateLocalPresentationState( bool force = false )
	{
		var isLocalPlayer = IsLocalPlayer;

		if ( !force && LastLocalPresentationState == isLocalPlayer )
			return;

		LastLocalPresentationState = isLocalPlayer;

		foreach ( var camera in Components.GetAll<CameraComponent>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !camera.IsValid() )
				continue;

			camera.Enabled = isLocalPlayer;
			camera.IsMainCamera = isLocalPlayer;
		}

		foreach ( var screenPanel in Components.GetAll<ScreenPanel>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !screenPanel.IsValid() )
				continue;

			screenPanel.Enabled = isLocalPlayer;
		}

		Log.Info( $"FloodPlayer presentation updated. Name={GameObject.Name}, Local={isLocalPlayer}, Proxy={IsProxy}" );
	}
}
