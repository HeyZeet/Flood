using Sandbox;
using Sandbox.Movement;

public sealed class FloodPlayerWaterSensor : Component
{
	[Property] public bool UseFloodWaterFallback { get; set; } = true;
	[Property] public float DefaultBodyHeight { get; set; } = 72f;
	[Property] public float FeetOffset { get; set; } = 0f;
	[Property] public float TriggerRadius { get; set; } = 48f;
	[Property] public string WaterTag { get; set; } = "water";
	[Property] public bool LogDebug { get; set; } = false;

	private FloodPlayer Player { get; set; }
	private PlayerController Controller { get; set; }
	private MoveModeSwim SwimMode { get; set; }
	private GameObject LocalWaterTriggerObject { get; set; }
	private BoxCollider LocalWaterTriggerCollider { get; set; }
	private float LastWaterLevel { get; set; } = -1f;

	protected override void OnStart()
	{
		CacheComponents();
	}

	protected override void OnUpdate()
	{
		if ( !UseFloodWaterFallback )
			return;

		if ( !ShouldUpdateLocalMovement() )
			return;

		if ( !SwimMode.IsValid() )
			CacheComponents();

		if ( !SwimMode.IsValid() )
			return;

		var waterLevel = GetFloodWaterLevel();
		UpdateLocalWaterTrigger( waterLevel );

		if ( LogDebug && !LastWaterLevel.AlmostEqual( waterLevel, 0.05f ) )
		{
			LastWaterLevel = waterLevel;
			Log.Info( $"{GameObject.Name} flood swim water level: {waterLevel:0.00}, move mode water level: {SwimMode.WaterLevel:0.00}" );
		}
	}

	protected override void OnDestroy()
	{
		if ( LocalWaterTriggerObject.IsValid() )
			LocalWaterTriggerObject.Destroy();
	}

	private void CacheComponents()
	{
		Player = Components.Get<FloodPlayer>( FindMode.EverythingInSelfAndAncestors );
		Controller = Components.Get<PlayerController>( FindMode.EverythingInSelfAndAncestors );
		SwimMode = Components.Get<MoveModeSwim>( FindMode.EverythingInSelfAndDescendants );
	}

	private bool ShouldUpdateLocalMovement()
	{
		if ( !Player.IsValid() )
			CacheComponents();

		if ( Player.IsValid() )
			return Player.IsLocalPlayer;

		return !IsProxy;
	}

	private float GetFloodWaterLevel()
	{
		var water = FloodWaterController.Instance;

		if ( !water.IsValid() )
			return 0f;

		var bodyHeight = GetBodyHeight();
		var feetZ = WorldPosition.z + FeetOffset;
		var submergedHeight = water.SurfaceHeight - feetZ;

		return (submergedHeight / bodyHeight).Clamp( 0f, 1f );
	}

	private void UpdateLocalWaterTrigger( float waterLevel )
	{
		EnsureLocalWaterTrigger();

		if ( !LocalWaterTriggerObject.IsValid() || !LocalWaterTriggerCollider.IsValid() )
			return;

		var water = FloodWaterController.Instance;

		if ( !water.IsValid() || waterLevel <= 0f )
		{
			LocalWaterTriggerObject.Enabled = false;
			return;
		}

		var bodyHeight = GetBodyHeight();
		var feetZ = WorldPosition.z + FeetOffset;
		var submergedHeight = (bodyHeight * waterLevel).Clamp( 1f, bodyHeight );
		var centerZ = feetZ + submergedHeight * 0.5f;

		LocalWaterTriggerObject.Enabled = true;
		LocalWaterTriggerObject.WorldPosition = new Vector3( WorldPosition.x, WorldPosition.y, centerZ );
		LocalWaterTriggerObject.WorldRotation = Rotation.Identity;
		LocalWaterTriggerCollider.Scale = new Vector3( TriggerRadius, TriggerRadius, submergedHeight );
	}

	private void EnsureLocalWaterTrigger()
	{
		if ( LocalWaterTriggerObject.IsValid() && LocalWaterTriggerCollider.IsValid() )
			return;

		LocalWaterTriggerObject = new GameObject( true, $"{GameObject.Name} Local Water Trigger" );
		LocalWaterTriggerObject.NetworkMode = NetworkMode.Never;
		LocalWaterTriggerObject.Tags.Add( WaterTag );

		LocalWaterTriggerCollider = LocalWaterTriggerObject.Components.Create<BoxCollider>();
		LocalWaterTriggerCollider.IsTrigger = true;
	}

	private float GetBodyHeight()
	{
		if ( Controller.IsValid() && Controller.BodyHeight > 0f )
			return Controller.BodyHeight;

		return DefaultBodyHeight.Clamp( 1f, 512f );
	}
}
