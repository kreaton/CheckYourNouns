// Synthesized sounds, the saved score, and the physical keyboard.
window.gameSounds = (() => {
    let ctx = null;
    let master = null;

    function audio() {
        if (!ctx) {
            const Ctx = window.AudioContext || window.webkitAudioContext;
            if (!Ctx) return null;
            ctx = new Ctx();
            const comp = ctx.createDynamicsCompressor();
            comp.threshold.value = -14;
            comp.ratio.value = 5;
            master = ctx.createGain();
            master.gain.value = 0.8;
            master.connect(comp).connect(ctx.destination);
        }
        if (ctx.state === "suspended") ctx.resume();
        return ctx;
    }

    // iOS only lets audio start inside a user gesture, so wake the context on the first tap.
    function unlock() {
        const c = audio();
        if (!c) return;
        const source = c.createBufferSource();
        source.buffer = c.createBuffer(1, 1, 22050);
        source.connect(c.destination);
        source.start(0);
        window.removeEventListener("pointerdown", unlock);
        window.removeEventListener("keydown", unlock);
    }
    window.addEventListener("pointerdown", unlock);
    window.addEventListener("keydown", unlock);

    // A soft electric-piano-like tone: a sine with a quiet overtone and a quick decay.
    function tone(c, { freq, start = 0, length = 0.4, volume = 0.2, type = "sine", slideTo = null }) {
        const t = c.currentTime + start;
        const gain = c.createGain();
        gain.gain.setValueAtTime(0.0001, t);
        gain.gain.exponentialRampToValueAtTime(volume, t + 0.008);
        gain.gain.exponentialRampToValueAtTime(0.0001, t + length);
        gain.connect(master);

        for (const [mult, level] of [[1, 1], [2, 0.25]]) {
            const osc = c.createOscillator();
            const g = c.createGain();
            osc.type = type;
            osc.frequency.setValueAtTime(freq * mult, t);
            if (slideTo) osc.frequency.exponentialRampToValueAtTime(slideTo * mult, t + length);
            g.gain.value = level;
            osc.connect(g).connect(gain);
            osc.start(t);
            osc.stop(t + length + 0.05);
        }
    }

    return {
        // A rising major arpeggio.
        success() {
            const c = audio();
            if (!c) return;
            [523.25, 659.25, 783.99, 1046.5].forEach((freq, i) =>
                tone(c, { freq, start: i * 0.075, length: 0.6, volume: 0.16 }));
        },

        // A short muted buzz, quick because wrong letters happen a lot.
        error() {
            const c = audio();
            if (!c) return;
            tone(c, { freq: 155, slideTo: 120, length: 0.18, volume: 0.22, type: "triangle" });
        },

        // Time's up: a falling "wah-wah", longer and lower than a wrong letter.
        timeout() {
            const c = audio();
            if (!c) return;
            tone(c, { freq: 392, slideTo: 349.2, length: 0.32, volume: 0.2, type: "triangle" });
            tone(c, { freq: 311.1, slideTo: 196, start: 0.3, length: 0.7, volume: 0.22, type: "triangle" });
        },

        // A soft clock tick for each of the last seconds.
        tick() {
            const c = audio();
            if (!c) return;
            tone(c, { freq: 880, length: 0.06, volume: 0.08, type: "square" });
        },

        // A quiet tick for each right letter.
        letter() {
            const c = audio();
            if (!c) return;
            tone(c, { freq: 1318.5, length: 0.09, volume: 0.06 });
        },
    };
})();

window.gameStore = {
    load(key) {
        try { return localStorage.getItem(key); } catch { return null; }
    },
    save(key, value) {
        try { localStorage.setItem(key, value); } catch { /* private mode: score is not kept */ }
    },
};

// Forwards letters, ?, 1-3 (gender) and Enter from the physical keyboard to the game.
window.gameKeys = {
    attach(dotnet) {
        this.detach();
        this.handler = e => {
            if (e.ctrlKey || e.metaKey || e.altKey || e.repeat) return;
            // Leave typing in the language pickers alone.
            if (e.target instanceof Element && e.target.closest("select, input, textarea")) return;
            const key = e.key;
            if (key.length === 1 && /\p{L}|[?1-3]/u.test(key) || key === "Enter") {
                e.preventDefault();
                dotnet.invokeMethodAsync("OnKey", key);
            }
        };
        document.addEventListener("keydown", this.handler);
    },
    detach() {
        if (this.handler) document.removeEventListener("keydown", this.handler);
        this.handler = null;
    },
};
