using Sandbox;
using System.Collections.Generic;

public sealed class BuildAreaVolume : Component
{
	private static readonly List<BuildAreaVolume> BuildAreas = new();

	public static IReadOnlyList<BuildAreaVolume> All => BuildAreas;

	[Property] public Vector3 Size { get; set; } = new Vector3( 1024f, 1024f, 512f );
	public BBox WorldBounds
	{
		get
		{
			var halfSize = Size * 0.5f;
			return new BBox( WorldPosition - halfSize, WorldPosition + halfSize );
		}
	}

	protected override void OnStart()
	{
		if ( !BuildAreas.Contains( this ) )
			BuildAreas.Add( this );
	}

	protected override void OnDestroy()
	{
		BuildAreas.Remove( this );
	}

	public bool Contains( Vector3 position )
	{
		var bounds = WorldBounds;

		return
			position.x >= bounds.Mins.x &&
			position.y >= bounds.Mins.y &&
			position.z >= bounds.Mins.z &&
			position.x <= bounds.Maxs.x &&
			position.y <= bounds.Maxs.y &&
			position.z <= bounds.Maxs.z;
	}

	public static bool HasAnyBuildAreas()
	{
		foreach ( var area in BuildAreas )
		{
			if ( area.IsValid() )
				return true;
		}

		return false;
	}

	public static bool IsInsideAnyBuildArea( Vector3 position )
	{
		foreach ( var area in BuildAreas )
		{
			if ( !area.IsValid() )
				continue;

			if ( area.Contains( position ) )
				return true;
		}

		return false;
	}
}
