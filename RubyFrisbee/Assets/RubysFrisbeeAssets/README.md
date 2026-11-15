# Ruby's Frisbee — Unity Asset Package 1.0

**Target:** Unity 6000.0.34f1 (Unity 6.0), Built-in Render Pipeline / 2D.

This is an **auto-configuring .unitypackage**. It contains the runtime artwork/audio, a native AudioMixer, complete animation/controller/atlas definitions and Editor setup code. After import and script compilation, Unity creates the native `.anim`, `.controller`, `.spriteatlas`, prefabs and preview scene using its own public asset APIs. These generated assets are not pre-baked by a sandbox without Unity.

## Import

1. Commit or back up your Unity project first. Setup intentionally adds eight sorting layers.
2. In Unity choose **Assets → Import Package → Custom Package…**, select `RubysFrisbee_Unity6_Assets_v1.unitypackage`, and import all files.
3. Wait for import and compilation to finish. Setup runs once automatically after the Editor becomes idle. It may take a few minutes to import the sprite frames and build native assets.
4. Look for **Ruby setup completed** in the Console. Details are saved to `Assets/RubysFrisbeeAssets/Generated/SetupReport.json`.
5. Open `Generated/Scenes/RubyAssetPreview.unity` as its own scene, press Play, and use the visual/audio/FX controls. The controls can be hidden for an unobstructed view.

If setup did not run, fix any project compilation errors first, then choose **Tools → Ruby's Frisbee → Setup or Repair**. **Validate Package** performs a read-only check. Failed setup writes errors and does not stamp success.

**Validation limit:** No Unity Editor is installed in the package-build workspace. The package received offline C# syntax, data/reference, mixer-graph and archive checks—not a real Unity import, C# API compilation, native atlas packing or Play-mode test. Verify the Console/setup report and preview scene in your Editor before relying on the package in production.

## Generated content

- **25 AnimationClips**, preserving the supplied frame timings and pivots.
- **Ruby.controller:** 15 named states with locomotion, airborne, catch, sit/stand and reaction transitions.
- **7 effect controllers and prefabs**, including one-shot automatic disabling and a looping ring trail.
- **8 native SpriteAtlas assets:** run, idle, jump, sit/stand, reactions, UI, effects and props. Rotation/tight packing disabled, padding 4, included in builds. Unity performs target-specific atlas packing.
- **RubyVisual, Ring, Backyard and RubyAudioRig prefabs.**
- **RubyAudioBank.asset**, referencing 14 processed cues/loops and the proper mixer buses.
- **RubyAssetPreview.unity**, a non-gameplay asset tester.

The source set contains **425 configured textures** and **14 WAV audio files**. Artwork is copied unchanged from the asset library. No rejected procedural run revision or cropped draft characters are included.

Generated paths are under `Assets/RubysFrisbeeAssets/Generated/`. Customize copies outside `Generated`; repair intentionally rebuilds package-owned definitions. Main generated asset GUIDs are preserved where practical, while controller subassets are rebuilt. Existing preview scenes are preserved, not overwritten automatically.

## Sorting layers

Back to front:

1. RF_Background
2. RF_Midground
3. RF_Ground
4. RF_Props
5. RF_Character
6. RF_Projectiles
7. RF_Effects
8. RF_UI

Missing names are appended to the project's sorting-layer list. Existing names, unrelated sorting layers, tags and physics layers are not replaced. If your project already has RF layers in another order, review that order manually. Sorting layers are not collision layers.

## Animation control

Use `RubysFrisbee.Assets.RubyAnimationDriver` on the supplied RubyVisual prefab:

```csharp
visual.SetLocomotion(Mathf.Abs(body.linearVelocity.x), body.linearVelocity.y, grounded);
visual.SetFacing(facingRight);
visual.SetSitting(true);       // false requests stand-up
visual.TriggerCatch();        // selects grounded or airborne catch using IsGrounded
visual.TriggerCelebrate();
visual.SetGameOver(true);     // edge-triggered: repeated true does not restart the clip
visual.Recover();             // clear game-over and play recovery
```

The controller parameters are MoveSpeed, VerticalSpeed, IsGrounded, IsSitting, IsGameOver, Catch, Celebrate, GameOver and Recover. The last four action names are triggers; IsGameOver is the boolean gate. Use the helper methods so gates and triggers stay coordinated.

Sprite transitions use zero blend duration. Jump poses do not apply baked root motion; your Rigidbody/controller owns movement. Rising/falling poses can hold while waiting for physics. Landing includes its recovery drawing range. Moving or becoming airborne cancels the sitting flag. Animation cue events are exposed but do not mutate score or game state and are not automatically wired to audio, avoiding duplicate sounds with existing gameplay code.

The original selected run prototype is intentionally preserved, including its previously documented gait/seam limitations. This import package does not claim to repair that artwork.

## Mixer presets and sound

`Audio/RubyAudio.mixer` is supplied as a native mixer with:

- User-volume groups: Master, Music, SFX, UI and Ambience.
- Separate leaf mix groups for snapshot gain changes.
- Exposed controls: MasterVolumeDb, MusicVolumeDb, SfxVolumeDb, UiVolumeDb and AmbienceVolumeDb.
- Snapshots: **Gameplay**, **Menu**, **Paused**.

| Snapshot | Music Mix | SFX Mix | UI Mix | Ambience Mix |
|---|---:|---:|---:|---:|
| Gameplay | −10 dB | 0 dB | −3 dB | −18 dB |
| Menu | −6 dB | −8 dB | −2 dB | −24 dB |
| Paused | −18 dB | −80 dB | −4 dB | −30 dB |

Snapshot gain changes live on child mix groups so exposed parent-volume preferences can coexist with them. `RubyAudioRig` has separate 2D Music, Ambience, SFX and UI sources. Music and the optional stream ambience do not autoplay.

```csharp
audioRig.PlayCue("catch");
audioRig.PlayCue("ui_confirm");
audioRig.PlayMusic();
audioRig.ApplySnapshot("Paused", 0.2f);
audioRig.SetMasterVolume01(0.8f);
```

Effects use decompressed PCM review defaults; music/ambience stream as Vorbis with source sample rates preserved. Audition compression and loop joins in Unity. The optional ambience is a stream/water texture, not a default dry-backyard bed. The bark is synthetic generic dog audio, not Ruby's recorded voice.

## Using this in the existing RubyFrisbee project

See `Documentation/ExistingGameIntegration.md`. The package does not modify your existing GameScene, RubyController, GameManager, spawner or original scene-bootstrap script. It also does not install a second Rigidbody/player-input handler. Existing game logic must feed the visual driver and call audio/effect cues at the appropriate events.

Do not rename the `Assets/RubysFrisbeeAssets` root before setup. Atlas definitions use package-relative source paths. Some large source UI/environment images remain separate sprites; the repeated ground is deliberately not packed into an atlas.

## Memory and platform notes

Defaults prioritize clean 2D edges: full-rectangle sprites, alpha transparency, no mipmaps, bilinear filtering and uncompressed review textures. Atlas page counts and memory depend on Unity's target-specific packer. Profile on actual devices and select compression/atlas sizes appropriate to your build. Check the original asset-library notes for source-native contour variation and state-transition limitations.

## Optional batch validation

After importing the package into a licensed Unity project:

```text
Unity -batchmode -projectPath "/path/to/project" -executeMethod RubysFrisbee.Assets.Editor.RubyPackageBatch.BuildAndValidate -logFile "/path/to/ruby-import.log"
```

The batch entry point exits 0 only after setup/native-reference validation reports success; otherwise it exits 1. It is not a replacement for Play-mode or gameplay testing.
