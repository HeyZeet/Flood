using Sandbox;

public abstract class BaseWeapon : BaseCarryable
{
	[Property] public float Damage { get; set; } = 10f;
	[Property] public float PrimaryFireRate { get; set; } = 0.5f;
	[Property] public float SecondaryFireRate { get; set; } = 0.5f;

	private TimeSince TimeSincePrimaryAttack { get; set; }
	private TimeSince TimeSinceSecondaryAttack { get; set; }

	protected FloodPlayer OwnerPlayer
	{
		get
		{
			if ( !Inventory.IsValid() )
				return null;

			return Inventory.Components.Get<FloodPlayer>();
		}
	}

	protected PlayerController OwnerController
	{
		get
		{
			if ( !Inventory.IsValid() )
				return null;

			return Inventory.Components.Get<PlayerController>();
		}
	}

	public override void OnAddedToInventory( PlayerInventory inventory )
	{
		base.OnAddedToInventory( inventory );

		TimeSincePrimaryAttack = 999f;
		TimeSinceSecondaryAttack = 999f;
	}

	public override void OnPlayerUpdate()
	{
		if ( Input.Down( "attack1" ) )
			TryPrimaryAttack();

		if ( Input.Down( "attack2" ) )
			TrySecondaryAttack();
	}

	public void TryPrimaryAttack()
	{
		if ( !CanPrimaryAttack() )
			return;

		TimeSincePrimaryAttack = 0f;
		PrimaryAttack();
	}

	public void TrySecondaryAttack()
	{
		if ( !CanSecondaryAttack() )
			return;

		TimeSinceSecondaryAttack = 0f;
		SecondaryAttack();
	}

	public override bool CanPrimaryAttack()
	{
		if ( !base.CanPrimaryAttack() )
			return false;

		if ( PrimaryFireRate <= 0f )
			return true;

		return TimeSincePrimaryAttack >= PrimaryFireRate;
	}

	public override bool CanSecondaryAttack()
	{
		if ( !base.CanSecondaryAttack() )
			return false;

		if ( SecondaryFireRate <= 0f )
			return true;

		return TimeSinceSecondaryAttack >= SecondaryFireRate;
	}

	public override void PrimaryAttack()
	{
		Log.Info( $"{DisplayName} primary attack." );
	}

	public override void SecondaryAttack()
	{
		Log.Info( $"{DisplayName} secondary attack." );
	}
}