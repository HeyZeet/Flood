using Sandbox;
using System;

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

		var position = GetPlacementPosition( camera, ignoreObject, pieceData, fallbackPosition, fallbackRotation );
		var rotation = GetPlacementRotation( camera, fallbackRotation );

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
			return GetPlacementFromPlayerCamera( camera, ignoreObject, pieceData );

		var sceneCamera = Scene.Camera;

		if ( sceneCamera.IsValid() )
			return GetPlacementFromSceneCamera( sceneCamera, ignoreObject, pieceData );

		return fallbackPosition + fallbackRotation.Forward * BuildDistance;
	}

	private Vector3 GetPlacementFromPlayerCamera(
		FloodPlayerCamera camera,
		GameObject ignoreObject,
		BuildPieceData pieceData )
	{
		if ( UseSurfacePlacement )
		{
			var tr = camera.TraceAim( MaxBuildDistance );

			if ( tr.Hit )
				return tr.HitPosition + tr.Normal * GetPlacementSurfaceOffset( pieceData );
		}

		return camera.GetPointInFront( BuildDistance );
	}

	private Vector3 GetPlacementFromSceneCamera(
		CameraComponent sceneCamera,
		GameObject ignoreObject,
		BuildPieceData pieceData )
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
				return tr.HitPosition + tr.Normal * GetPlacementSurfaceOffset( pieceData );
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

		var bounds = pieceData.PlacementBounds;

		bounds.x = MathF.Max( bounds.x - pieceData.OverlapPadding, 1f );
		bounds.y = MathF.Max( bounds.y - pieceData.OverlapPadding, 1f );
		bounds.z = MathF.Max( bounds.z - pieceData.OverlapPadding, 1f );

		var halfExtents = bounds * 0.5f;

		var tr = Scene.Trace
			.Box( new BBox( -halfExtents, halfExtents ), position, position + Vector3.Up * 0.1f )
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

	private float GetPlacementSurfaceOffset( BuildPieceData pieceData )
	{
		if ( pieceData is not null )
			return pieceData.PlacementSurfaceOffset;

		return 0f;
	}
}
