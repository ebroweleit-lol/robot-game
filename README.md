# Robot Game

A Unity game featuring two robots that battle using a push power system.

## Features

- Two-player local multiplayer
- Push power mechanics: hold forward to build power (0-100)
- Power-based push battles: stronger robot pushes the weaker one
- Kinematic physics with custom collision detection
- Gravity and falling off edges

## Controls

**Player 1 (WASD):**
- W/S: Move forward/backward
- A/D: Rotate left/right

**Player 2 (IJKL):**
- I/K: Move forward/backward
- J/L: Rotate left/right

## Gameplay

- Hold forward to build push power (up to 100)
- Power decays when not pressing forward
- When robots collide, the one with more power pushes the other
- Power numbers displayed above each robot (yellow for P1, cyan for P2)

## Technical Details

- Built with Unity
- Custom kinematic rigidbody movement
- Collision detection using OverlapSphere
- Manual gravity implementation
