# Play Ruby's Frisbee on Your Phone (WebGL)

This guide walks you through building the game for the **mobile browser** — no App Store required. Open a link on your phone and play.

## Prerequisites

- Unity 6 with **WebGL Build Support** installed (Unity Hub → your install → Add modules)
- The game scene set up (`Ruby Frisbee → Setup Game Scene`)

## Step 1 — Build for WebGL

In Unity:

1. **Ruby Frisbee → Configure WebGL Build** (sets platform, scene, and hosting-friendly compression)
2. **Ruby Frisbee → Build WebGL**
3. Unity outputs to `RubyFrisbee/Builds/WebGL/` — upload the **entire folder**, not just `index.html`

Or manually:

1. **File → Build Settings**
2. Select **WebGL** → **Switch Platform**
3. Add `Assets/Scenes/GameScene.unity`
4. Under **Player Settings → WebGL**:
   - **Compression Format:** Disabled (simplest for drag-and-drop hosts)
   - **Default Canvas Width/Height:** 960 × 600 (scales on phone)
5. Click **Build** and choose an output folder

## Step 2 — Host the build

Pick one host and upload the full build folder.

### Option A: itch.io (easiest)

1. Create a free account at [itch.io](https://itch.io)
2. **Create new project** → set kind to **HTML**
3. Zip the contents of `Builds/WebGL/` (all files at the zip root)
4. Upload the zip, check **This file will be played in the browser**
5. Share the itch.io page link — open it on your phone

### Option B: Netlify (free, custom link)

1. Sign up at [netlify.com](https://netlify.com)
2. Drag the `Builds/WebGL/` folder onto the Netlify dashboard
3. You get a URL like `https://ruby-frisbee.netlify.app`
4. Open that URL on your phone

### Option C: Vercel (recommended if you already use Vercel)

Yes — Vercel works great for Unity WebGL, especially with **Compression Format: Disabled** (the project default). Vercel already serves `.wasm` with the correct MIME type; no extra config is required for uncompressed builds.

#### Quick deploy with Vercel CLI

1. Build the game: **Ruby Frisbee → Build WebGL**
2. Copy `deploy/vercel.json` into your build folder:
   ```bash
   cp deploy/vercel.json Builds/WebGL/
   ```
3. From the build folder, deploy:
   ```bash
   cd Builds/WebGL
   npx vercel
   ```
4. Follow the prompts (link to your Vercel account, confirm project name)
5. For production: `npx vercel --prod`
6. Open the URL on your phone

#### Deploy from the Vercel dashboard

1. Sign up at [vercel.com](https://vercel.com)
2. **Add New → Project**
3. Either:
   - Import a Git repo that contains the `Builds/WebGL` output, or
   - Use the CLI method above (simplest for Unity builds done locally)
4. Set **Root Directory** to where `index.html` lives (e.g. `RubyFrisbee/Builds/WebGL`)
5. Framework Preset: **Other** (no build command needed — it's static files)
6. Deploy and share the URL

#### Notes for Vercel

| Topic | Detail |
|-------|--------|
| Build output | Upload the **entire** `Builds/WebGL/` folder, not just `index.html` |
| Compression | Keep **Disabled** in Unity — avoids Safari double-compression issues on Vercel |
| File size | Free tier supports large static files; first load may take a few seconds |
| Custom domain | Add your own domain in Vercel project settings after deploy |
| Re-deploys | Rebuild in Unity, then run `npx vercel --prod` from `Builds/WebGL/` again |

If you enable Brotli/Gzip compression in Unity later, copy `deploy/vercel.json` and add `.br` / `.gz` header rules — see [Unity's WebGL deploy docs](https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-deploying.html).

### Option D: GitHub Pages

1. Put the build output in a `docs/` folder on your repo's default branch
2. Repo **Settings → Pages → Source:** Deploy from `/docs`
3. Your game will be at `https://<username>.github.io/<repo>/`

Note: GitHub Pages can be picky about Unity's `.wasm` and `.data` MIME types. If the game fails to load, use itch.io or Netlify instead.

## Step 3 — Play on your phone

1. Open the hosted link in **Safari** (iPhone) or **Chrome** (Android)
2. Tap **Play** on the menu
3. Use the on-screen **← →** and **Jump** buttons (they appear automatically on touch devices)
4. Optional: **Add to Home Screen** (iPhone: Share → Add to Home Screen) for an app-like icon

## Touch controls

On phones and touch browsers, controls show during gameplay:

| Button | Action |
|--------|--------|
| ← | Run left (hold) |
| → | Run right (hold) |
| Jump | Jump to catch frisbees |

Desktop keyboard controls still work when testing in a browser on a computer.

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Black screen after load | Rebuild with **Compression Format: Disabled** |
| Controls don't appear | Re-run **Setup Game Scene**; ensure you're in Play mode with touch or on a phone |
| Game is tiny on phone | Unity scales the canvas; rotate to landscape for more room |
| Build fails | Install WebGL module in Unity Hub; run Setup Game Scene first |
| Slow first load | Normal for WebGL — wait for the loading bar to finish |

## Rebuilding after changes

After code or art changes:

1. **Ruby Frisbee → Build WebGL**
2. Re-upload the new `Builds/WebGL/` folder to your host

---

Made with love for Ruby.
