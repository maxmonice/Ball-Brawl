# Generates the Assets/Audio/*.wav files procedurally (original synthesis, no third-party samples).
# Run from the project folder:  python Tools/gen_audio.py   (needs numpy)  -- edit and rerun to retune sounds.
import numpy as np, wave, os
sr = 44100; rng = np.random.default_rng(7); PI2 = 2 * np.pi
def T(d): return np.arange(int(sr * d)) / sr
def env(t, k): return np.exp(-np.clip(t, 0, None) * k)
def noise(t): return rng.standard_normal(len(t))
def lp(x, fc):
    a = 1 - np.exp(-PI2 * np.broadcast_to(np.asarray(fc, float), x.shape) / sr); y = np.empty_like(x); s = 0.0
    for i in range(len(x)): s += a[i] * (x[i] - s); y[i] = s
    return y
def hp(x, fc): return x - lp(x, fc)
def sweep(t, f0, f1, d): return np.sin(2 * np.pi * np.cumsum(f0 * (f1 / f0) ** (np.clip(t, 0, d) / d)) / sr)
def sine(t, f): return np.sin(PI2 * f * t)
def bursts(t, n, decay, lo=0.0, hi=1.0):          # electric crackle: random short bright noise bursts
    e = np.zeros(len(t))
    for _ in range(n):
        s = int(rng.uniform(lo, hi) * len(t) * .95); e[s:] += rng.uniform(.4, 1) * np.exp(-np.arange(len(t) - s) / sr * decay)
    return hp(noise(t), 2500) * np.minimum(e, 1.5)
def clang(t, base, k=6): return sum(sine(t, base * r) * env(t, k + i * 5) / (1 + i * .4) for i, r in enumerate([1, 2.32, 4.25, 6.63, 9.38])) + hp(noise(t), 3000) * env(t, 400)
def save(folder, name, x, peak=.9):
    x = x / (np.max(np.abs(x)) + 1e-9) * peak; f = min(len(x) // 20, 400); x[-f:] *= np.linspace(1, 0, f)
    p = os.path.join('Assets', 'Audio', folder); os.makedirs(p, exist_ok=True)
    with wave.open(os.path.join(p, name + '.wav'), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr); w.writeframes((x * 32767).astype('<i2').tobytes())

t = T(.18); save('Shared', 'collide', sweep(t, 200, 80, .18) * env(t, 25) + .6 * lp(noise(t), 3000) * env(t, 120))
t = T(.05); save('Shared', 'click', (sine(t, 1500) + .5 * sine(t, 2300)) * env(t, 90))
t = T(.9); save('Shared', 'match_start', sine(t, 523.25) * env(t, 5) + sine(t, 784) * env(t - .15, 5) * (t > .15))
t = T(1.4); save('Shared', 'match_end', sum(sine(t, f) * env(t - s, 3.5) * (t > s) for f, s in [(392, 0), (329.6, .22), (261.6, .44)]))
t = T(.35); save('Lightning', 'l_zap', bursts(t, 9, 300, 0, .5) + .5 * sweep(t, 3200, 700, .35) * env(t, 14))
t = T(.7); save('Lightning', 'l_zap_big', bursts(t, 18, 200, 0, .8) + .7 * sweep(t, 4200, 300, .7) * env(t, 6) + .8 * sine(t, 60) * env(t, 12))
t = T(1.3); rise = sweep(t, 100, 1400, .6) * (t < .6) * np.clip(t / .6, 0, 1); a = np.clip(t - .6, 0, None)
save('Lightning', 'l_ult_start', rise * .6 + (t >= .6) * (bursts(t, 30, 120, .45, .9) + .8 * lp(noise(t), 5000) * env(a, 9) + sine(t, 55) * env(a, 6)))
t = T(1.0); save('Lightning', 'l_hum', (sum(sine(t, 60 * k) / k for k in range(1, 9)) * (.6 + .4 * sine(t, 28)) * .5 + .4 * bursts(t, 6, 250)) * np.sin(np.pi * t) ** .5)
t = T(.9); save('Lightning', 'l_ult_end', sweep(t, 900, 70, .9) * env(t, 3) * .6 + bursts(t, 12, 200, 0, .6) * env(t, 3))
t = T(.2); save('Lightning', 'l_crackle', bursts(t, 5, 350, 0, .8))
t = T(.28); save('Sword', 's_whoosh', hp(lp(noise(t), 600 + 4400 * np.sin(np.pi * t / .28)), 400) * np.sin(np.pi * t / .28) ** 1.5)
t = T(.6); save('Sword', 's_clang', clang(t, 1250))
t = T(.9); save('Sword', 's_ult_hit', .8 * hp(noise(t), 1500) * env(t, 30) + .7 * clang(t, 900, 4) + sweep(t, 120, 45, .9) * env(t, 5))
t = T(.25); save('Sword', 's_sting', hp(noise(t), 3000) * (t / .25) ** 2 * (t < .2) + sine(t, 2400) * env(t, 18))
t = T(.35); save('Sword', 's_burst', lp(noise(t), 6000) * env(t, 25) + sweep(t, 260, 110, .35) * env(t, 12))
t = T(.8); save('Sword', 's_boom', sweep(t, 95, 35, .8) * env(t, 5) + .7 * lp(noise(t), 500) * env(t, 10) + .6 * hp(noise(t), 2000) * env(t, 200))
t = T(.7); save('Juggernaut', 'j_slam', sweep(t, 120, 38, .7) * env(t, 6) + .8 * lp(noise(t), 800) * env(t, 25) + .8 * sine(t, 45) * env(t, 9) + .2 * lp(noise(t), 300) * env(t, 4))
print('done')
