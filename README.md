A work-in-progress Flood-style gamemode for S&box built with C#, GameObjects, Components, Scenes, and networked server-authoritative gameplay systems.

This project is inspired by Garry's Mod Flood: players build rafts out of props, the water rises, the build phase ends, and everyone tries to survive while fighting on whatever they managed to weld together.

This is my first full game project, so the goal is simple: keep the code modular, keep the systems readable, and keep improving it one playable slice at a time.

Current Gameplay Loop
Players join the server.
The round waits until enough players are connected.
Build phase starts.
Players spend money to place props.
Players can weld their own props together into rafts.
Build phase ends and water rises.
Props float using the custom Flood buoyancy system.
Combat phase starts.
Players can damage each other and enemy boat pieces.
The last surviving player wins.
The scoreboard and winner preview display.
The round resets.
Implemented Systems
Round Flow
FloodGameManager controls the main phase loop.
Round states:
WaitingForPlayers
BuildPhase
FloodPhase
CombatPhase
RoundEnd
Phase timers are networked for host and clients.
Debug controls can force phases, reset rounds, and damage players.
Round reset cleans up placed build pieces and respawns players.
Building
Players can place build pieces during build phase only.
Placement is server-authoritative.
Money is deducted only after successful placement.
Build pieces remember their owner.
Players can only sell and weld pieces they own.
Other players can damage enemy pieces during combat.
Placement preview supports valid/invalid colors.
Placement uses grid snapping and build area validation.
Prop resources are data-driven through .bpiece files.
Props and Materials
Build pieces now use real S&box prop models and their authored model collision.

Current material categories:

Wood: cheap, light, floats well, weak.
Metal: expensive, heavy, strong.
Plastic: very buoyant, low health.
Armor: strong plating intended to reinforce boats.
Each build piece resource can tune:

Display name
Description
Cost
Weight
Max health
Material type
Prop model
Placement bounds
Surface offset
Weld/boat-part behavior
Welding
Weld tool weapon exists as its own tool.
Welding is owner-restricted during build phase.
Welded pieces become connected raft pieces.
Connected raft pieces improve raft stability and lift.
Destroyed pieces can weaken raft stability.
Water and Buoyancy
FloodWaterController controls water rise, drain, reset, and replicated water state.
Water visuals and water trigger behavior work for host and joined clients.
FloodBuoyancy applies custom floating behavior to build pieces.
Buoyancy supports material presets.
Welded/attached rafts receive lift and stability bonuses.
Damaged rafts lose lift/stability.
Players must be about halfway submerged before water damage starts.
Combat and Damage
Player health is server-authoritative.
Boat piece health is server-authoritative.
Pistol and crowbar damage works across host and clients.
Players are eliminated on death.
Ragdolls spawn on death.
Dead players are hidden and locked out of movement/tools.
Weapons and Tools
Modular weapon base exists for adding more carryables.
First-person viewmodels are local-player only.
Third-person world models replicate to other players.
Current weapons/tools include:
USP pistol
Crowbar
Tool gun / weld tool
UI
Networked round HUD.
Timer and phase display.
Death HUD.
Scoreboard.
Round winner display with rotating player model preview.
Ammo HUD for weapons.
Project Structure
Code/
  Building/   Build pieces, placement, preview, factory, economy hooks
  Core/       Round manager, networking, win conditions
  Damage/     Shared damage data/components
  Player/     Player component, health, camera, inventory hooks
  UI/         Flood HUD, scoreboard, winner preview
  Water/      Water controller, buoyancy, water damage, swim support
  Weapons/    Weapon/tool base classes and implementations

Assets/
  prefabs/    Player, builder, weapon, and gameplay prefabs
  resources/  Build piece resources and prop data
  scenes/     Main test scene
Development Notes
Gameplay authority should stay on the host/server.
Client code should mainly handle input, prediction-friendly visuals, HUD, and local viewmodels.
Build piece spawning, money changes, damage, ownership, round state, and win checks should remain server-authoritative.
Use [Property] for values that need editor tuning.
Keep new gameplay systems as focused Components instead of large manager classes.
Testing In S&box
Recommended test flow:

Open the project in S&box.
Start the main scene as host.
Use Join via New Instance to test networking.
Wait for the second player to connect.
Confirm the round enters build phase.
Place props, weld owned props, and verify ownership restrictions.
Let water rise.
Confirm both host and client see/swim/take damage from water.
Enter combat phase.
Damage players and boat pieces.
Confirm round end, winner display, scoreboard, and reset.
Debug Controls
Debug controls are intentionally isolated behind the round manager debug settings.

Current debug behavior:

Damage all players.
Reset round.
Force build phase.
Force battle/combat phase.
These can be disabled through EnableDebugControls.

Roadmap
Short-term goals:

Continue tuning buoyancy and raft stability.
Improve build placement for irregular prop shapes.
Add better weld feedback, sounds, particles, and tool animations.
Add a proper prop shop/build menu.
Improve armor behavior so armor can reinforce existing boat pieces.
Expand economy rewards and round-end payouts.
Polish scoreboard and winner presentation.
Longer-term goals:

Team support.
More weapons/tools.
Better raft destruction feedback.
More maps/build areas.
Better client-side prediction and polish.
Persistent progression or unlocks, if the gameplay earns it.
Status
Flood 2.0 is playable but still heavily WIP. The core loop, networking, building, water, weapons, damage, and round reset are all in active development.
