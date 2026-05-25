using Sandbox;
using Sandbox.UI;

#pragma warning disable CS0618 // ScenePanel's current model-preview path still uses SceneWorld/Camera.
public sealed class BuildPieceModelPanel : ScenePanel
{
	public string ModelPath { get; set; } = "";

	private SceneModel PreviewModel { get; set; }
	private ScenePointLight FillLight { get; set; }
	private SceneDirectionalLight KeyLight { get; set; }
	private string CurrentModelPath { get; set; } = "";
	private float SpinYaw { get; set; }

	protected override void OnParametersSet()
	{
		base.OnParametersSet();

		if ( CurrentModelPath == ModelPath && World is not null )
			return;

		RebuildPreviewScene();
	}

	public override void Tick()
	{
		base.Tick();

		if ( PreviewModel is null )
			return;

		SpinYaw += Time.Delta * 32f;
		PreviewModel.Transform = new Transform( Vector3.Zero, Rotation.FromYaw( SpinYaw ) );
		UpdateCamera();
	}

	private void RebuildPreviewScene()
	{
		CurrentModelPath = ModelPath ?? "";
		World = new SceneWorld();
		World.AmbientLightColor = Color.White * 0.28f;

		PreviewModel = null;
		FillLight = null;
		KeyLight = null;

		if ( string.IsNullOrWhiteSpace( CurrentModelPath ) )
			return;

		PreviewModel = new SceneModel( World, CurrentModelPath, Transform.Zero );

		if ( PreviewModel is null || PreviewModel.Model.IsError )
			return;

		FillLight = new ScenePointLight( World, new Vector3( -80f, -110f, 95f ), 260f, Color.White * 1.35f );
		KeyLight = new SceneDirectionalLight( World, Rotation.From( 45f, 35f, 0f ), Color.White * 1.1f );

		Camera.FieldOfView = 42f;
		Camera.ZFar = 5000f;
		UpdateCamera();
	}

	private void UpdateCamera()
	{
		if ( PreviewModel is null || PreviewModel.Model.IsError )
			return;

		var bounds = PreviewModel.Bounds;
		var size = bounds.Size.Length.Clamp( 24f, 900f );
		var center = bounds.Center;
		var cameraDirection = new Vector3( -0.8f, -1.35f, 0.65f ).Normal;
		var distance = MathX.SphereCameraDistance( size * 0.5f, Camera.FieldOfView ) * 0.95f;

		Camera.Position = center - cameraDirection * distance;
		Camera.Rotation = Rotation.LookAt( cameraDirection, Vector3.Up );
	}
}
#pragma warning restore CS0618
