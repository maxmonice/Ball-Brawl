using NAudio.Wave;
using NAudio.Wave.SampleProviders;
namespace BallBrawl;

// One playing instance of a cached sound (linear-interpolated resampling gives per-play pitch variation).
sealed class Voice : ISampleProvider
{
    readonly float[] d; readonly float vol, rate; float pos; bool done;
    public Voice(float[] data, float volume, float rate) { d = data; vol = volume; this.rate = rate; }
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
    public int Read(float[] buf, int off, int count)
    {
        int n = 0;
        while (n + 1 < count) { int i = (int)pos; if (i >= d.Length - 1) break; float f = pos - i, v = (d[i] * (1 - f) + d[i + 1] * f) * vol; buf[off + n++] = v; buf[off + n++] = v; pos += rate; }
        if (n < count && !done) { done = true; Interlocked.Decrement(ref Audio.Active); }   // mixer drops a source on its first short read
        return n;
    }
}

// Reusable audio manager: sounds are decoded once into memory and mixed on NAudio's own playback thread,
// so playing never blocks the UI. Missing files fall back to a short synthesized blip.
static class Audio
{
    public static float Master = .8f, Effects = 1f, Scale = 1f;   // Scale: per-source loudness (menu background battle is quieter)
    internal static int Active; const int MaxVoices = 24;
    static readonly Dictionary<string, float[]> bank = new(); static readonly Dictionary<string, long> last = new();
    static readonly List<(float t, string n, float v, float p, int g)> pending = new(); static readonly Random rng = new();
    static MixingSampleProvider mixer; static WaveOutEvent output; static bool ok; static float amb;
    static readonly (float t, string n, float v)[] cues = { (.12f, "s_sting", .8f), (.24f, "s_sting", .9f), (.30f, "s_burst", 1f), (.45f, "s_boom", 1f) };

    public static void Init(string dir)
    {
        try
        {
            if (Directory.Exists(dir)) foreach (var f in Directory.GetFiles(dir, "*.wav", SearchOption.AllDirectories)) try { bank[Path.GetFileNameWithoutExtension(f)] = Load(f); } catch { }
            mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2)) { ReadFully = true };
            output = new WaveOutEvent { DesiredLatency = 80 }; output.Init(mixer); output.Play(); ok = true;
        }
        catch { ok = false; }   // no audio device: game stays fully playable, silently
    }
    public static void Shutdown() { try { output?.Dispose(); } catch { } ok = false; }

    static float[] Load(string path)
    {
        using var r = new AudioFileReader(path); ISampleProvider sp = r; int ch = r.WaveFormat.Channels;
        if (r.WaveFormat.SampleRate != 44100) sp = new WdlResamplingSampleProvider(sp, 44100);
        var all = new List<float>(); var buf = new float[4096]; int n;
        while ((n = sp.Read(buf, 0, buf.Length)) > 0) for (int i = 0; i + ch <= n; i += ch) { float v = 0; for (int c = 0; c < ch; c++) v += buf[i + c]; all.Add(v / ch); }
        return all.ToArray();
    }
    static float[] Get(string name)
    {
        if (bank.TryGetValue(name, out var d)) return d;
        float f = 200 + name.Sum(c => c) % 700; d = new float[5292];   // fallback blip (0.12s)
        for (int i = 0; i < d.Length; i++) d[i] = MathF.Sin(2 * MathF.PI * f * i / 44100) * MathF.Exp(-i / 44100f * 30) * .5f;
        return bank[name] = d;
    }

    public static void Play(string name, float vol = 1, float pitchVar = .08f, float delay = 0, int gapMs = 45)
    {
        if (!ok) return;
        if (delay > 0) { pending.Add((delay, name, vol, pitchVar, gapMs)); return; }
        long now = Environment.TickCount64; if (last.TryGetValue(name, out var l) && now - l < gapMs) return;   // anti-spam
        float v = vol * Master * Effects * Scale; if (v < .001f || Volatile.Read(ref Active) >= MaxVoices) return;
        last[name] = now; Interlocked.Increment(ref Active);
        mixer.AddMixerInput(new Voice(Get(name), v, 1 + (float)(rng.NextDouble() * 2 - 1) * pitchVar));
    }
    public static void Update(float dt)   // fires delayed sounds; called from the game timer, never sleeps
    {
        for (int i = pending.Count - 1; i >= 0; i--) { var p = pending[i]; p.t -= dt; if (p.t > 0) { pending[i] = p; continue; } pending.RemoveAt(i); Play(p.n, p.v, p.p, 0, p.g); }
    }

    public static void OnEvent(GameEvent e)
    {
        if (e.T == 1) { Play("l_ult_start", 1f, .02f, 0, 200); Play("l_hum", .45f, .03f, .2f, 300); return; }
        if (e.T == 2) { Play("l_ult_end", .8f, .03f, 0, 200); return; }
        switch (e.K)
        {
            case Kind.Lightning: Play("collide", .45f, .1f, 0, 70); Play(e.Ult ? "l_zap_big" : "l_zap", .9f); break;
            case Kind.Sword:
                Play("s_whoosh", .7f, .12f); Play("s_clang", .9f, .1f, .07f);
                if (e.Ult) Play("s_ult_hit", 1f, .02f, 0, 300); break;
            default: Play("collide", .5f, .06f, 0, 70); Play("j_slam", 1f, .05f, 0, 90); break;
        }
    }
    public static void Cine(float prev, float cur) { foreach (var c in cues) if (prev < c.t && cur >= c.t) Play(c.n, c.v, .02f, 0, 40); }   // synced to the ultimate's phases
    public static void Ambient(Sim s, float dt)   // sparse crackle ticks while Overcharge is active (no constant loop)
    {
        if (dt <= 0) return;
        foreach (var b in s.Balls) if (b.Alive && b.D.K == Kind.Lightning && b.UltT > 0 && (amb -= dt) <= 0) { amb = .25f + (float)rng.NextDouble() * .3f; Play("l_crackle", .35f, .15f, 0, 90); }
    }
}