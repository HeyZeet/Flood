using Sandbox;

public abstract partial class BaseGunWeapon
{
	protected virtual void PlayFireEffects()
	{
		if ( FireSound is not null )
			Sound.Play( FireSound, WorldPosition );

		PlayMuzzleFlash();
	}

	protected virtual void PlayDryFireEffects()
	{
		if ( DryFireSound is not null )
			Sound.Play( DryFireSound, WorldPosition );

		ClearOneShotAnimationParams();
		PlayWeaponAnimation( DryAttackTrigger );
	}

	private void BroadcastFireEffects( bool skipLocalOwner = true )
	{
		if ( !Networking.IsHost )
			return;

		PlayFireEffectsBroadcast( skipLocalOwner );
	}

	[Rpc.Broadcast]
	private void PlayFireEffectsBroadcast( bool skipLocalOwner )
	{
		if ( skipLocalOwner && IsLocallyControlled() )
			return;

		PlayFireEffects();
	}

	private void BroadcastDryFireEffects( bool skipLocalOwner = true )
	{
		if ( !Networking.IsHost )
			return;

		PlayDryFireEffectsBroadcast( skipLocalOwner );
	}

	[Rpc.Broadcast]
	private void PlayDryFireEffectsBroadcast( bool skipLocalOwner )
	{
		if ( skipLocalOwner && IsLocallyControlled() )
			return;

		PlayDryFireEffects();
	}

	protected virtual void PlayReloadEffects()
	{
		if ( ReloadSound is not null )
			Sound.Play( ReloadSound, WorldPosition );

		ClearOneShotAnimationParams();
		PlayWeaponAnimation( ReloadTrigger );
	}

	private void BroadcastReloadEffects( bool skipLocalOwner = true )
	{
		if ( !Networking.IsHost )
			return;

		PlayReloadEffectsBroadcast( skipLocalOwner );
	}

	[Rpc.Broadcast]
	private void PlayReloadEffectsBroadcast( bool skipLocalOwner )
	{
		if ( Networking.IsHost && IsLocallyControlled() )
			return;

		if ( skipLocalOwner && IsLocallyControlled() )
			return;

		PlayReloadEffects();
	}

	private void UpdateReloadPresentation()
	{
		if ( WasReloadingLastFrame == IsReloading )
			return;

		WasReloadingLastFrame = IsReloading;

		if ( !IsReloading )
			return;

		if ( Networking.IsHost )
			return;
	}

	private void PlayFirstPersonMuzzleFlash()
	{
		if ( !ShouldShowViewModel() )
			return;

		var viewModel = Components.Get<ViewModelWeapon>( FindMode.EverythingInSelfAndDescendants );

		if ( !viewModel.IsValid() )
			return;

		if ( !viewModel.TryGetBoneTransform( MuzzleBoneName, out var muzzleTransform ) )
			return;

		SpawnMuzzleFlash( muzzleTransform );
	}

	private void PlayThirdPersonMuzzleFlash()
	{
		var thirdPersonModel = Components.Get<ThirdPersonWeaponModel>( FindMode.EverythingInSelfAndDescendants );

		if ( !thirdPersonModel.IsValid() )
			return;

		if ( thirdPersonModel.ShouldHideForLocalPlayer() )
			return;

		var muzzleTransform = thirdPersonModel.GetMuzzleTransform();

		SpawnMuzzleFlash( muzzleTransform );
	}

	private void SpawnMuzzleFlash( Transform muzzleTransform )
	{
		var flash = MuzzleFlashPrefab.Clone();

		flash.WorldPosition =
			muzzleTransform.Position +
			muzzleTransform.Rotation.Forward * MuzzleFlashPositionOffset.x +
			muzzleTransform.Rotation.Right * MuzzleFlashPositionOffset.y +
			muzzleTransform.Rotation.Up * MuzzleFlashPositionOffset.z;

		flash.WorldRotation = muzzleTransform.Rotation * MuzzleFlashRotationOffset.ToRotation();

		if ( MuzzleFlashLifeTime > 0f )
		{
			var destroyAfterTime = flash.Components.Create<DestroyAfterTime>();
			destroyAfterTime.LifeTime = MuzzleFlashLifeTime;
		}
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

	private Vector3 GetSafeImpactNormal( Vector3 hitNormal )
	{
		if ( hitNormal.Length > 0.01f )
			return hitNormal.Normal;

		return Vector3.Up;
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
}
