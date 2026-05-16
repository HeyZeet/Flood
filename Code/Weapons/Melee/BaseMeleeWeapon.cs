using Sandbox;

public abstract class BaseMeleeWeapon : BaseWeapon
{
	[Property] public float Range { get; set; } = 85f;
	[Property] public float TraceRadius { get; set; } = 8f;
	[Property] public bool DrawDebugTrace { get; set; } = true;

	public override void PrimaryAttack()
	{
		base.PrimaryAttack();

		DoMeleeAttack();
	}

	protected virtual void DoMeleeAttack()
    {
	    var owner = OwnerPlayer;

	    if ( !owner.IsValid() )
	    {
		    Log.Warning( $"{DisplayName} has no valid owner player." );
		    return;
	    }

	    var start = GetAttackStart();
	    var end = start + GetAttackDirection() * Range;

	    var tr = Scene.Trace
		    .Sphere( TraceRadius, start, end )
		    .IgnoreGameObjectHierarchy( owner.GameObject )
		    .Run();

	    if ( DrawDebugTrace )
	    {
		    DebugOverlay.Trace( tr, 1f );
	    }

	    if ( !tr.Hit )
	    {
		    Log.Info( $"{DisplayName} missed." );
		    return;
	    }

	    Log.Info( $"{DisplayName} hit {tr.GameObject.Name}." );

        var buildPiece = tr.GameObject.Components.Get<BuildPiece>( FindMode.EverythingInSelfAndAncestors );

        if ( buildPiece.IsValid() )
        {
	        Log.Info( $"Hit build piece: {buildPiece.DisplayName}, Material: {buildPiece.Material}, Cost: {buildPiece.Cost}" );
        }

        var damageable = tr.GameObject.Components.Get<DamageableComponent>( FindMode.EverythingInSelfAndAncestors );

        if ( damageable.IsValid() )
        {
	        var damageInfo = DamageInfo.FromWeapon( this, tr );
	        damageable.TakeDamage( damageInfo );

	        Log.Info( $"{DisplayName} damaged {tr.GameObject.Name} for {Damage}." );
        }
    }

	protected virtual Vector3 GetAttackStart()
	{
		var camera = OwnerPlayer?.Components.Get<FloodPlayerCamera>();

		if ( camera.IsValid() )
			return camera.EyePosition;

		var sceneCamera = Scene.Camera;

		if ( sceneCamera.IsValid() )
		return sceneCamera.WorldPosition;

		return WorldPosition;
	}


	protected virtual Vector3 GetAttackDirection()
	{
		var camera = OwnerPlayer?.Components.Get<FloodPlayerCamera>();

		if ( camera.IsValid() )
			return camera.AimForward;

		var sceneCamera = Scene.Camera;

		if ( sceneCamera.IsValid() )
			return sceneCamera.WorldRotation.Forward;

		return WorldRotation.Forward;
	}
}
