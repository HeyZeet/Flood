using Sandbox;

public sealed class UspWeapon : BaseGunWeapon
{
	public override string DisplayName => "USP";

	protected override void OnStart()
	{
		base.OnStart();

		Damage = 18f;
		PrimaryFireRate = 0.18f;

		BulletRange = 5000f;
		BulletRadius = 1.5f;

		ClipSize = 12;
		AmmoInClip = ClipSize;
		InfiniteAmmo = true;

		HoldType = "pistol";
	}
}