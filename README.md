# Tower Defense

CSCI 381, Video Game Design and Development, Assignment 2
Donovan Gonzalez. Unity 6 (6000.6.0f1), URP.

## How to play
Stop the enemies before they reach the red FINISH pad. Earn coins by killing enemies and spend them on towers.

| Control | Action |
|---|---|
| Click the bottom menu, or keys 1/2/3 (Tab cycles) | Choose a tower |
| Left click on the grass | Place the tower |
| SPACE | Skip the prep timer and start the wave |
| R | Restart |

## Towers
- **Rapid:** cheap, fast fire, low damage.
- **Sniper:** long range, high damage.
- **Bomber:** splash damage that ignores armor.

Towers can't be built on the road or too close to each other, and there is a tower cap.

## Levels
1. **Grassy Start:** 20 enemies (Goblins, Wolves, Ogres).
2. **Winding Valley:** 30 enemies and a new path, adding Golems (armored) and Skeletons.
3. **Boss Gauntlet:** 36 enemies, flying UFOs, and a demon boss with 3 phases. Fast Wolves keep spawning until the boss dies, and he summons Ogres and a Wolf swarm as he loses health.

## Features
- Coin economy with a locked or affordable tower menu
- Enemy types: fast, tank, armored (takes reduced damage), flyer, boss
- Predictive tower aiming and splash explosions
- Health bars and a HUD (level, enemies left, boss alerts)
- Procedural map: grass, dirt road, brick wall border, trees and rocks

## Assets
There are no third-party assets. The textures, enemy models, towers and scenery are all generated in code with Unity primitives, so nothing copyrighted is used. Scripts are in `Assets/Scripts`. AI assistance (Claude) was used through my own prompts.