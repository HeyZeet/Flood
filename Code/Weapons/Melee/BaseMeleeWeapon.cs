using Sandbox;

public abstract class BaseMeleeWeapon : BaseWeapon
{
	[Property, Group( "Melee Trace" )] public float Range { get; set; } = 85f;
	[Property, Group( "Melee Trace" )] public float TraceRadius { get; set; } = 8f;
	[Property, Group( "Melee Trace" )] public bool DrawDebugTrace { get; set; } = true;

	[Property, Group( "Impact Effects" )] public GameObject DefaultImpactPrefab { get; set; }
	[Property, Group( "Impact Effects" )] public GameObject WoodImpactPrefab { get; set; }
	[Property, Group( "Impact Effects" )] public GameObject MetalImpactPrefab { get; set; }
	[Property, Group( "Impact Effects" )] public GameObject GlassImpactPrefab { get; set; }
	[Property, Group( "Impact Effects" )] public GameObject WaterImpactPrefab { get; set; }
	[Property, Group( "Impact Effects" )] public GameObject BrickImpactPrefab { get; set; }
	[Property, Group( "Impact Effects" )] public GameObject FleshImpactPrefab { get; set; }
	[Property, Group( "Impact Effects" )] public float ImpactLifeTime { get; set; } = 1.5f;

	public override void PrimaryAttack()
	{
		base.PrimaryAttack();

		if ( !Networking.IsHost )
		{
			RequestMeleeAttack( GetAttackStart(), GetAttackDirection() );
			return;
		}

		DoMeleeAttack();
		OnMeleeAttackApproved( true );
	}

	[Rpc.Host]
	private void RequestMeleeAttack( Vector3 start, Vector3 direction )
	{
		if ( !CanPrimaryAttack() )
			return;

		if ( !IsAimRequestReasonable( start, direction ) )
			return;

		ResetPrimaryAttackCooldown();
		DoMeleeAttack( start, direction );
		OnMeleeAttackApproved( true );
	}

	protected virtual void OnMeleeAttackApproved( bool skipLocalOwner )
	{
		if ( PlayAttackAnimation )
			BroadcastWeaponAnimation( AttackTrigger, skipLocalOwner );
	}

	protected virtual void DoMeleeAttack()
	{
		DoMeleeAttack( GetAttackStart(), GetAttackDirection() );
	}

	protected virtual void DoMeleeAttack( Vector3 start, Vector3 direction )
	{
		var owner = OwnerPlayer;

		if ( !owner.IsValid() )
		{
			Log.Warning( $"{DisplayName} has no valid owner player." );
			return;
		}

		var tr = TraceFromAim( start, direction, Range, TraceRadius );

		if ( DrawDebugTrace )
			DebugOverlay.Trace( tr, 1f );

		if ( !tr.Hit )
		{
			Log.Info( $"{DisplayName} missed." );
			return;
		}

		Log.Info( $"{DisplayName} hit {tr.GameObject.Name}." );

		LogBuildPieceHit( tr );
		PlayImpactEffect( tr );
		BroadcastImpactEffect( tr );
		OnMeleeHit( tr );
		TryDamageHitObject( tr );
	}

	protected virtual void OnMeleeHit( SceneTraceResult trace )
	{
	}

	protected virtual Vector3 GetAttackStart()
	{
		return GetOwnerEyePosition();
	}

	protected virtual Vector3 GetAttackDirection()
	{
		return GetOwnerAimDirection();
	}

	protected virtual void TryDamageHitObject( SceneTraceResult trace )
	{
		var damageable = trace.GameObject.Components.Get<DamageableComponent>( FindMode.EverythingInSelfAndAncestors );

		if ( !damageable.IsValid() )
			return;

		var damageInfo = DamageInfo.FromWeapon( this, trace );

		damageable.TakeDamage( damageInfo );

		Log.Info( $"{DisplayName} damaged {trace.GameObject.Name} for {Damage}." );
	}

	protected virtual void PlayImpactEffect( SceneTraceResult trace )
	{
		var impactPrefab = GetImpactPrefab( trace );

		if ( !impactPrefab.IsValid() )
			return;

		PlayImpactEffect( impactPrefab, trace.HitPosition, trace.Normal, ImpactLifeTime );
	}

	private void BroadcastImpactEffect( SceneTraceResult trace )
	{
		if ( !Networking.IsHost )
			return;

		var impactPrefab = GetImpactPrefab( trace );

		if ( !impactPrefab.IsValid() )
			return;

		PlayImpactEffectBroadcast( impactPrefab, trace.HitPosition, trace.Normal, ImpactLifeTime );
	}

	[Rpc.Broadcast]
	private void PlayImpactEffectBroadcast( GameObject impactPrefab, Vector3 hitPosition, Vector3 hitNormal, float lifeTime )
	{
		if ( Networking.IsHost )
			return;

		PlayImpactEffect( impactPrefab, hitPosition, hitNormal, lifeTime );
	}

	private void PlayImpactEffect( GameObject impactPrefab, Vector3 hitPosition, Vector3 hitNormal, float lifeTime )
	{
		if ( !impactPrefab.IsValid() )
			return;

		var impact = impactPrefab.Clone();

		impact.WorldPosition = hitPosition;
		impact.WorldRotation = Rotation.LookAt( GetSafeImpactNormal( hitNormal ) );

		if ( lifeTime > 0f )
		{
			var destroyAfterTime = impact.Components.Create<DestroyAfterTime>();
			destroyAfterTime.LifeTime = lifeTime;
		}
	}

	protected virtual GameObject GetImpactPrefab( SceneTraceResult trace )
	{
		if ( HasTagInHierarchy( trace.GameObject, "water" ) && WaterImpactPrefab.IsValid() )
			return WaterImpactPrefab;

		if ( IsFleshHit( trace ) && FleshImpactPrefab.IsValid() )
			return FleshImpactPrefab;

		if ( HasTagInHierarchy( trace.GameObject, "glass" ) && GlassImpactPrefab.IsValid() )
			return GlassImpactPrefab;

		if ( HasTagInHierarchy( trace.GameObject, "metal" ) && MetalImpactPrefab.IsValid() )
			return MetalImpactPrefab;

		if ( HasTagInHierarchy( trace.GameObject, "wood" ) && WoodImpactPrefab.IsValid() )
			return WoodImpactPrefab;

		if ( HasTagInHierarchy( trace.GameObject, "brick" ) && BrickImpactPrefab.IsValid() )
			return BrickImpactPrefab;

		if ( HasTagInHierarchy( trace.GameObject, "concrete" ) && BrickImpactPrefab.IsValid() )
			return BrickImpactPrefab;

		return DefaultImpactPrefab;
	}

	private Vector3 GetSafeImpactNormal( Vector3 hitNormal )
	{
		if ( hitNormal.Length > 0.01f )
			return hitNormal.Normal;

		return Vector3.Up;
	}

	private bool HasTagInHierarchy( GameObject gameObject, string tag )
	{
		var current = gameObject;

		while ( current.IsValid() )
		{
			if ( current.Tags.Has( tag ) )
				return true;

			current = current.Parent;
		}

		return false;
	}

	private bool IsFleshHit( SceneTraceResult trace )
	{
		if ( trace.GameObject.Components.Get<FloodPlayer>( FindMode.EverythingInSelfAndAncestors ).IsValid() )
			return true;

		if ( trace.GameObject.Components.Get<PlayerHealth>( FindMode.EverythingInSelfAndAncestors ).IsValid() )
			return true;

		if ( HasTagInHierarchy( trace.GameObject, "player" ) )
			return true;

		if ( HasTagInHierarchy( trace.GameObject, "flesh" ) )
			return true;

		return false;
	}

	private bool IsAimRequestReasonable( Vector3 start, Vector3 direction )
	{
		var owner = OwnerPlayer;

		if ( !owner.IsValid() )
			return false;

		if ( direction.Length < 0.1f )
			return false;

		var maxEyeDistance = 160f;
		return (start - owner.WorldPosition).Length <= maxEyeDistance;
	}

	private void LogBuildPieceHit( SceneTraceResult trace )
	{
		var buildPiece = trace.GameObject.Components.Get<BuildPiece>( FindMode.EverythingInSelfAndAncestors );

		if ( !buildPiece.IsValid() )
			return;

		Log.Info( $"Hit build piece: {buildPiece.DisplayName}, Material: {buildPiece.Material}, Cost: {buildPiece.Cost}" );
	}
}
