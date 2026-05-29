using Sandbox;
using System.Collections.Generic;
using System.Linq;

public sealed partial class WeldToolWeapon
{
	private sealed class MultiWeldCandidate
	{
		public BuildPiece First { get; init; }
		public BuildPiece Second { get; init; }
		public float Distance { get; init; }
		public Vector3 ContactPoint { get; init; }
	}

	private bool TryWeldSelectedPiecesIntoSingleRaft( List<BuildPiece> selectedPieces, out int weldedCount )
	{
		weldedCount = 0;
		var anchorRoot = selectedPieces[0].GetWeldRoot();

		if ( !anchorRoot.IsValid() )
			return false;

		var remainingRoots = selectedPieces
			.Select( piece => piece.GetWeldRoot() )
			.Where( root => root.IsValid() && root != anchorRoot )
			.Distinct()
			.ToList();

		if ( remainingRoots.Count == 0 )
		{
			weldedCount = 1;
			return true;
		}

		while ( remainingRoots.Count > 0 )
		{
			var anchorPieces = anchorRoot.GetWeldedPieces();
			var bestCandidate = FindClosestRootCandidate( anchorPieces, remainingRoots );

			if ( bestCandidate is null )
			{
				Log.Warning( $"{DisplayName}: could not connect every selected piece into one raft. Remaining roots: {remainingRoots.Count}" );
				return false;
			}

			if ( !bestCandidate.Second.WeldTo(
				bestCandidate.First,
				bestCandidate.ContactPoint,
				WeldLinearStrength,
				WeldAngularStrength,
				EnableWeldedPieceCollisions,
				CreatePhysicalJoints ) )
			{
				Log.Warning( $"{DisplayName}: failed to weld {bestCandidate.Second.DisplayName} to raft root {bestCandidate.First.DisplayName}." );
				return false;
			}

			weldedCount++;
			anchorRoot = bestCandidate.First.GetWeldRoot();
			remainingRoots.RemoveAll( root => !root.IsValid() || root.GetWeldRoot() == anchorRoot );
		}

		var finalRoot = selectedPieces[0].GetWeldRoot();
		var allSelectedPiecesShareRoot = selectedPieces.All( piece => piece.IsValid() && piece.GetWeldRoot() == finalRoot );

		if ( !allSelectedPiecesShareRoot )
		{
			Log.Warning( $"{DisplayName}: weld verification failed; selected pieces still have more than one raft root." );
			return false;
		}

		return weldedCount > 0;
	}

	private MultiWeldCandidate FindClosestRootCandidate( List<BuildPiece> anchorPieces, List<BuildPiece> remainingRoots )
	{
		var maxGap = MaxMultiWeldGap.Clamp( 0f, 256f );
		MultiWeldCandidate bestCandidate = null;

		foreach ( var anchorPiece in anchorPieces )
		{
			if ( !anchorPiece.IsValid() )
				continue;

			foreach ( var root in remainingRoots )
			{
				if ( !root.IsValid() )
					continue;

				foreach ( var candidatePiece in root.GetWeldedPieces() )
				{
					var candidate = BuildWeldCandidate( anchorPiece, candidatePiece );

					if ( candidate is null )
						continue;

					if ( candidate.Distance > maxGap )
						continue;

					if ( bestCandidate is null || candidate.Distance < bestCandidate.Distance )
						bestCandidate = candidate;
				}
			}
		}

		return bestCandidate;
	}

	private MultiWeldCandidate BuildWeldCandidate( BuildPiece firstPiece, BuildPiece secondPiece )
	{
		if ( !firstPiece.IsValid() || !secondPiece.IsValid() )
			return null;

		if ( !firstPiece.Rigidbody.IsValid() || !secondPiece.Rigidbody.IsValid() )
			return null;

		var firstBounds = firstPiece.Rigidbody.GetWorldBounds();
		var secondBounds = secondPiece.Rigidbody.GetWorldBounds();
		var firstClosestToSecond = firstBounds.ClosestPoint( secondBounds.Center );
		var secondClosestToFirst = secondBounds.ClosestPoint( firstBounds.Center );
		var distance = (firstClosestToSecond - secondClosestToFirst).Length;
		var contactPoint = (firstClosestToSecond + secondClosestToFirst) * 0.5f;

		return new MultiWeldCandidate
		{
			First = firstPiece,
			Second = secondPiece,
			Distance = distance,
			ContactPoint = contactPoint
		};
	}
}
