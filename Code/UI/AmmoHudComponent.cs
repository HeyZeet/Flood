using Sandbox;

public sealed class AmmoHudComponent : Component
{
	public static AmmoHudComponent Local { get; private set; }

    protected override void OnStart()
    {
	    if ( IsProxy )
		    return;

	    Local = this;

	    Log.Info( "AmmoHudComponent Local set." );
    }

	protected override void OnDestroy()
	{
		if ( Local == this )
			Local = null;
	}

	public BaseGunWeapon ActiveGun
	{
		get
		{
			var player = FloodPlayer.Local;

			if ( !player.IsValid() )
				return null;

			var inventory = player.Inventory;

			if ( !inventory.IsValid() )
				return null;

			var activeCarryable = inventory.ActiveCarryable;

			if ( !activeCarryable.IsValid() )
				return null;

			return activeCarryable.Components.Get<BaseGunWeapon>( FindMode.EverythingInSelfAndDescendants );
		}
	}
}