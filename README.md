# AI-Labs
## AI System

During this laboratory work, I implemented AI systems for **two different enemy types** (excluding supporting systems such as player movement, player health, camera control, etc.):

* **Regular Enemy**
* **Teleporting Enemy**

The main goal was to create a fully functional AI system for both enemies using different behaviors and states.

### Regular Enemy

The AI system of the regular enemy is relatively simple. The enemy moves between predefined patrol points in sequence (**PATROL**) and has a **180-degree field of view**.

The enemy behavior works as follows:

* If the player enters the enemy's field of view, the enemy starts **CHASING** the player.
* If the player leaves the field of view, the enemy moves toward the **last position where the player was seen** (**SEARCH**).
* If the enemy does not find the player during the search, it returns to **PATROL**.
* If the player is found, the enemy switches back to **CHASING**.
* While **CHASING**, if the player is within attack range and the attack is not on cooldown, the enemy **ATTACKS**.

The overall behavior can therefore be represented as:

`PATROL → CHASE → SEARCH → PATROL / CHASE`

### Teleporting Enemy

The AI system of the teleporting enemy is more complex.

The enemy also patrols predefined points in sequence (**PATROL**), but unlike the regular enemy, it has a **360-degree field of view** and can see the player **through walls**.

When the enemy detects the player:

* If it can safely teleport to a valid position (meaning the destination is not inside a wall or another obstacle), it **TELEPORTS** and then starts **CHASING** the player.
* If teleportation is not possible, it immediately starts **CHASING** the player.

While **CHASING**, the enemy will attempt to **TELEPORT** whenever possible and will **ATTACK** whenever the player is within attack range and the attack is not on cooldown.

If the player leaves the enemy's detection area, the teleporting enemy uses a system similar to the regular enemy:

* It moves toward the **last known position of the player** (**SEARCH**).
* If the player is found, the enemy returns to **CHASING**.
* If the player is not found, the enemy returns to **PATROL**.
* However, there is one important difference: if the enemy encounters an obstacle while moving toward a patrol point, it will attempt to **TELEPORT through the obstacle** if a valid teleport destination is available.


with **TELEPORT** and **ATTACK** being additional actions available during **CHASE**, and **TELEPORT** also being available during **PATROL** when an obstacle blocks the enemy's path.
