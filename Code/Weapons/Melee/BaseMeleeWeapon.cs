using Sandbox;

public abstract class BaseMeleeWeapon : BaseWeapon
{
	[Property] public float Range { get; set; } = 85f;
	[Property] public float TraceRadius { get; set; } = 8f;
	[Property] public bool DrawDebugTrace { get; set; } = true;

	public override void PrimaryAttack()
	{
		base.PrimaryAttack();

		if ( !Networking.IsHost )
		{
			RequestMeleeAttack( GetAttackStart(), GetAttackDirection() );
			return;
		}

		DoMeleeAttack();
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
		TryDamageHitObject( tr );
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
