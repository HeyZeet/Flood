public enum GamePhase
{
	WaitingForPlayers = 0,
	Waiting = WaitingForPlayers,

	BuildPhase = 1,
	Build = BuildPhase,

	FloodPhase = 2,
	Flood = FloodPhase,

	CombatPhase = 3,
	Battle = CombatPhase,

	RoundEnd = 4
}
