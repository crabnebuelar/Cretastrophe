# Cretastrophe

**Cretastrophe** is a 2D puzzle-platformer centered around drawing and erasing chalk platforms to navigate levels and solve physics-based puzzles.

Players can dynamically create and erase platforms in real time, requiring the game systems to handle constantly changing collision geometry, unstable surfaces, and physics interactions driven by player input.

---

## My Contributions

I was responsible for the core gameplay systems, including:

- Player controller and movement system
- Line-drawing and erasing mechanic for chalk platforms
- Physics-driven puzzle mechanics built on dynamic, player-created geometry

My work focused on making the game feel responsive and stable despite the environment changing every frame.

---

## Key Systems I Implemented

### Player Controller

- Implemented a custom 2D platformer controller
- Adapted movement and grounding logic to support:
  - Rapidly changing slopes
  - Non-uniform, player-drawn platforms (not straight lines)
- Ensured stable movement when platforms are:
  - Added or removed under the player
  - Modified while the player is standing on them

This required handling edge cases where collision normals and surface angles change continuously due to player input.

---

### Chalk Line Drawing & Erasing System

- Implemented a real-time line-drawing system allowing players to:
  - Draw platforms
  - Erase sections of existing platforms
- Lines are converted into physics objects with collision and rigidbody behavior
- Designed the system to support different chalk types with unique behavior

---

### Physics-Driven Chalk (Gravity-Affected Chalk)

One chalk type is affected by gravity, which introduced several non-trivial challenges:

- Chalk lines behave as rigidbodies and can fall or break apart
- Erasing a section of a chalk line:
  - Splits a single line into **multiple independent rigidbody objects**
  - Requires re-generating collision, mass, and physics state for each segment
- Ensured stable behavior when:
  - Lines are erased mid-air
  - Lines are partially erased while under physics simulation

This system required careful handling of geometry splitting and physics reassignment to avoid instability or jitter.

---

### Puzzle Mechanics

- Designed and implemented multiple puzzle mechanics built on top of:
  - The dynamic line-drawing system
  - Physics-based interactions
- Created mechanics that rely on:
  - Player-drawn geometry
  - Timing
  - Environmental manipulation

---

## Technical Challenges

- Handling player movement on **constantly changing collision geometry**
- Maintaining stable physics behavior when geometry is created, destroyed, or split at runtime
- Designing systems flexible enough to support multiple chalk behaviors without hard-coding logic

---

## Technologies Used

- 2D physics engine
- Custom gameplay and physics systems
- C# (Unity)

---

## Relevant Code

Key systems implemented by me can be found in:
- `Assets/Scripts/PlayerControl/`
- `AssetsScripts/DrawErase/`

(See individual files for detailed implementations of movement, drawing, erasing, and physics handling.)
