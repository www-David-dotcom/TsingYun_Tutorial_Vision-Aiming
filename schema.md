I'm an engineer working in a RoboMaster team, responsible for the design and
implementation of the robot's self-aiming system. Now we're recruiting new
members to join our team. As a way of evaluating the candidates' skills and
knowledge, we're going to design a game that incorporates a self-aiming system.
The coarsest description of the game is:

- Game: based on Unity.
- Code: written in Python or C++. Only a scaffold will be provided; candidates
  are expected to complete the code by themselves to implement the self-aiming
  system.

## Visual design of the game
Here's an upper-level expectation:

> The game adopts a high-fidelity futuristic sci-fi 3D rendering style with a sleek cyberpunk industrial aesthetic for the maze environment. The entire maze is constructed with interlocking angular geometric structures, featuring smooth matte metallic walls, glossy carbon-fiber partition panels, and transparent tempered glass barriers embedded with luminous circuit lines. All assets apply physically based rendering (PBR) with precise metallic and roughness texture mapping, delivering realistic material reflections and surface texture layering.Volumetric ray-traced lighting dominates the scene: cool-toned cyan, electric blue and magenta neon light strips run along maze edges, corridor ceilings and wall gaps, casting soft bloom glows and long light trails in the air. Subtle atmospheric fog pervades the enclosed maze space, with dynamic light particles drifting slowly, enhancing the sense of depth and futuristic vastness. Ambient occlusion softens the shadow corners of maze intersections and structural corners, while real-time reflection on glass and metal surfaces mirrors agent silhouettes and neon light halos.Multi-agent characters are designed with streamlined futuristic tactical exoskeletons, rendered with gradient luminous armor lines, semi-transparent energy shield outlines, and subtle specular highlights on mechanical joints. Each agent features distinct glowing color identifiers on their chest and back for easy team and enemy distinction in the complex maze. FPS weapons adopt minimalist sci-fi firearm modeling with luminous power cores on the body, muzzle flash particle effects with dynamic light diffusion, and shell casings that cast tiny real-time shadows when bouncing on the ground.Holographic floating interfaces hover above maze intersections and key nodes, displaying mini-map projections, grid coordinate lines and glowing warning markers with semi-transparent alpha blending. The overall color palette is dominated by cold dark gray and deep black as the base, paired with high-saturation neon accent colors, creating a high-contrast, immersive tech-driven visual atmosphere. Dynamic post-processing effects including chromatic aberration, subtle lens vignetting and motion blur are applied to strengthen the first-person shooting immersion and futuristic sci-fi tension within the labyrinthine map.

The arena has already been started in Unity, but it still lacks the visual
quality described above. Future plans must treat this document as the highest
priority source of truth.

# Game Objects
- Vehicle: An agent with HP. It attacks by firing bullets. The vehicle is surrounded by 4 armor plates, divided into red and blue types. Each armor plate is printed with an MNIST sticker marked with a digit from 0 to 9, which represents the unique ID of the vehicle (one unique number per vehicle). The sticker is slightly smaller than the armor plate so that the underlying red or blue color of the plate remains visible. The red color is pure hex `#FF0000` and blue is pure hex `#0000FF`, using high-saturation solid colors instead of pale tints. Bullets only deal damage when hitting armor plates. Only the vehicle body has HP; armor plates have no independent HP, and any hit on an armor plate deducts HP directly from the vehicle itself. The vehicle can perform translational movement and self-rotation. Self-rotation makes it harder for enemy bullets to land on armor plates. Following real-world robot heat constraints, continuous chassis rotation reduces the maximum translational speed. Only the chassis rotates during self-rotation; the gimbal remains fixed and does not rotate along with the chassis.
- Gimbal: Mounted on top of the vehicle, equipped with a gun barrel and an adjacent camera. The camera serves as the player’s observation view and acts as the foundation for subsequent auto-aim implementation. The gimbal supports full 360° horizontal rotation, and the gun barrel’s pitch is constrained between -25° and 25°. It can fire at a rate of 5 rounds per second. Simulating real competition heat control mechanics, continuous prolonged firing triggers a forced fire lock; shooting can only resume after heat drops back to a safe threshold.
- Bullet: A 17mm spherical projectile traveling at 20 m/s along a parabolic trajectory. Each successful hit on an enemy armor plate deals 20 damage. Bullets deal no damage if hitting any part of a vehicle other than its armor plates. Damage values are consistent regardless of hitting teammates or opponents.
- Armor Plate: Emits a brief flash effect when struck by a bullet.

# Game Rule
The game is split into Red Team and Blue Team. Players are randomly assigned to one of the two teams. The upper-level game logic passes team information to lower-level modules via boolean flags, allowing the auto-aim system to identify its own team faction. The match supports 1v1, 2v2, or 3v3 vehicle configurations, with all vehicles on the same team assigned distinct ID numbers. Red Team vehicles spawn at a fixed corner spawn point, while Blue Team vehicles spawn diagonally at the opposite corner. Vehicle base HP scales with team size: 300 HP for 1v1, 500 HP for 2v2 per vehicle, and 700 HP for 3v3 per vehicle. The player always controls the vehicle with the maximum HP. Spawn points double as team healing zones; any vehicle within range regenerates 10 HP per second. Two score boost points spawn randomly and uniformly across the map. Any team holding proximity to a boost point gains 3 points per second. Vehicles respawn at their death position with full HP after a 10-second delay upon being eliminated. Each match lasts exactly 5 minutes. Victory conditions are defined as follows: If one team reaches 200 boost points first, they win immediately. If the 5-minute timer ends, the team with higher boost point total wins. If boost points are tied, total damage dealt is compared, with the higher damage team declared the winner. If all above conditions are tied, the match ends in a draw.

All AI vehicles except the player are controlled by a trained reinforcement learning policy. The learned policy is required to master the following behaviors:
- When at full/adequate HP: Learn to occupy the nearest boost point, and actively engage enemy vehicles within visual range (avoid passive behavior where multiple vehicles occupy separate boost points without engaging each other).
- When low on HP: Learn to take cover behind obstacles or return to the spawn healing zone, and avoid direct confrontation with enemies.
- When under enemy fire: Learn to activate chassis self-rotation to increase the difficulty for enemies to hit its armor plates.

The RL policy is trained directly within this Unity game simulation environment.

All non-player agents use a baseline aiming strategy that targets the geometric center of enemy vehicles by default. The player uses manual aiming and firing controls. After students complete and implement the auto-aim system, they can toggle both themselves and all teammate vehicles into full auto-aim mode with one click.

# Game stats
At the end of each match, the system outputs the player’s individual hit rate and the overall team hit rate.

# Current Deficiencies & Required Improvements
- The previous generated codebase is disorganized, containing redundant
  deployment and CI configurations, an abandoned non-Unity arena module, and
  numerous unnecessary smoke tests. The first priority is full codebase cleanup.
  Deployment pipelines are not required for now; only local runtime
  functionality of the Unity game needs to be preserved.
- Complete game art assets and visual design were never implemented in the prior iteration, though a design outline was already drafted. You may continue extending the existing plan via Unity MCP, redesign the art style from scratch, or integrate ready-made free asset packages to build the scene. Game visual and art design is a core requirement and must be polished to a high standard.
- Existing game rule logic is fragmented and incomplete, with many specified rules unimplemented in code. You need to reorganize and formalize all game rules strictly, then implement every rule accurately in the codebase.
- Reinforcement learning training code for the aforementioned vehicle AI policy is completely missing and needs to be fully developed from scratch.
- The existing fill-in-the-blank assignment framework designed by Claude Code is poorly structured. You need to redesign a challenging assignment framework that follows the current project workflow. All blank tasks must start with `TODO` comments; each blank should be neither overly trivial nor excessively complex in scope. Provide standard pseudocode as reference via inline comments, following the format example below:
```
openSet = PriorityQueue()
openSet.Add(start, f_score(start))
closedSet = Set()
cameFrom = Map()

g_score = Map()
g_score[all nodes] = Infinity
g_score[start] = 0

f_score = Map()
f_score[all nodes] = Infinity
f_score[start] = Heuristic(start, goal)

while openSet is not empty:
    current = openSet.PopLowest()

    if current == goal:
        return ReconstructPath(cameFrom, current)

    closedSet.Add(current)

    for each neighbor in GetNeighbors(current, grid):
        if neighbor in closedSet or neighbor is obstacle:
            continue

        tentative_g = g_score[current] + Distance(current, neighbor)

        if tentative_g >= g_score[neighbor]:
            continue

        cameFrom[neighbor] = current
        g_score[neighbor] = tentative_g
        f_score[neighbor] = g_score[neighbor] + Heuristic(neighbor, goal)

        if neighbor not in openSet:
            openSet.Add(neighbor, f_score[neighbor])
return null
```
- Partial assignment completions (e.g., finishing only Task 1–2) cannot run directly in the main game simulator. You need to design independent lightweight mini test cases to let students verify the correctness of their implemented logic separately.
- Design a dedicated training ground scene with a static target vehicle placed opposite the player. Implement a simple in-game UI that allows students to adjust the target vehicle’s translation speed and rotation speed in real time, to test whether their auto-aim system can correctly lock onto and hit the target vehicle’s armor plates, and visualize the real-time performance of their auto-aim logic.
