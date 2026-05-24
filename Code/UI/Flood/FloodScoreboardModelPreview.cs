using Sandbox;
using System.Linq;

public sealed class FloodScoreboardModelPreview : Component
{
	private static FloodScoreboardModelPreview ActiveLocalPreview { get; set; }

	[Property] public string ScoreboardInputAction { get; set; } = "Score";
	[Property] public float PreviewDistance { get; set; } = 155f;
	[Property] public float PreviewRightOffset { get; set; } = 42f;
	[Property] public float PreviewUpOffset { get; set; } = -8f;
	[Property] public float ModelScale { get; set; } = 0.9f;
	[Property] public float RotationSpeed { get; set; } = 35f;

	private GameObject PreviewObject { get; set; }
	private SkinnedModelRenderer PreviewRenderer { get; set; }
	private FloodPlayer CurrentPreviewPlayer { get; set; }
	private Model CurrentModel { get; set; }
	private float SpinYaw { get; set; }

	protected override void OnUpdate()
	{
		var localPlayer = FloodPlayer.Local;

		if ( !localPlayer.IsValid() || !IsOwnedByLocalPlayer( localPlayer ) || !ClaimLocalPreview() )
		{
			DestroyPreview();
			return;
		}

		var targetPlayer = GetPreviewPlayer();

		if ( !targetPlayer.IsValid() )
		{
			DestroyPreview();
			return;
		}

		var camera = localPlayer.Components.Get<CameraComponent>( FindMode.EverythingInSelfAndDescendants );

		if ( !camera.IsValid() || !camera.Enabled )
		{
			DestroyPreview();
			return;
		}

		UpdatePreviewModel( targetPlayer );
		UpdatePreviewTransform( camera );
	}

	protected override void OnDestroy()
	{
		if ( ActiveLocalPreview == this )
			ActiveLocalPreview = null;

		DestroyPreview();
	}

	private bool IsOwnedByLocalPlayer( FloodPlayer localPlayer )
	{
		var owner = Components.Get<FloodPlayer>( FindMode.EverythingInSelfAndAncestors );

		if ( !owner.IsValid() )
			return false;

		return owner == localPlayer && owner.IsLocalPlayer;
	}

	private bool ClaimLocalPreview()
	{
		if ( !ActiveLocalPreview.IsValid() )
		{
			ActiveLocalPreview = this;
			return true;
		}

		return ActiveLocalPreview == this;
	}

	private FloodPlayer GetPreviewPlayer()
	{
		var roundManager = FloodGameManager.Instance;

		if ( roundManager.IsValid() && roundManager.IsRoundEndPhase() )
			return FindWinnerPlayer( roundManager );

		if ( IsScoreboardHeld() )
			return GetLeadingPlayer();

		return null;
	}

	private FloodPlayer FindWinnerPlayer( FloodGameManager roundManager )
	{
		if ( roundManager.RoundWinnerConnectionId == System.Guid.Empty )
			return null;

		return FloodPlayer.All
			.Where( player => player.IsValid() )
			.FirstOrDefault( player => player.PlayerConnectionId == roundManager.RoundWinnerConnectionId );
	}

	private FloodPlayer GetLeadingPlayer()
	{
		return FloodPlayer.All
			.Where( player => player.IsValid() )
			.OrderByDescending( player => player.Kills )
			.ThenBy( player => player.Deaths )
			.FirstOrDefault();
	}

	private bool IsScoreboardHeld()
	{
		if ( string.IsNullOrWhiteSpace( ScoreboardInputAction ) )
			return false;

		return Input.Down( ScoreboardInputAction );
	}

	private void UpdatePreviewModel( FloodPlayer targetPlayer )
	{
		var sourceRenderer = targetPlayer.Components.GetAll<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants )
			.FirstOrDefault( renderer => renderer.IsValid() && renderer.Model is not null );

		if ( !sourceRenderer.IsValid() || sourceRenderer.Model is null )
		{
			DestroyPreview();
			return;
		}

		EnsurePreviewObject();

		if ( !PreviewRenderer.IsValid() )
			return;

		if ( CurrentPreviewPlayer == targetPlayer && CurrentModel == sourceRenderer.Model )
			return;

		CurrentPreviewPlayer = targetPlayer;
		CurrentModel = sourceRenderer.Model;
		PreviewRenderer.Model = sourceRenderer.Model;
		PreviewRenderer.Tint = Color.White;
	}

	private void EnsurePreviewObject()
	{
		if ( PreviewObject.IsValid() && PreviewRenderer.IsValid() )
			return;

		PreviewObject = new GameObject( true, "Scoreboard Player Preview" );
		PreviewObject.NetworkMode = NetworkMode.Never;
		PreviewObject.Tags.Add( "scoreboard_preview" );

		PreviewRenderer = PreviewObject.Components.Create<SkinnedModelRenderer>();
	}

	private void UpdatePreviewTransform( CameraComponent camera )
	{
		if ( !PreviewObject.IsValid() )
			return;

		SpinYaw += RotationSpeed * Time.Delta;

		var rotation = camera.WorldRotation;
		var position =
			camera.WorldPosition +
			rotation.Forward * PreviewDistance +
			rotation.Right * PreviewRightOffset +
			rotation.Up * PreviewUpOffset;

		var faceCamera = Rotation.LookAt( -rotation.Forward, Vector3.Up );

		PreviewObject.Enabled = true;
		PreviewObject.WorldPosition = position;
		PreviewObject.WorldRotation = faceCamera * Rotation.FromYaw( SpinYaw );
		PreviewObject.WorldScale = Vector3.One * ModelScale;
	}

	private void DestroyPreview()
	{
		if ( PreviewObject.IsValid() )
			PreviewObject.Destroy();

		PreviewObject = null;
		PreviewRenderer = null;
		CurrentPreviewPlayer = null;
		CurrentModel = null;
	}
}
