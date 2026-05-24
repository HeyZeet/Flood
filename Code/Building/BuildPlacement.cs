using Sandbox;
using System;
using System.Collections.Generic;

public sealed class BuildPlacement : Component
{
	[Property] public bool UseGridSnapping { get; set; } = true;
	[Property] public bool CheckOverlap { get; set; } = true;
	[Property] public float GridSize { get; set; } = 25f;

	[Property] public bool UseSurfacePlacement { get; set; } = true;
	[Property] public float MaxBuildDistance { get; set; } = 350f;
	[Property] public float BuildDistance { get; set; } = 150f;

	[Property, Group( "Build Area" )]
	public bool RequireBuildArea { get; set; } = true;

	[Property] public bool DrawDebugPlacement { get; set; } = true;

	public Rotation PlacementRotationOffset { get; private set; } = Rotation.Identity;

	public BuildPlacementResult GetPlacementResult(
		FloodPlayerCamera camera,
		GameObject ignoreObject,
		BuildPieceData pieceData,
		Vector3 fallbackPosition,
		Rotation fallbackRotation )
	{
		if ( pieceData is null )
			return BuildPlacementResult.Invalid( fallbackPosition, fallbackRotation, "No selected piece." );

		var rotation = GetPlacementRotation( camera, fallbackRotation );
		var position = GetPlacementPosition( camera, ignoreObject, pieceData, fallbackPosition, rotation );

		if ( CheckOverlap && IsOverlappingBlockedObject( position, rotation, pieceData, ignoreObject ) )
			return BuildPlacementResult.Invalid( position, rotation, "Blocked by another object." );

		if ( !IsInsideAllowedBuildArea( position ) )
			return BuildPlacementResult.Invalid( position, rotation, "Outside build area." );

		return BuildPlacementResult.Valid( position, rotation );
	}

	public BuildPlacementResult ValidateRequestedPlacement(
		Vector3 position,
		Rotation rotation,
		GameObject ignoreObject,
		BuildPieceData pieceData,
		Vector3 builderPosition )
	{
		if ( pieceData is null )
			return BuildPlacementResult.Invalid( position, rotation, "No selected piece." );

		var distance = (builderPosition - position).Length;
		var maxDistance = MaxBuildDistance + MathF.Max( GridSize, 0f );

		if ( distance > maxDistance )
			return BuildPlacementResult.Invalid( position, rotation, "Too far away." );

		if ( CheckOverlap && IsOverlappingBlockedObject( position, rotation, pieceData, ignoreObject ) )
			return BuildPlacementResult.Invalid( position, rotation, "Blocked by another object." );

		if ( !IsInsideAllowedBuildArea( position ) )
			return BuildPlacementResult.Invalid( position, rotation, "Outside build area." );

		return BuildPlacementResult.Valid( position, rotation );
	}

	public Vector3 GetPlacementPosition(
		FloodPlayerCamera camera,
		GameObject ignoreObject,
		BuildPieceData pieceData,
		Vector3 fallbackPosition,
		Rotation fallbackRotation )
	{
		var rawPosition = GetRawPlacementPosition( camera, ignoreObject, pieceData, fallbackPosition, fallbackRotation );
		return SnapPositionToGrid( rawPosition );
	}

	public Rotation GetPlacementRotation( FloodPlayerCamera camera, Rotation fallbackRotation )
	{
		if ( camera.IsValid() )
		{
			var angles = camera.EyeRotation.Angles();
			var yawOnly = Rotation.FromYaw( angles.yaw );

			return yawOnly * PlacementRotationOffset;
		}

		var sceneCamera = Scene.Camera;

		if ( sceneCamera.IsValid() )
		{
			var angles = sceneCamera.WorldRotation.Angles();
			var yawOnly = Rotation.FromYaw( angles.yaw );

			return yawOnly * PlacementRotationOffset;
		}

		return fallbackRotation * PlacementRotationOffset;
	}

	public void RotatePlacement()
	{
		PlacementRotationOffset *= Rotation.FromYaw( 90f );
		Log.Info( "Rotated placement." );
	}

	public void DrawDebug( BuildPlacementResult result, FloodPlayerCamera camera )
	{
		if ( !DrawDebugPlacement )
			return;

		var color = result.IsValid ? Color.Green : Color.Red;

		DebugOverlay.Sphere( new Sphere( result.Position, 8f ), color, 0f );
	}

	private Vector3 GetRawPlacementPosition(
		FloodPlayerCamera camera,
		GameObject ignoreObject,
		BuildPieceData pieceData,
		Vector3 fallbackPosition,
		Rotation fallbackRotation )
	{
		if ( camera.IsValid() )
			return GetPlacementFromPlayerCamera( camera, ignoreObject, pieceData, fallbackRotation );

		var sceneCamera = Scene.Camera;

		if ( sceneCamera.IsValid() )
			return GetPlacementFromSceneCamera( sceneCamera, ignoreObject, pieceData, fallbackRotation );

		return fallbackPosition + fallbackRotation.Forward * BuildDistance;
	}

	private Vector3 GetPlacementFromPlayerCamera(
		FloodPlayerCamera camera,
		GameObject ignoreObject,
		BuildPieceData pieceData,
		Rotation placementRotation )
	{
		if ( UseSurfacePlacement )
		{
			var tr = camera.TraceAim( MaxBuildDistance );

			if ( tr.Hit )
				return tr.HitPosition + tr.Normal * GetPlacementSurfaceOffset( pieceData, tr.Normal, placementRotation );
		}

		return camera.GetPointInFront( BuildDistance );
	}

	private Vector3 GetPlacementFromSceneCamera(
		CameraComponent sceneCamera,
		GameObject ignoreObject,
		BuildPieceData pieceData,
		Rotation placementRotation )
	{
		if ( UseSurfacePlacement )
		{
			var start = sceneCamera.WorldPosition;
			var end = start + sceneCamera.WorldRotation.Forward * MaxBuildDistance;

			var trace = Scene.Trace.Ray( start, end );

			if ( ignoreObject.IsValid() )
				trace = trace.IgnoreGameObjectHierarchy( ignoreObject );

			var tr = trace.Run();

			if ( tr.Hit )
				return tr.HitPosition + tr.Normal * GetPlacementSurfaceOffset( pieceData, tr.Normal, placementRotation );
		}

		return sceneCamera.WorldPosition + sceneCamera.WorldRotation.Forward * BuildDistance;
	}

	private Vector3 SnapPositionToGrid( Vector3 position )
	{
		if ( !UseGridSnapping )
			return position;

		if ( GridSize <= 0f )
			return position;

		return new Vector3(
			MathF.Round( position.x / GridSize ) * GridSize,
			MathF.Round( position.y / GridSize ) * GridSize,
			MathF.Round( position.z / GridSize ) * GridSize
		);
	}

	private bool IsOverlappingBlockedObject(
		Vector3 position,
		Rotation rotation,
		BuildPieceData pieceData,
		GameObject ignoreObject )
	{
		if ( pieceData is null )
			return true;

		var localBounds = GetPlacementBounds( pieceData );
		localBounds = ShrinkBounds( localBounds, pieceData.OverlapPadding );

		var traceBounds = GetWorldAlignedBounds( localBounds, rotation );

		var tr = Scene.Trace
			.Box( traceBounds, position, position + Vector3.Up * 0.1f )
			.WithoutTags( "trigger" )
			.IgnoreGameObjectHierarchy( ignoreObject )
			.Run();

		return tr.Hit;
	}

	private bool IsInsideAllowedBuildArea( Vector3 position )
	{
		if ( !RequireBuildArea )
			return true;

		if ( !BuildAreaVolume.HasAnyBuildAreas() )
			return true;

		return BuildAreaVolume.IsInsideAnyBuildArea( position );
	}

	private float GetPlacementSurfaceOffset( BuildPieceData pieceData, Vector3 surfaceNormal, Rotation rotation )
	{
		if ( pieceData is null )
			return 0f;

		if ( !pieceData.UseModelBoundsForPlacement || !pieceData.PropModel.IsValid() )
			return pieceData.PlacementSurfaceOffset;

		var localBounds = GetPlacementBounds( pieceData );
		var maxDistanceBehindSurface = 0f;

		foreach ( var corner in GetBoxCorners( localBounds ) )
		{
			var rotatedCorner = rotation * corner;
			var distanceBehindSurface = -Vector3.Dot( rotatedCorner, surfaceNormal );

			if ( distanceBehindSurface > maxDistanceBehindSurface )
				maxDistanceBehindSurface = distanceBehindSurface;
		}

		return MathF.Max( maxDistanceBehindSurface, 0f );
	}

	private BBox GetPlacementBounds( BuildPieceData pieceData )
	{
		if ( pieceData is null )
			return BBox.FromPositionAndSize( Vector3.Zero, 1f );

		if ( pieceData.UseModelBoundsForPlacement && pieceData.PropModel.IsValid() )
		{
			var bounds = pieceData.PropModel.Bounds;
			var scale = pieceData.ModelScale.Clamp( 0.05f, 10f );

			bounds.Mins *= scale;
			bounds.Maxs *= scale;

			if ( bounds.Size.Length > 1f )
				return bounds;
		}

		var fallbackSize = new Vector3(
			MathF.Max( pieceData.PlacementBounds.x, 1f ),
			MathF.Max( pieceData.PlacementBounds.y, 1f ),
			MathF.Max( pieceData.PlacementBounds.z, 1f )
		);

		return BBox.FromPositionAndSize( Vector3.Zero, fallbackSize );
	}

	private BBox ShrinkBounds( BBox bounds, float padding )
	{
		if ( padding <= 0f )
			return bounds;

		var shrink = new Vector3( padding, padding, padding ) * 0.5f;
		var min = bounds.Mins + shrink;
		var max = bounds.Maxs - shrink;

		if ( min.x >= max.x || min.y >= max.y || min.z >= max.z )
			return bounds;

		return new BBox( min, max );
	}

	private BBox GetWorldAlignedBounds( BBox localBounds, Rotation rotation )
	{
		var first = true;
		var mins = Vector3.Zero;
		var maxs = Vector3.Zero;

		foreach ( var corner in GetBoxCorners( localBounds ) )
		{
			var rotatedCorner = rotation * corner;

			if ( first )
			{
				mins = rotatedCorner;
				maxs = rotatedCorner;
				first = false;
				continue;
			}

			mins = Vector3.Min( mins, rotatedCorner );
			maxs = Vector3.Max( maxs, rotatedCorner );
		}

		return new BBox( mins, maxs );
	}

	private IEnumerable<Vector3> GetBoxCorners( BBox bounds )
	{
		yield return new Vector3( bounds.Mins.x, bounds.Mins.y, bounds.Mins.z );
		yield return new Vector3( bounds.Mins.x, bounds.Mins.y, bounds.Maxs.z );
		yield return new Vector3( bounds.Mins.x, bounds.Maxs.y, bounds.Mins.z );
		yield return new Vector3( bounds.Mins.x, bounds.Maxs.y, bounds.Maxs.z );
		yield return new Vector3( bounds.Maxs.x, bounds.Mins.y, bounds.Mins.z );
		yield return new Vector3( bounds.Maxs.x, bounds.Mins.y, bounds.Maxs.z );
		yield return new Vector3( bounds.Maxs.x, bounds.Maxs.y, bounds.Mins.z );
		yield return new Vector3( bounds.Maxs.x, bounds.Maxs.y, bounds.Maxs.z );

	}
}
