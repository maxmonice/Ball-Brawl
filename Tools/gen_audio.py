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

# ---- newer characters ----
def seq(t, s): return np.clip(t - s, 0, None) * (t >= s)
t = T(.25); save('Inferno', 'i_hit', hp(lp(noise(t), 3500), 300) * env(t, 18) + bursts(t, 6, 200, 0, .6) * .5 + sweep(t, 300, 120, .25) * env(t, 14) * .6)
t = T(.3); save('Inferno', 'i_crackle', bursts(t, 10, 150, 0, .9) * .7 + lp(noise(t), 2000) * env(t, 12) * .3)
t = T(1.4); ex = (t >= .25) * 1.0; save('Inferno', 'i_ult', hp(noise(t), 800) * (t / .25) ** 2 * (t < .25) * .6 + ex * (sweep(t, 90, 30, 1.4) * env(t - .25, 2.2) + lp(noise(t), 1200) * env(t - .25, 2.5) + hp(noise(t), 2500) * env(t - .25, 15) * .8))
t = T(.25); save('Frost', 'f_hit', (sine(t, 2800) + .6 * sine(t, 4200) + .4 * sine(t, 5600)) * env(t, 25) + bursts(t, 4, 300) * .3)
t = T(.9); w = np.sin(np.pi * t / .9); save('Frost', 'f_freeze', hp(lp(noise(t), 500 + 3500 * w), 600) * w ** 1.2 + bursts(t, 16, 150, 0, 1) * .4)
t = T(.8); save('Frost', 'f_shatter', bursts(t, 40, 120, 0, .5) + clang(t, 3000, 10) * .6 + hp(noise(t), 4000) * env(t, 12))
t = T(.3); save('Void', 'v_hit', np.tanh(3 * sweep(t, 200, 70, .3) * env(t, 10)) + .3 * lp(noise(t), 800) * env(t, 20))
t = T(3.0); save('Void', 'v_hole', (sine(t, 55) + .5 * sine(t, 110.5)) * (.6 + .4 * sine(t, 3)) * np.sin(np.pi * t / 3) ** .3 + .35 * lp(noise(t), 200 + 1500 * t / 3) * np.sin(np.pi * t / 3))
t = T(1.0); save('Void', 'v_implode', hp(noise(t), 800) * (t / .4) ** 2 * (t < .4) * .5 + (t >= .4) * (sweep(t, 70, 28, 1.0) * env(t - .4, 4) + lp(noise(t), 400) * env(t - .4, 6)))
t = T(.22); save('Venom', 'n_hit', lp(noise(t), 2500) * env(t, 25) * (.5 + .5 * sine(t, 45)) + .5 * sweep(t, 300, 900, .22) * env(t, 40))
t = T(.5); save('Venom', 'n_bubble', sum(np.where(t >= s0, sweep(np.clip(t - s0, 0, .08), f0, f0 * 3, .08) * env(t - s0, 30), 0) for s0, f0 in [(0, 250), (.1, 320), (.22, 280), (.35, 400)]))
t = T(1.0); save('Venom', 'n_cloud', hp(noise(t), 3000) * env(t, 2.5) * (1 - env(t, 40)) * .6 + lp(noise(t), 1500) * env(t, 20))
t = T(.5); save('Solar', 'o_hit', sum(sine(t, f) * env(t - s0, 7) * (t >= s0) for f, s0 in [(1568, 0), (2093, .04), (2637, .08)]))
t = T(.8); save('Solar', 'o_burst', sweep(t, 400, 2000, .15) * (t < .15) + sum(sine(t, f) * env(t - .1, 5) * (t >= .1) for f in (1046, 1568, 2093)) + hp(noise(t), 3000) * env(t, 20) * .4)
t = T(1.5); save('Solar', 'o_flare', sweep(t, 150, 900, .6) * (t < .6) * np.clip(t / .6, 0, 1) + (t >= .6) * (sum(sine(t, f) * env(t - .6, 3) for f in (523, 784, 1047, 1568)) * .5 + lp(noise(t), 6000) * env(t - .6, 6) * .8 + sine(t, 70) * env(t - .6, 5)))
t = T(.3); save('Mecha', 'm_hit', clang(t, 500, 12) * .7 + lp(noise(t), 1500) * env(t, 60) * .5)
t = T(.6); save('Mecha', 'm_shield', sweep(t, 200, 600, .25) * (t < .25) + sine(t, 1800) * env(t - .25, 12) * (t >= .25) + .3 * sine(t, 900) * env(t, 8))
t = T(1.4); save('Mecha', 'm_over', sum(sweep(t, 100 * k, 700 * k, 1.2) / k for k in (1, 2, 3)) * (t < 1.2) * np.clip(t / .3, 0, 1) + clang(np.clip(t - 1.2, 0, None), 800, 8) * (t >= 1.2))
t = T(.4); save('Cosmic', 'c_hit', sweep(t, 500, 150, .4) * env(t, 8) + .3 * sine(t, 1200) * env(t, 15))
t = T(.5); save('Cosmic', 'c_boost', sweep(t, 300, 1800, .5) * np.sin(np.pi * t / .5))
t = T(1.2); save('Cosmic', 'c_cast', sum(sine(t, f) * env(t - s0, 5) * (t >= s0) for f, s0 in [(440, 0), (554, .15), (659, .3), (880, .45), (1108, .6)]) + sweep(t, 80, 40, 1.2) * env(t, 3) * .6)
t = T(.7); save('Cosmic', 'c_meteor', hp(lp(noise(t), 4000 * np.exp(-2.5 * t) + 300), 200) * (t / .7) ** 1.5)
t = T(.7); save('Cosmic', 'c_impact', sweep(t, 130, 40, .7) * env(t, 7) + lp(noise(t), 1500) * env(t, 15) * .9 + hp(noise(t), 2500) * env(t, 60) * .5)
t = T(.3); w = np.sin(np.pi * t / .3); save('Tempest', 't_hit', hp(lp(noise(t), 500 + 3500 * w), 300) * w)
t = T(.2); w = np.sin(np.pi * t / .2); save('Tempest', 't_tail', hp(lp(noise(t), 400 + 2500 * w), 300) * w)
t = T(1.4); save('Tempest', 't_cyclone', lp(noise(t), 300 + 2500 * t / 1.4) * (t / 1.4) ** 1.5 * (.7 + .3 * sine(t, 9)) + sine(t, 60) * (t / 1.4) * .3)
t = T(.9); save('Tempest', 't_gust', hp(lp(noise(t), 4000 * env(t, 3) + 300), 200) * np.sin(np.pi * np.clip(t / .9, 0, 1)) ** .5 + lp(noise(t), 800) * env(t, 5) * .5)
print('newer characters done')
