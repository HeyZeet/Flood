using Sandbox;

public abstract class BaseWeapon : BaseCarryable
{
	[Property] public float Damage { get; set; } = 10f;
	[Property] public float PrimaryFireRate { get; set; } = 0.5f;
	[Property] public float SecondaryFireRate { get; set; } = 0.5f;

	private TimeSince _timeSincePrimaryAttack;
	private TimeSince _timeSinceSecondaryAttack;

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

		_timeSincePrimaryAttack = 999f;
		_timeSinceSecondaryAttack = 999f;
	}

	public override void OnPlayerUpdate()
	{
		if ( Input.Pressed( "attack1" ) || Input.Down( "attack1" ) )
		{
			TryPrimaryAttack();
		}

		if ( Input.Pressed( "attack2" ) || Input.Down( "attack2" ) )
		{
			TrySecondaryAttack();
		}
	}

	public void TryPrimaryAttack()
	{
		if ( !CanPrimaryAttack() )
			return;

		_timeSincePrimaryAttack = 0f;

		PrimaryAttack();
	}

	public void TrySecondaryAttack()
	{
		if ( !CanSecondaryAttack() )
			return;

		_timeSinceSecondaryAttack = 0f;

		SecondaryAttack();
	}

	public override bool CanPrimaryAttack()
	{
		if ( !base.CanPrimaryAttack() )
			return false;

		if ( PrimaryFireRate <= 0f )
			return true;

		return _timeSincePrimaryAttack >= PrimaryFireRate;
	}

	public override bool CanSecondaryAttack()
	{
		if ( !base.CanSecondaryAttack() )
			return false;

		if ( SecondaryFireRate <= 0f )
			return true;

		return _timeSinceSecondaryAttack >= SecondaryFireRate;
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