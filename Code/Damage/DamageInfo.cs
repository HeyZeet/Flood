using Sandbox;

public struct DamageInfo
{
	public float Damage { get; set; }

	public GameObject Attacker { get; set; }
	public GameObject Weapon { get; set; }
	public GameObject HitObject { get; set; }

	public Vector3 HitPosition { get; set; }
	public Vector3 HitNormal { get; set; }
	public Vector3 Force { get; set; }

	public static DamageInfo FromWeapon( BaseWeapon weapon, SceneTraceResult trace )
	{
		var owner = weapon.Inventory?.GameObject;

		return new DamageInfo
		{
			Damage = weapon.Damage,
			Attacker = owner,
			Weapon = weapon.GameObject,
			HitObject = trace.GameObject,
			HitPosition = trace.HitPosition,
			HitNormal = trace.Normal,
			Force = weapon.WorldRotation.Forward * weapon.Damage
		};
	}
}
