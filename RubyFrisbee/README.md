# Ruby's Frisbee — Unity 6 Game

A 2D side-scrolling frisbee-catching game starring **Ruby**, a black-and-tan German Shepherd who loves frisbee.

You play as Ruby — run left and right, jump, and catch frisbees before they hit the ground!

## Requirements

- [Unity 6](https://unity.com/download) (6000.x) with the **2D** and **Input System** modules
- Unity Hub recommended for project management

## Quick Start

1. Open **Unity Hub** → **Add** → select the `RubyFrisbee` folder in this repo.
2. Open the project with Unity 6 (6000.x). Unity will import packages on first load.
3. In the Unity menu bar, run **Ruby Frisbee → Setup Game Scene**.
4. Open `Assets/Scenes/GameScene.unity` if it isn't already open.
5. Press **Play**.

## Controls

### Desktop / keyboard

| Action | Keys |
|--------|------|
| Move left/right | Arrow keys or A / D |
| Jump | Space, W, or Up Arrow |

### Phone / touch (WebGL in mobile browser)

| Action | Control |
|--------|---------|
| Move left/right | Hold ← or → on-screen buttons |
| Jump | Tap **Jump** |

Touch controls appear automatically on phones and touch browsers during gameplay.

## Play on your phone (WebGL)

See **[WEBGL_DEPLOY.md](WEBGL_DEPLOY.md)** for the full guide. Quick version:

1. In Unity: **Ruby Frisbee → Configure WebGL Build**, then **Ruby Frisbee → Build WebGL**
2. Upload the `Builds/WebGL/` folder to [itch.io](https://itch.io), [Netlify](https://netlify.com), or [Vercel](https://vercel.com)
3. Open the link on your phone and play

## Gameplay

- Frisbees arc in from the right — catch them with Ruby's mouth (trigger collider).
- **Combo multiplier** grows with consecutive catches.
- **Air catch bonus** when Ruby catches while jumping.
- You have **3 lives** — a frisbee hitting the ground costs one life.
- High score is saved locally via `PlayerPrefs`.

## Project Structure

```
Assets/
  Editor/
    RubyFrisbeeSceneSetup.cs   # One-click scene bootstrap
  Scripts/
    Core/                      # GameManager, ScoreManager
    Player/                    # RubyController
    Gameplay/                  # Frisbee, FrisbeeSpawner, CameraJuice
    UI/                        # GameUI (menu, HUD, game over)
  Scenes/
    GameScene.unity            # Created by setup menu
  Prefabs/
    Frisbee.prefab             # Created by setup menu
```

## Ruby's Look (placeholder art)

The setup tool builds a stylized placeholder Ruby using colored sprites:

- Tan/gold body (`#C4A35A`)
- Black mask and upright ears
- Grassy backyard with fence silhouette

Replace placeholder sprites in `Assets/Sprites/` with custom art based on Ruby's photos when ready.

## Next Steps

- [ ] Custom Ruby sprite sheet (idle, run, jump, catch)
- [ ] Bark SFX on catch, thud on miss
- [ ] Particle effects (grass kick, catch burst)
- [x] Mobile touch controls (on-screen buttons)
- [ ] Golden frisbee bonus disc
- [x] WebGL build for phone browser — see [WEBGL_DEPLOY.md](WEBGL_DEPLOY.md)

## Building

### WebGL (phone browser)

1. **Ruby Frisbee → Configure WebGL Build**
2. **Ruby Frisbee → Build WebGL**
3. Host `Builds/WebGL/` — see [WEBGL_DEPLOY.md](WEBGL_DEPLOY.md)

### Other platforms

1. **File → Build Settings**
2. Add `Assets/Scenes/GameScene.unity`
3. Choose your target platform (PC, Android, iOS)
4. Click **Build**

---

Made with love for Ruby.
