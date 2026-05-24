using Sandbox;

public sealed class WaterDamageVolume : Component
{
	[Header( "Damage" )]
	[Property] public float DamagePerSecond { get; set; } = 10f;
	[Property, Range( 0f, 1f )] public float DamageStartSubmergedFraction { get; set; } = 0.5f;
	[Property] public float DefaultBodyHeight { get; set; } = 72f;

	[Header( "Debug" )]
	[Property] public bool LogDebug { get; set; } = false;

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		var water = FloodWaterController.Instance;

		if ( !water.IsValid() )
			return;

		foreach ( var player in FloodPlayer.All )
		{
			if ( !player.IsValid() )
				continue;

			if ( player.IsDead )
				continue;

			if ( !player.Health.IsValid() )
				continue;

			var depth = water.GetDepth( player.WorldPosition );
			var damageStartDepth = GetDamageStartDepth( player );

			if ( depth < damageStartDepth )
				continue;

			var damage = DamagePerSecond * Time.Delta;

			player.Health.TakeDebugDamage( damage );

			if ( LogDebug )
				Log.Info( $"Water damaged {player.GameObject.Name} for {damage:0.00}. Depth: {depth:0.00}/{damageStartDepth:0.00}" );
		}
	}

	private float GetDamageStartDepth( FloodPlayer player )
	{
		var bodyHeight = DefaultBodyHeight;
		var controller = player.Components.Get<PlayerController>( FindMode.EverythingInSelfAndDescendants );

		if ( controller.IsValid() && controller.BodyHeight > 0f )
			bodyHeight = controller.BodyHeight;

		return bodyHeight * DamageStartSubmergedFraction.Clamp( 0f, 1f );
	}
}
