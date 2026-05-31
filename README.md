# Flood 2.0

Flood 2.0 is a work-in-progress Flood-style gamemode for [S&box](https://sbox.game/) built with C#, GameObjects, Components, Scenes, and networked server-authoritative gameplay systems.

The project is inspired by Garry's Mod Flood: players build rafts out of props, weld them together, survive rising water, then fight on whatever still floats.

## Current Gameplay Loop

1. Players join the server.
2. The round waits until enough players are connected.
3. Build phase starts.
4. Players use build money to place props from the build menu.
5. Players weld owned props together into a raft.
6. Build phase ends and water rises.
7. Welded rafts float using the custom buoyancy and stability system.
8. Combat phase starts.
9. Players damage each other and enemy boat pieces.
10. Eliminated players are hidden/locked out and ragdolls spawn.
11. The last surviving player wins.
12. Winner display and scoreboard show.
13. The round resets for the next loop.

## Implemented Systems

### Round Flow

- `FloodGameManager` controls the phase loop.
- Current phases:
  - `WaitingForPlayers`
  - `BuildPhase`
  - `FloodPhase`
  - `CombatPhase`
  - `RoundEnd`
- Phase state and timers replicate to host and clients.
- Game starts once the required player count is reached.
- Round reset cleans up placed pieces, resets water, respawns players, and prepares the next build phase.
- Debug helpers exist for forcing phases, resetting rounds, and damaging players.

### Building

- Players can build only during `BuildPhase`.
- Placement is server-authoritative.
- Money is deducted only after successful placement.
- Build pieces remember their owner.
- Players can only sell or weld their own pieces.
- Enemy pieces can be damaged during combat.
- Placement preview supports valid/invalid colors.
- Placement supports grid snapping, surface placement, irregular prop bounds, and build-area validation.
- Buildable props are data-driven through `.bpiece` resources.
- Press `Q` with the Boat Builder equipped to open the build menu.
- The build menu groups props by material and shows spinning 3D prop previews.

### Props And Materials

Build pieces use real S&box prop models and their authored model collisions.

Current material categories:

- `Wood`: cheap, light, floats well, weak.
- `Metal`: expensive, heavy, strong.
- `Plastic`: very buoyant, low health.
- `Armor`: strong plating intended to reinforce boats.
- `Foam`: lightweight buoyant support, supported by code/presets.

Each build resource can tune display name, description, cost, material, health, weight, buoyancy, model, placement bounds, surface offset, and weld behavior.

### Welding

- Weld tool is a proper carryable/tool.
- Welding is owner-restricted during build phase.
- Players can select multiple owned pieces and weld them together at once.
- Selected pieces are highlighted.
- Welded pieces are grouped under a raft root for stable raft buoyancy.
- Structural welding avoids the worst physics breakage from fully physical joints.
- Damaged or destroyed pieces can weaken or break weld connections.

### Water And Buoyancy

- `FloodWaterController` controls water rise, drain, reset, visual water, and replicated water height.
- Water works for host and joined clients.
- Players can swim in the flood water on host and client.
- Players must be roughly halfway submerged before water damage begins.
- Water uses an invisible gameplay volume for swim/damage plus a separate flat visual surface.
- Visual water uses a custom material/shader with subtle animated dirty-water movement.
- A screen-space underwater distortion effect runs from the local player camera.
- Water entry and bobbing sounds can be assigned through `FloodBuoyancy`.
- `FloodBuoyancy` applies custom floating behavior to build pieces.
- Material presets affect lift, drag, stability, and mass behavior.
- Welded rafts use group buoyancy so connected pieces float as one craft instead of each prop fighting independently.
- Raft stability is actively being tuned to reduce spinning, flipping, and vertical separation between welded pieces.
- Recent buoyancy work is inspired by K3rhos/WaterTool-style ideas: sampled water height, spring/damping lift, water flow, and multi-point hull sampling.

Current known tuning issue:

- Some welded barrel/crate raft layouts can still flip upside down or behave awkwardly under load. They float, but raft leveling is still experimental.

### Combat And Damage

- Player and build-piece damage is server-authoritative.
- Players are eliminated on death.
- Ragdolls spawn when players die.
- Dead players are hidden and prevented from moving or using tools.
- Build pieces can be damaged and destroyed during combat.
- Weapon impact effects are broadcast to clients.
- Melee impact effects use the same authoritative hit trace as damage.

### Weapons, Tools, And Inventory

- Carryables use a modular inventory system designed for future tools/weapons.
- First-person viewmodels are local-player only.
- Third-person world models replicate to other players.
- Weapons/tools can be locked by default and unlocked through the shop.
- Current carryables:
  - Boat Builder
  - Weld Tool
  - USP Pistol
  - Shotgun
  - Crowbar
- USP, shotgun, and crowbar are shop unlocks.
- Shotgun reloads one shell at a time and supports empty-first-shell reload animation behavior.
- Gun and melee impact effects support material-specific prefabs.
- Weapon sounds, reload animations, impact effects, local viewmodels, and third-person world models are networked for host/client play.

### Shop

- `PlayerShopController` handles local shop input and purchase requests.
- Shop items are data-driven through `.shopitem` resources.
- Current shop items include USP pistol, shotgun, and crowbar.
- Purchases are server-authoritative.
- Unique weapon purchases unlock existing pre-networked carryables when available, which keeps host/client inventory replication stable.
- The system is intended to expand later into perks and game modifiers.

### UI

- Unified Flood HUD for round state, timer, build info, ammo, shop, build menu, scoreboard, and winner display.
- Round HUD updates for host and clients.
- Build menu has material tabs and spinning 3D prop previews.
- Shop displays money and purchasable unlocks.
- Carryable selector HUD appears while selecting a tool/weapon and hides after selection.
- Scoreboard and round winner display show at round end.
- Winner preview displays a rotating player model.
- A simple crosshair keeps build/combat aiming centered on the same origin.

### Maps And Presentation

- `basemap.scene` is the current arena map in the repo.
- Water sizing/offsets are tuned around the `MainRoom` gameplay space.
- The map uses a daytime outdoor arena direction with enclosed walls.
- The water setup is designed so the gameplay water volume can stay hidden while the visible surface provides the look.

## Project Structure

```text
Code/
  Building/   Boat builder, placement, preview, factory, build pieces, ownership, selling
  Core/       Round manager, phase enum, win conditions, network/spawn helpers
  Damage/     Shared damage data and damageable components
  Player/     Player, health, camera, inventory, economy/resources
  Shop/       Shop item data and player shop controller
  UI/         Unified Flood HUD, build/shop/scoreboard models, preview helpers
  Water/      Water controller, buoyancy, water damage, swim support
  Weapons/    Carryable, weapon, gun, melee, tool, viewmodel, and world model systems

Assets/
  materials/  Flood water material and supporting textures
  prefabs/    Player, weapons, tools, impact effects, muzzle flashes, gameplay prefabs
  resources/  Build piece resources and shop items
  scenes/     Basemap and supporting scenes
  shaders/    Water surface and screen-space water distortion shaders
  sounds/     Flood-specific sound event assets
```

## Testing In S&box

Recommended multiplayer test flow:

1. Open the project in S&box.
2. Start the main scene as host.
3. Use `Join via New Instance`.
4. Wait for the second player to connect.
5. Confirm build phase starts and the HUD timer updates on both screens.
6. Equip Boat Builder and press `Q` to open the build menu.
7. Place props from multiple material groups.
8. Equip Weld Tool, select owned pieces, and weld them into a raft.
9. Confirm other players cannot sell or weld your pieces.
10. Let the water rise.
11. Confirm both host and client see water, swim, take water damage, hear water sounds, and see rafts float.
12. Enter combat phase.
13. Buy/equip weapons from the shop.
14. Confirm weapon unlocks appear in inventory on host and client.
15. Shoot and melee players/props and confirm sounds, animations, damage, and impact effects replicate.
16. Kill a player and confirm ragdoll/elimination behavior.
17. Confirm round winner display, scoreboard, and reset.

Useful raft stress test:

1. Place 4 crates in a square.
2. Place 4 plastic barrels around the sides/corners.
3. Select all pieces with the Weld Tool and weld them into one raft.
4. Start flood phase.
5. Watch for vertical separation, spinning, flipping, and player movement on top of the raft.

## Development Notes

- Keep gameplay authority on the host/server.
- Client code should mostly handle input, HUD, prediction-friendly presentation, and local viewmodels.
- Build spawning, money, shop purchases, damage, ownership, round state, water state, and win checks should remain server-authoritative.
- Prefer small Components over large managers.
- Use `[Property]` for editor-tunable values.
- Keep new systems modular so weapons, tools, build materials, shop items, and future perks can be added without rewriting the core loop.

## Roadmap

Short-term goals:

- Continue tuning raft walking/player movement on floating rafts.
- Continue tuning welded raft buoyancy, load balancing, and upside-down recovery.
- Improve armor so it reinforces existing boat pieces.
- Add stronger feedback for water entry, prop splashes, and raft damage.
- Add more weapon/tool polish, particles, sounds, and animations.
- Expand shop content and add round-end payouts.
- Improve scoreboard/winner presentation.
- Add clearer build/weld feedback.

Longer-term goals:

- Team support.
- More weapons, tools, perks, and game modifiers.
- More maps and build areas.
- Better raft destruction feedback.
- Better client-side presentation/prediction polish.
- Persistent progression or unlocks if the gameplay earns it.

## Status

Flood 2.0 is playable in host/client testing. The core loop, networking, building, ownership, welding, water, shop unlocks, weapons, damage, HUD, winner display, and round reset are working. Buoyancy and raft stability are functional but still the most active tuning area.
