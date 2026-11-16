# Tower Defense

CSCI 381, Video Game Design and Development (Fall 2026), Assignment 2
Donovan Gonzalez

A 3D physics tower defense game built in Unity 6 (6000.6.0f1, URP). Stop every enemy before it reaches the red FINISH pad.

## How to run
1. Open the project folder in Unity 6000.6.0f1.
2. Open `Assets/Scenes/SampleScene`.
3. Press Play.

## Controls
| Input | Action |
|---|---|
| Click a tower in the bottom menu, or keys 1 / 2 / 3 (Tab cycles) | Choose a tower |
| Left click on the grass | Place the selected tower |
| SPACE | Skip the preparation timer and start the wave, or go to the next level |
| R | Restart |

On-screen text explains how to place towers. You get a limited number of towers per level (8 maximum), and towers can't be placed on the road, outside the walls, or too close to each other.

## Towers
Each tower type has different stats, including height.

| Tower | Height | Role |
|---|---|---|
| Rapid | Short | Cheap, fast fire, low damage |
| Sniper | Tall | Long range, high damage per shot, slow fire rate |
| Bomber | Medium, wide | Splash damage that ignores enemy armor, slower shells |

Rate of fire, projectile speed, range, damage and cost are all different per tower. Towers aim at the **predicted position** of their target, based on its current velocity and location, so shots intercept moving enemies.

## Coins
You start each level with a set amount of coins and earn more by killing enemies. Towers cost coins, and towers you can't afford are shown as locked in the menu.

## Enemies
| Enemy | Notes |
|---|---|
| Goblin | Basic enemy |
| Wolf | Very fast, low HP |
| Ogre | Slow tank with high HP |
| Skeleton | Fast and fragile |
| Golem | Armored, takes half damage from bullets |
| UFO | Flying enemy that goes straight over the map |
| Demon Boss | 3 phases (see level 3) |

Enemies have different speed and HP, and HP scales up each level. Every enemy has a health bar.

## Levels
1. **Grassy Start:** 20 enemies (Goblins, Wolves, Ogres) on a winding path.
2. **Winding Valley:** 30 enemies on a new path, adding Golems and Skeletons.
3. **Boss Gauntlet:** 36 enemies on a third path, plus flying UFOs and a Demon boss. Fast Wolves keep spawning until the boss is dead. At 50% health he summons Ogres, and at 25% health he enrages and sends a Wolf swarm.

Enemies travel along predetermined, non-straight paths. The game ends if an enemy reaches the finish.

## Above and beyond
- Three levels with different paths and difficulty
- Coin economy with locked or affordable towers
- Armor, a flying enemy type, and a 3-phase boss with summons
- Splash-damage Bomber with explosion effects
- Enemy models, towers and scenery built in code from Unity primitives, with generated grass, brick wall and dirt road textures
- Walk animations, hovering UFO, flapping boss wings
- HUD with level, enemies remaining and boss alerts

## Assets and credits
- Most visuals (enemies, towers, map, textures) are built in code with Unity primitives, so no copyrighted assets are used.
- The UFO and tower weapon models come from the **Kenney Tower Defense Kit** (https://kenney.nl/assets/tower-defense-kit), licensed **CC0**. They are in `Assets/Resources/Kenney`.
- Scripts are in `Assets/Scripts`.
- AI assistance (Claude) was used through my own prompts.