# Connecting the package to the existing RubyFrisbee project

This guide targets the supplied Unity 6000.0.34f1 repository. The package itself does not change its existing gameplay scripts or scene.

## Assembly reference

The existing gameplay assembly is named `RubyFrisbee` in `Assets/Scripts/RubyFrisbee.asmdef`. Add an Assembly Definition Reference to **RubysFrisbee.Assets.Runtime** before referring to its C# classes from that assembly. The package's own preview scene does not need this extra reference.

## Player visual

1. Keep the existing RubyController, Rigidbody2D and collision logic.
2. Remove or disable the old colored placeholder renderers: body, mask and ears. Avoid accidentally disabling the newly added visual renderer.
3. Add `Generated/Prefabs/RubyVisual.prefab` as a visual child of the existing player.
4. Align its foot-origin pivot to the player's existing GroundCheck/contact point. Do not assume the physics root is already at the feet.
5. Avoid inheriting the placeholder's non-uniform scale. Use a unit-scale visual hierarchy or compensate deliberately; tune colliders rather than stretching Ruby's legs/body.
6. Hold a serialized reference to RubyAnimationDriver and feed authoritative movement/ground state after your controller updates it.

Example additions to the existing controller (adapt placement to your code):

```csharp
[SerializeField] RubysFrisbee.Assets.RubyAnimationDriver animationVisual;

void UpdateVisual()
{
    if (!animationVisual) return;
    animationVisual.SetLocomotion(Mathf.Abs(rb.linearVelocity.x), rb.linearVelocity.y, IsGrounded);
    animationVisual.SetFacing(IsFacingRight);
}
```

The current original UpdateFacing only flips its own SpriteRenderer. Feed `SetFacing` on the new visual too. If a catch collider/socket is directional, mirror its offset as well; flipping a SpriteRenderer does not flip collider offsets.

## Catch and game-state hooks

On a confirmed catch, call `animationVisual.TriggerCatch()` and play `audioRig.PlayCue("catch")`, optionally spawning a catch effect near the mouth. The controller chooses GroundCatch or AirCatch using IsGrounded. Running catches may transition back to Run early; tune state timing against gameplay responsiveness.

On a confirmed miss, play `miss` and spawn a miss effect at the actual ring impact point. Do not derive gameplay success from animation events. The game remains authoritative for scores, combos and lives.

On GameOver, call `SetGameOver(true)` once or repeatedly—the helper edge guard prevents restart loops. On a new session/recovery, call `Recover()` or `SetGameOver(false)`.

Menu sitting is optional: call `SetSitting(true)` when movement is stopped; false requests StandUp. Movement/airborne status cancels sitting in the visual driver.

## Ring and environment

Use the supplied Ring visual or assign its sprite and **RF_Projectiles** sorting layer to the existing Frisbee prefab. Review scale and collider radius rather than assuming the new ring silhouette matches the original placeholder disc.

The Backyard prefab contains layered renderers and a tiled ground with a BoxCollider2D whose top is local Y=0. Keep the existing ground OR this ground as appropriate—do not unintentionally stack colliders. Its physics layer remains Default; choose the ground layer/mask expected by your controller. The RF sorting layers are only render ordering.

The original menu command **Ruby Frisbee → Setup Game Scene** rebuilds the placeholder scene/prefab. Do not rerun it over manual integration without first updating that original bootstrap or saving a separate integrated scene.

## Audio

Place one RubyAudioRig in the scene and reference it from game/UI code. Keep one enabled AudioListener, normally on the main camera. Use PlayCue with the audio-bank keys, and Gameplay/Menu/Paused snapshots with your actual game state. The package does not change Time.timeScale or pause gameplay by itself.

Animation clips expose `OnAnimationCue` events on RubyAnimationDriver. They are not wired automatically. Use either your gameplay sound call or an event listener for a given cue—not both—to avoid double playback.

## Acceptance checklist in Unity

- Import completes with no package compilation errors and a successful SetupReport.
- Preview scene: idle, run, jump, catch, sit/stand, celebration, game-over and recovery display correctly.
- Source sprite pivots/PPU and all atlas references are intact.
- Native AudioMixer opens with the nine-group hierarchy and three snapshots; preview cues route correctly.
- Existing gameplay still controls velocity, catches, scores and lives.
- Ruby's feet line up with ground; mouth/catch region mirrors correctly.
- No original placeholder ears/body remain visible.
- UI safe areas, texture compression, atlas memory and audio levels are tested on target desktop/phone hardware.

These integration and Play-mode checks were not performed in the packaging environment, which has no Unity Editor installed.
