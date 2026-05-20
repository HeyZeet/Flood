using Sandbox;

public sealed class ShotgunWeapon : BaseGunWeapon
{
	public override string DisplayName => "USP";

    public override void OnDeploy()
    {
	    base.OnDeploy();

	    Log.Info( "USP deployed." );
    }

    public override void OnHolster()
    {
	    base.OnHolster();

	    Log.Info( "USP holstered." );
    }

	protected override void OnStart()
    {
	    base.OnStart();

	    Damage = 18f;
	    PrimaryFireRate = 0.18f;

	    BulletRange = 5000f;
	    BulletRadius = 1.5f;

	    ClipSize = 12;
	    AmmoInClip = ClipSize;
	    ReserveAmmo = 48;
	    InfiniteAmmo = false;
	    HasReserveAmmo = true;
	    ReloadTime = 1.4f;

	    HoldType = "pistol";
    }
}