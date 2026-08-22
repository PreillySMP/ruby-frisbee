(() => {
  "use strict";

  const canvas = document.getElementById("game");
  const ctx = canvas.getContext("2d");

  const menuPanel = document.getElementById("menu");
  const gameOverPanel = document.getElementById("gameover");
  const hud = document.getElementById("hud");
  const controls = document.getElementById("controls");
  const scoreEl = document.getElementById("score");
  const livesEl = document.getElementById("lives");
  const comboEl = document.getElementById("combo");
  const menuHighEl = document.getElementById("menu-high");
  const finalScoreEl = document.getElementById("final-score");
  const gameOverHighEl = document.getElementById("gameover-high");

  const playBtn = document.getElementById("play-btn");
  const restartBtn = document.getElementById("restart-btn");
  const menuBtn = document.getElementById("menu-btn");
  const leftBtn = document.getElementById("left-btn");
  const rightBtn = document.getElementById("right-btn");
  const jumpBtn = document.getElementById("jump-btn");

  const HIGH_SCORE_KEY = "rubyFrisbeeHighScore";
  const GRAVITY = 900;
  const GROUND_RATIO = 0.82;

  let W = 0;
  let H = 0;
  let groundY = 0;

  const state = {
    mode: "menu",
    score: 0,
    combo: 0,
    lives: 3,
    highScore: Number(localStorage.getItem(HIGH_SCORE_KEY) || 0),
    spawnTimer: 0,
    elapsed: 0,
    keys: { left: false, right: false, jump: false },
    touch: { left: false, right: false, jumpQueued: false },
    frisbees: [],
    particles: [],
  };

  const ruby = {
    x: 0,
    y: 0,
    w: 72,
    h: 52,
    vx: 0,
    vy: 0,
    onGround: true,
    facing: 1,
    mouthX: 0,
    mouthY: 0,
    mouthR: 28,
  };

  function resize() {
    W = canvas.width = window.innerWidth;
    H = canvas.height = window.innerHeight;
    groundY = H * GROUND_RATIO;
    ruby.w = Math.max(56, W * 0.14);
    ruby.h = ruby.w * 0.72;
    ruby.mouthR = ruby.w * 0.38;
    if (state.mode === "menu") {
      ruby.x = W * 0.25;
      ruby.y = groundY - ruby.h;
    }
  }

  function resetSession() {
    state.score = 0;
    state.combo = 0;
    state.lives = 3;
    state.elapsed = 0;
    state.spawnTimer = 0.8;
    state.frisbees = [];
    state.particles = [];
    ruby.x = W * 0.25;
    ruby.y = groundY - ruby.h;
    ruby.vx = 0;
    ruby.vy = 0;
    ruby.onGround = true;
    updateHud();
  }

  function startGame() {
    state.mode = "playing";
    resetSession();
    menuPanel.classList.add("hidden");
    gameOverPanel.classList.add("hidden");
    hud.classList.remove("hidden");
    controls.classList.remove("hidden");
  }

  function endGame() {
    state.mode = "gameover";
    hud.classList.add("hidden");
    controls.classList.add("hidden");
    gameOverPanel.classList.remove("hidden");
    finalScoreEl.textContent = `Score: ${state.score}`;
    gameOverHighEl.textContent = `Best: ${state.highScore}`;
  }

  function returnToMenu() {
    state.mode = "menu";
    hud.classList.add("hidden");
    controls.classList.add("hidden");
    gameOverPanel.classList.add("hidden");
    menuPanel.classList.remove("hidden");
    menuHighEl.textContent = `High Score: ${state.highScore}`;
  }

  function updateHud() {
    scoreEl.textContent = `Score: ${state.score}`;
    livesEl.textContent = `Lives: ${state.lives}`;
    comboEl.textContent = state.combo > 1 ? `Combo x${state.combo}` : "";
  }

  function addScore(airCatch) {
    state.combo += 1;
    const multiplier = Math.min(5, 1 + Math.floor((state.combo - 1) * 0.25));
    let points = 100 * multiplier;
    if (airCatch) points += 50;
    state.score += points;
    if (state.score > state.highScore) {
      state.highScore = state.score;
      localStorage.setItem(HIGH_SCORE_KEY, String(state.highScore));
    }
    updateHud();
    spawnParticles(ruby.x + ruby.w * 0.6, ruby.y + ruby.h * 0.3, "#ffd54f", 8);
  }

  function missFrisbee() {
    state.combo = 0;
    state.lives -= 1;
    updateHud();
    spawnParticles(W * 0.5, groundY, "#888", 6);
    if (state.lives <= 0) endGame();
  }

  function spawnParticles(x, y, color, count) {
    for (let i = 0; i < count; i++) {
      state.particles.push({
        x,
        y,
        vx: (Math.random() - 0.5) * 220,
        vy: -Math.random() * 180 - 40,
        life: 0.4 + Math.random() * 0.3,
        color,
        size: 3 + Math.random() * 4,
      });
    }
  }

  function spawnFrisbee() {
    const speed = 260 + state.elapsed * 8;
    const startX = W + 40;
    const startY = groundY - 80 - Math.random() * (H * 0.25);
    const angle = Math.PI + 0.15 + Math.random() * 0.35;
    state.frisbees.push({
      x: startX,
      y: startY,
      vx: Math.cos(angle) * speed,
      vy: Math.sin(angle) * speed * -1,
      r: Math.max(16, W * 0.028),
      spin: 0,
    });
  }

  function getInput() {
    let move = 0;
    if (state.keys.left || state.touch.left) move -= 1;
    if (state.keys.right || state.touch.right) move += 1;

    const jump =
      state.keys.jump ||
      state.touch.jumpQueued ||
      state.touch.jumpQueued;

    if (state.touch.jumpQueued) state.touch.jumpQueued = false;
    if (state.keys.jump) state.keys.jump = false;

    return { move, jump };
  }

  function updateRuby(dt, move, jump) {
    const speed = W * 0.55;
    ruby.vx = move * speed;
    if (move !== 0) ruby.facing = move > 0 ? 1 : -1;

    if (jump && ruby.onGround) {
      ruby.vy = -H * 0.95;
      ruby.onGround = false;
      spawnParticles(ruby.x + ruby.w / 2, ruby.y + ruby.h, "#8bc34a", 4);
    }

    ruby.vy += GRAVITY * dt;
    ruby.x += ruby.vx * dt;
    ruby.y += ruby.vy * dt;

    const minX = ruby.w * 0.3;
    const maxX = W - ruby.w * 0.7;
    ruby.x = Math.max(minX, Math.min(maxX, ruby.x));

    if (ruby.y + ruby.h >= groundY) {
      ruby.y = groundY - ruby.h;
      ruby.vy = 0;
      ruby.onGround = true;
    } else {
      ruby.onGround = false;
    }

    ruby.mouthX = ruby.x + ruby.w * (ruby.facing > 0 ? 0.72 : 0.28);
    ruby.mouthY = ruby.y + ruby.h * 0.42;
  }

  function updateFrisbees(dt) {
    for (let i = state.frisbees.length - 1; i >= 0; i--) {
      const f = state.frisbees[i];
      f.vy += GRAVITY * 0.85 * dt;
      f.x += f.vx * dt;
      f.y += f.vy * dt;
      f.spin += dt * 8;

      const dx = f.x - ruby.mouthX;
      const dy = f.y - ruby.mouthY;
      const dist = Math.hypot(dx, dy);
      if (dist < ruby.mouthR + f.r * 0.5) {
        addScore(!ruby.onGround);
        state.frisbees.splice(i, 1);
        continue;
      }

      if (f.y - f.r > groundY + 4) {
        missFrisbee();
        state.frisbees.splice(i, 1);
        continue;
      }

      if (f.x < -80) {
        state.frisbees.splice(i, 1);
      }
    }
  }

  function updateParticles(dt) {
    for (let i = state.particles.length - 1; i >= 0; i--) {
      const p = state.particles[i];
      p.life -= dt;
      p.x += p.vx * dt;
      p.y += p.vy * dt;
      p.vy += 400 * dt;
      if (p.life <= 0) state.particles.splice(i, 1);
    }
  }

  function drawBackground() {
    const sky = ctx.createLinearGradient(0, 0, 0, H);
    sky.addColorStop(0, "#87ceeb");
    sky.addColorStop(1, "#b8e4f9");
    ctx.fillStyle = sky;
    ctx.fillRect(0, 0, W, H);

    ctx.fillStyle = "#4caf50";
    ctx.fillRect(0, groundY, W, H - groundY);

    ctx.fillStyle = "#388e3c";
    ctx.fillRect(0, groundY, W, 8);

    ctx.fillStyle = "rgba(139, 90, 43, 0.35)";
    const fenceY = groundY - H * 0.12;
    for (let x = 0; x < W; x += 40) {
      ctx.fillRect(x, fenceY, 8, H * 0.12);
    }
    ctx.fillRect(0, fenceY, W, 6);
  }

  function drawRuby() {
    const { x, y, w, h, facing } = ruby;
    const cx = x + w / 2;

    ctx.save();

    // Tail
    ctx.fillStyle = "#c4a35a";
    ctx.beginPath();
    ctx.ellipse(x + w * 0.1, y + h * 0.55, w * 0.12, h * 0.22, -0.4, 0, Math.PI * 2);
    ctx.fill();

    // Body
    ctx.fillStyle = "#c4a35a";
    ctx.beginPath();
    ctx.ellipse(cx, y + h * 0.55, w * 0.42, h * 0.38, 0, 0, Math.PI * 2);
    ctx.fill();

    // Black saddle
    ctx.fillStyle = "#1a1a1a";
    ctx.beginPath();
    ctx.ellipse(cx, y + h * 0.42, w * 0.3, h * 0.2, 0, 0, Math.PI * 2);
    ctx.fill();

    // Head
    ctx.fillStyle = "#c4a35a";
    ctx.beginPath();
    ctx.ellipse(x + w * (facing > 0 ? 0.68 : 0.32), y + h * 0.32, w * 0.22, h * 0.24, 0, 0, Math.PI * 2);
    ctx.fill();

    // Mask
    ctx.fillStyle = "#1a1a1a";
    ctx.beginPath();
    ctx.ellipse(x + w * (facing > 0 ? 0.74 : 0.26), y + h * 0.34, w * 0.14, h * 0.14, 0, 0, Math.PI * 2);
    ctx.fill();

    // Ears
    for (const side of [-1, 1]) {
      const earX = cx + side * w * 0.18;
      ctx.fillStyle = "#1a1a1a";
      ctx.beginPath();
      ctx.moveTo(earX, y + h * 0.08);
      ctx.lineTo(earX - w * 0.08 * side, y - h * 0.18);
      ctx.lineTo(earX + w * 0.08 * side, y + h * 0.02);
      ctx.closePath();
      ctx.fill();
    }

    // Tongue
    ctx.fillStyle = "#ff8a9b";
    ctx.beginPath();
    ctx.ellipse(x + w * (facing > 0 ? 0.78 : 0.22), y + h * 0.42, w * 0.06, h * 0.08, 0.3 * facing, 0, Math.PI * 2);
    ctx.fill();

    // Legs (simple)
    ctx.fillStyle = "#a8893f";
    const legY = y + h * 0.78;
    for (const lx of [0.28, 0.48, 0.62, 0.78]) {
      ctx.fillRect(x + w * lx - 4, legY, 8, h * 0.22);
    }

    ctx.restore();
  }

  function drawFrisbees() {
    for (const f of state.frisbees) {
      ctx.save();
      ctx.translate(f.x, f.y);
      ctx.rotate(f.spin);
      ctx.fillStyle = "#f57c00";
      ctx.beginPath();
      ctx.ellipse(0, 0, f.r, f.r * 0.35, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.strokeStyle = "#fff3e0";
      ctx.lineWidth = 2;
      ctx.stroke();
      ctx.restore();
    }
  }

  function drawParticles() {
    for (const p of state.particles) {
      ctx.globalAlpha = Math.max(0, p.life * 2);
      ctx.fillStyle = p.color;
      ctx.beginPath();
      ctx.arc(p.x, p.y, p.size, 0, Math.PI * 2);
      ctx.fill();
      ctx.globalAlpha = 1;
    }
  }

  function drawMenuRuby() {
    drawRuby();
    ctx.fillStyle = "rgba(0,0,0,0.25)";
    ctx.font = `600 ${Math.max(14, W * 0.035)}px system-ui`;
    ctx.textAlign = "center";
    ctx.fillText("Ruby is ready to play!", W / 2, groundY - H * 0.08);
  }

  let last = performance.now();

  function frame(now) {
    const dt = Math.min(0.033, (now - last) / 1000);
    last = now;

    drawBackground();

    if (state.mode === "playing") {
      state.elapsed += dt;
      state.spawnTimer -= dt;
      const interval = Math.max(0.7, 2.2 - state.elapsed * 0.012);
      if (state.spawnTimer <= 0) {
        spawnFrisbee();
        state.spawnTimer = interval;
      }

      const { move, jump } = getInput();
      updateRuby(dt, move, jump);
      updateFrisbees(dt);
      updateParticles(dt);
      drawFrisbees();
      drawRuby();
      drawParticles();
    } else {
      ruby.y = groundY - ruby.h + Math.sin(now / 400) * 4;
      drawRuby();
    }

    requestAnimationFrame(frame);
  }

  function bindTouch(btn, onDown, onUp) {
    const down = (e) => {
      e.preventDefault();
      onDown();
      btn.classList.add("active");
    };
    const up = (e) => {
      e.preventDefault();
      onUp();
      btn.classList.remove("active");
    };
    btn.addEventListener("touchstart", down, { passive: false });
    btn.addEventListener("touchend", up, { passive: false });
    btn.addEventListener("touchcancel", up, { passive: false });
    btn.addEventListener("mousedown", down);
    btn.addEventListener("mouseup", up);
    btn.addEventListener("mouseleave", up);
  }

  window.addEventListener("keydown", (e) => {
    if (e.key === "ArrowLeft" || e.key === "a") state.keys.left = true;
    if (e.key === "ArrowRight" || e.key === "d") state.keys.right = true;
    if (e.key === " " || e.key === "ArrowUp" || e.key === "w") state.keys.jump = true;
  });

  window.addEventListener("keyup", (e) => {
    if (e.key === "ArrowLeft" || e.key === "a") state.keys.left = false;
    if (e.key === "ArrowRight" || e.key === "d") state.keys.right = false;
  });

  playBtn.addEventListener("click", startGame);
  restartBtn.addEventListener("click", startGame);
  menuBtn.addEventListener("click", returnToMenu);

  bindTouch(
    leftBtn,
    () => { state.touch.left = true; },
    () => { state.touch.left = false; }
  );
  bindTouch(
    rightBtn,
    () => { state.touch.right = true; },
    () => { state.touch.right = false; }
  );
  bindTouch(
    jumpBtn,
    () => { state.touch.jumpQueued = true; },
    () => {}
  );

  window.addEventListener("resize", resize);
  resize();
  menuHighEl.textContent = `High Score: ${state.highScore}`;
  requestAnimationFrame(frame);
})();
