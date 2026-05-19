using Sandbox;

public sealed class FloodWaterController : Component
{
	public static FloodWaterController Instance { get; private set; }

	[Header( "Water Object" )]
	[Property] public GameObject WaterObject { get; set; }

	[Header( "Heights" )]
	[Property] public float StartHeight { get; set; } = 0f;
	[Property] public float MaxHeight { get; set; } = 512f;
	[Property] public float DrainHeight { get; set; } = -64f;

	[Header( "Speed" )]
	[Property] public float RiseSpeed { get; set; } = 16f;
	[Property] public float DrainSpeed { get; set; } = 48f;

	[Header( "State" )]
	[Property] public bool StartFlooded { get; set; } = false;

	public float WaterHeight { get; private set; }
	public bool IsRising { get; private set; }
	public bool IsDraining { get; private set; }

	protected override void OnStart()
	{
		Instance = this;

		WaterHeight = StartFlooded ? MaxHeight : StartHeight;
		SetWaterHeight( WaterHeight );
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnUpdate()
	{
		if ( IsRising )
			UpdateRising();

		if ( IsDraining )
			UpdateDraining();
	}

	public void StartFlood()
	{
		IsRising = true;
		IsDraining = false;
	}

	public void StopFlood()
	{
		IsRising = false;
	}

	public void StartDrain()
	{
		IsDraining = true;
		IsRising = false;
	}

	public void ResetWater()
	{
		IsRising = false;
		IsDraining = false;

		SetWaterHeight( StartHeight );
	}

	public bool IsUnderwater( Vector3 position )
	{
		return position.z < WaterHeight;
	}

	public float GetDepth( Vector3 position )
	{
		return WaterHeight - position.z;
	}

	private void UpdateRising()
	{
		var newHeight = WaterHeight + RiseSpeed * Time.Delta;

		if ( newHeight >= MaxHeight )
		{
			newHeight = MaxHeight;
			IsRising = false;
		}

		SetWaterHeight( newHeight );
	}

	private void UpdateDraining()
	{
		var newHeight = WaterHeight - DrainSpeed * Time.Delta;

		if ( newHeight <= DrainHeight )
		{
			newHeight = DrainHeight;
			IsDraining = false;
		}

		SetWaterHeight( newHeight );
	}

	private void SetWaterHeight( float height )
	{
		WaterHeight = height;

		if ( !WaterObject.IsValid() )
			return;

		var position = WaterObject.WorldPosition;
		position.z = WaterHeight;
		WaterObject.WorldPosition = position;
	}
}