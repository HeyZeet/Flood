using Sandbox;

public sealed class WaterDamageVolume : Component
{
	[Header( "Damage" )]
	[Property] public float DamagePerSecond { get; set; } = 10f;
	[Property] public float DamageStartDepth { get; set; } = 8f;

	[Header( "Debug" )]
	[Property] public bool LogDebug { get; set; } = false;

	protected override void OnUpdate()
	{
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

			if ( depth < DamageStartDepth )
				continue;

			var damage = DamagePerSecond * Time.Delta;

			player.Health.TakeDebugDamage( damage );

			if ( LogDebug )
				Log.Info( $"Water damaged {player.GameObject.Name} for {damage:0.00}. Depth: {depth:0.00}" );
		}
	}
}