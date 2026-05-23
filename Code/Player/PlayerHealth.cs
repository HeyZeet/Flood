using Sandbox;
using System;

public sealed class PlayerHealth : DamageableComponent
{
	public event Action<PlayerHealth> OnEliminated;

	[Property] public float MaxHealth { get; set; } = 100f;

	[Property, Group( "Death" )]
	public GameObject RagdollPrefab { get; set; }

	[Property, Group( "Death" )]
	public bool HidePlayerModelOnDeath { get; set; } = true;

	[Property, Group( "Death" )]
	public bool SpawnRagdollOnDeath { get; set; } = true;

	[Sync( SyncFlags.FromHost )] public float Health { get; private set; } = 100f;
	[Sync( SyncFlags.FromHost )] public bool IsEliminated { get; private set; }

	public override bool IsAlive => Health > 0f;
	public bool IsDead => !IsAlive;

	public GameObject DeathCameraTarget
	{
		get
		{
			if ( ActiveRagdoll.IsValid() )
				return ActiveRagdoll;

			return GameObject;
		}
	}

	private PlayerController Controller { get; set; }
	private GameObject ActiveRagdoll { get; set; }

	protected override void OnStart()
	{
		Controller = Components.Get<PlayerController>();

		if ( Health <= 0f )
			Health = MaxHealth;

		if ( Networking.IsHost )
			ResetHealth();
	}

	protected override void OnFixedUpdate()
	{
		if ( !IsDead )
			return;

		LockDeadPlayerMovement();
	}
	

	public void TakeDamage( float amount )
	{
		var damageInfo = new DamageInfo
		{
			Damage = amount,
			HitObject = GameObject,
			IsDebugDamage = false
		};

		TakeDamage( damageInfo );
	}

	public void TakeDebugDamage( float amount )
	{
		var damageInfo = DamageInfo.Debug( amount, GameObject );

		TakeDamage( damageInfo );
	}

	public override void TakeDamage( DamageInfo damageInfo )
	{
		if ( !Networking.IsHost )
			return;

		if ( IsDead )
			return;

		if ( !CanTakeGameplayDamage( damageInfo ) )
			return;

		var amount = damageInfo.Damage.Clamp( 0f, float.MaxValue );

		Health -= amount;
		Health = Health.Clamp( 0f, MaxHealth );

		Log.Info( $"{GameObject.Name} took {amount} damage. Health: {Health}" );

		if ( Health <= 0f )
			Die( damageInfo );
	}

	public void Heal( float amount )
	{
		if ( !Networking.IsHost )
			return;

		if ( IsDead )
			return;

		amount = amount.Clamp( 0f, float.MaxValue );

		Health += amount;
		Health = Health.Clamp( 0f, MaxHealth );
	}

	public void Kill()
	{
		if ( !Networking.IsHost )
			return;

		if ( IsDead )
			return;

		var damageInfo = new DamageInfo
		{
			Damage = Health,
			HitObject = GameObject
		};

		Health = 0f;
		Die( damageInfo );
	}

	public void Respawn()
	{
		if ( !Networking.IsHost )
			return;

		ResetHealth();
		ClearEliminated();
		ClearMovement();
		DestroyRagdoll();
		SetPlayerModelVisible( true );

		Log.Info( $"{GameObject.Name} respawned. Health: {Health}" );
	}

	public void ResetHealth()
	{
		if ( !Networking.IsHost )
			return;

		Health = MaxHealth;
	}

	private void Die( DamageInfo damageInfo )
	{
		MarkEliminated();
		ClearMovement();

		if ( HidePlayerModelOnDeath )
			SetPlayerModelVisible( false );

		if ( SpawnRagdollOnDeath )
			SpawnRagdoll( damageInfo );

		Log.Info( $"{GameObject.Name} died." );
	}

	private void MarkEliminated()
	{
		if ( IsEliminated )
			return;

		IsEliminated = true;
		OnEliminated?.Invoke( this );

		Log.Info( $"{GameObject.Name} eliminated from the round." );
	}

	private void ClearEliminated()
	{
		IsEliminated = false;
	}

	private void LockDeadPlayerMovement()
	{
		if ( !Controller.IsValid() )
			Controller = Components.Get<PlayerController>();

		if ( !Controller.IsValid() )
			return;

		Controller.WishVelocity = Vector3.Zero;
	}

	private void ClearMovement()
	{
		if ( !Controller.IsValid() )
			Controller = Components.Get<PlayerController>();

		if ( !Controller.IsValid() )
			return;

		Controller.WishVelocity = Vector3.Zero;
	}

	private void SpawnRagdoll( DamageInfo damageInfo )
	{
		if ( ActiveRagdoll.IsValid() )
			return;

		if ( !RagdollPrefab.IsValid() )
		{
			Log.Warning( $"{GameObject.Name} died but has no RagdollPrefab assigned." );
			return;
		}

		ActiveRagdoll = RagdollPrefab.Clone( WorldPosition, WorldRotation );
		ActiveRagdoll.Name = $"{GameObject.Name} Ragdoll";

		ActiveRagdoll.NetworkSpawn();

		ApplyRagdollForce( damageInfo );

		Log.Info( $"Spawned ragdoll for {GameObject.Name}." );
	}

	private void DestroyRagdoll()
	{
		if ( !ActiveRagdoll.IsValid() )
			return;

		ActiveRagdoll.Destroy();
		ActiveRagdoll = null;
	}

	private void ApplyRagdollForce( DamageInfo damageInfo )
	{
		if ( !ActiveRagdoll.IsValid() )
			return;

		var force = damageInfo.Force;

		if ( force.Length <= 0f )
			force = WorldRotation.Forward * 250f;

		foreach ( var rigidbody in ActiveRagdoll.Components.GetAll<Rigidbody>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !rigidbody.IsValid() )
				continue;

			rigidbody.ApplyImpulse( force );
		}
	}

	private void SetPlayerModelVisible( bool visible )
	{
		foreach ( var renderer in Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !renderer.IsValid() )
				continue;

			renderer.Enabled = visible;
		}
	}
}
