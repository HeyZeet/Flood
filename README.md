# Flood 2.0

A work-in-progress Flood-style gamemode for [S&box](https://sbox.game/) built with C#, GameObjects, Components, Scenes, and networked server-authoritative gameplay systems.

This project is inspired by Garry's Mod Flood: players build rafts out of props, the water rises, the build phase ends, and everyone tries to survive while fighting on whatever they managed to weld together.

This is my first full game project, so the goal is simple: keep the code modular, keep the systems readable, and keep improving it one playable slice at a time.

## Current Gameplay Loop

1. Players join the server.
2. The round waits until enough players are connected.
3. Build phase starts.
4. Players spend money to place props from the build menu.
5. Players can weld their own props together into rafts, including multi-select welds.
6. Build phase ends and water rises.
7. Welded rafts float using the custom Flood buoyancy and stability system.
8. Combat phase starts.
9. Players can damage each other and enemy boat pieces.
10. The last surviving player wins.
11. The scoreboard and winner preview display.
12. The round resets.

## Implemented Systems

### Round Flow

- `FloodGameManager` controls the main phase loop.
- Round states:
  - `WaitingForPlayers`
  - `BuildPhase`
  - `FloodPhase`
  - `CombatPhase`
  - `RoundEnd`
- Phase timers are networked for host and clients.
- Debug controls can force phases, reset rounds, and damage players.
- Round reset cleans up placed build pieces and respawns players.

### Building

- Players can place build pieces during build phase only.
- Placement is server-authoritative.
- Money is deducted only after successful placement.
- Build pieces remember their owner.
- Players can only sell and weld pieces they own.
- Other players can damage enemy pieces during combat.
- Placement preview supports valid/invalid colors.
- Placement uses grid snapping and build area validation.
- Prop resources are data-driven through `.bpiece` files.
- Press **Q** with the Boat Builder equipped to open the build menu.
- Press **RMB** with the Boat Builder equipped to cycle to the next piece.
- The build menu groups pieces by material and shows each prop as a spinning 3D preview.

### Props and Materials

Build pieces now use real S&box prop models and their authored model collision.

Current material categories:

- **Wood**: cheap, light, floats well, weak.
- **Metal**: expensive, heavy, strong.
- **Plastic**: very buoyant, low health.
- **Armor**: strong plating intended to reinforce boats.
- **Foam**: very buoyant lightweight support material, currently supported by code/presets.

Each build piece resource can tune:

- Display name
- Description
- Cost
- Weight
- Max health
- Material type
- Prop model
- Placement bounds
- Surface offset
- Weld/boat-part behavior

### Welding

- Weld tool weapon exists as its own tool.
- Welding is owner-restricted during build phase.
- Players can select multiple owned pieces and weld them together at once.
- Selected pieces are highlighted.
- Welded pieces are structurally grouped into a single raft root for stable raft physics.
- Connected raft pieces contribute to raft stability and lift.
- Destroyed pieces can weaken raft stability.
- Players cannot sell or weld other players' pieces, but enemy pieces can be damaged during combat.

### Water and Buoyancy

- `FloodWaterController` controls water rise, drain, reset, and replicated water state.
- Water visuals and water trigger behavior work for host and joined clients.
- `FloodBuoyancy` applies custom floating behavior to build pieces.
- Buoyancy supports material presets.
- Welded rafts use group buoyancy so the raft floats as one connected craft instead of each prop fighting for its own water height.
- Damaged rafts lose lift/stability.
- Players must be about halfway submerged before water damage starts.
- Player raft movement assist is currently disabled while raft walking/standing behavior is being tuned.

### Combat and Damage

- Player health is server-authoritative.
- Boat piece health is server-authoritative.
- Pistol and crowbar damage works across host and clients.
- Players are eliminated on death.
- Ragdolls spawn on death.
- Dead players are hidden and locked out of movement/tools.

### Weapons and Tools

- Modular weapon base exists for adding more carryables.
- First-person viewmodels are local-player only.
- Third-person world models replicate to other players.
- Current weapons/tools include:
  - USP pistol
  - Shotgun class
  - Crowbar
  - Tool gun / weld tool

### UI

- Networked round HUD.
- Timer and phase display.
- Death HUD.
- Scoreboard.
- Round winner display with rotating player model preview.
- Ammo HUD for weapons.
- Build HUD with selected piece, resources, and controls.
- Build menu with material tabs and spinning 3D prop previews.

## Project Structure

```text
Code/
  Building/   Build pieces, placement, preview, factory, build menu hooks
  Core/       Round manager, networking, win conditions
  Damage/     Shared damage data/components
  Player/     Player component, health, camera, inventory hooks
  UI/         Flood HUD, build menu, scoreboard, winner preview
  Water/      Water controller, buoyancy, water damage, swim support
  Weapons/    Weapon/tool base classes and implementations

Assets/
  prefabs/    Player, builder, weapon, and gameplay prefabs
  resources/  Build piece resources and prop data
  scenes/     Main test scene
```

## Development Notes

- Gameplay authority should stay on the host/server.
- Client code should mainly handle input, prediction-friendly visuals, HUD, and local viewmodels.
- Build piece spawning, money changes, damage, ownership, round state, and win checks should remain server-authoritative.
- Use `[Property]` for values that need editor tuning.
- Keep new gameplay systems as focused Components instead of large manager classes.

## Testing In S&box

Recommended test flow:

1. Open the project in S&box.
2. Start the main scene as host.
3. Use **Join via New Instance** to test networking.
4. Wait for the second player to connect.
5. Confirm the round enters build phase.
6. Place props, weld owned props, and verify ownership restrictions.
7. Press **Q** with the Boat Builder equipped and confirm the build menu opens.
8. Select pieces from different material tabs and confirm the preview/placement updates.
9. Let water rise.
10. Confirm both host and client see/swim/take damage from water.
11. Enter combat phase.
12. Damage players and boat pieces.
13. Confirm round end, winner display, scoreboard, and reset.

## Debug Controls

Debug controls are intentionally isolated behind the round manager debug settings.

Current debug behavior:

- Damage all players.
- Reset round.
- Force build phase.
- Force battle/combat phase.

These can be disabled through `EnableDebugControls`.

## Roadmap

Short-term goals:

- Continue tuning buoyancy and raft stability.
- Continue improving player movement on floating rafts.
- Polish the build menu layout, input focus, and model preview sizing.
- Add better weld feedback, sounds, particles, and tool animations.
- Improve armor behavior so armor can reinforce existing boat pieces.
- Expand economy rewards and round-end payouts.
- Polish scoreboard and winner presentation.

Longer-term goals:

- Team support.
- More weapons/tools.
- Better raft destruction feedback.
- More maps/build areas.
- Better client-side prediction and polish.
- Persistent progression or unlocks, if the gameplay earns it.

## Status

Flood 2.0 is playable but still heavily WIP. The core loop, networking, building, water, weapons, damage, and round reset are all in active development.
