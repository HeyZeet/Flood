using Sandbox;

public sealed class BuildPreview : Component
{
	[Property] public bool DisablePhysicsOnPreview { get; set; } = true;
	[Property] public bool DisableDamageOnPreview { get; set; } = true;
	[Property] public bool DisableBuildPieceOnPreview { get; set; } = true;

	[Property] public Color ValidColor { get; set; } = new Color( 0f, 1f, 0f, 0.45f );
	[Property] public Color InvalidColor { get; set; } = new Color( 1f, 0f, 0f, 0.45f );

	private GameObject PreviewObject { get; set; }
	private BuildPieceData CurrentPieceData { get; set; }

	public bool HasPreview => PreviewObject.IsValid();

	public void UpdatePreview( BuildPieceData pieceData, BuildPlacementResult result )
	{
		if ( pieceData is null )
		{
			ClearPreview();
			return;
		}

		if ( CurrentPieceData != pieceData || !PreviewObject.IsValid() )
		{
			CreatePreview( pieceData, result.Position, result.Rotation );
		}

		if ( !PreviewObject.IsValid() )
			return;

		PreviewObject.WorldPosition = result.Position;
		PreviewObject.WorldRotation = result.Rotation;
		PreviewObject.Enabled = true;

		ApplyPreviewColor( result.IsValid ? ValidColor : InvalidColor );
	}

	public void ClearPreview()
	{
		if ( PreviewObject.IsValid() )
			PreviewObject.Destroy();

		PreviewObject = null;
		CurrentPieceData = null;
	}

	private void CreatePreview( BuildPieceData pieceData, Vector3 position, Rotation rotation )
	{
		ClearPreview();

		CurrentPieceData = pieceData;

		if ( !pieceData.Prefab.IsValid() )
		{
			Log.Warning( $"{pieceData.DisplayName} has no prefab assigned." );
			return;
		}

		PreviewObject = pieceData.Prefab.Clone( position, rotation );
		PreviewObject.Name = $"Preview - {pieceData.DisplayName}";

		PreparePreviewObject();

		PreviewObject.WorldPosition = position;
		PreviewObject.WorldRotation = rotation;
		PreviewObject.Enabled = true;

		ApplyPreviewColor( ValidColor );

		Log.Info( $"Created build preview: {pieceData.DisplayName}" );
	}

	private void PreparePreviewObject()
	{
		if ( !PreviewObject.IsValid() )
			return;

		if ( DisablePhysicsOnPreview )
		{
			foreach ( var rigidbody in PreviewObject.Components.GetAll<Rigidbody>( FindMode.EverythingInSelfAndDescendants ) )
			{
				rigidbody.Enabled = false;
			}

			foreach ( var collider in PreviewObject.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
			{
				collider.Enabled = false;
			}
		}

		if ( DisableDamageOnPreview )
		{
			foreach ( var damageable in PreviewObject.Components.GetAll<DamageableObject>( FindMode.EverythingInSelfAndDescendants ) )
			{
				damageable.Enabled = false;
			}
		}

		if ( DisableBuildPieceOnPreview )
		{
			foreach ( var buildPiece in PreviewObject.Components.GetAll<BuildPiece>( FindMode.EverythingInSelfAndDescendants ) )
			{
				buildPiece.Enabled = false;
			}
		}
	}

	private void ApplyPreviewColor( Color color )
	{
		if ( !PreviewObject.IsValid() )
			return;

		foreach ( var renderer in PreviewObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			renderer.Tint = color;
		}
	}
}
