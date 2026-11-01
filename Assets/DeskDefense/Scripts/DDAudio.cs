using UnityEngine;

namespace DeskDefense
{
    /// <summary>Placeholder sounds synthesized at startup; small AudioSource pool.</summary>
    public class DDAudio : MonoBehaviour
    {
        const int Rate = 44100;
        static readonly float[] notes = { 523f, 659f, 784f, 1046f };
        // Background loop: 16 eighth notes at 0.25 s = 4 s (0 = rest), bass changes every 2 beats.
        static readonly float[] melody = { 523f, 0f, 659f, 784f, 0f, 659f, 587f, 0f, 523f, 0f, 440f, 523f, 0f, 587f, 659f, 0f };
        static readonly float[] bass = { 131f, 131f, 110f, 110f, 87f, 87f, 98f, 98f };
        AudioSource[] sources;
        const float MusicVolume = 0.22f;
        AudioSource loop, music;
        int next;
        AudioClip pop, clink, win, lose, click, hurt, engine, hum, step, ding, door, key, tune;

        void Awake()
        {
            sources = new AudioSource[6];
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
            }
            loop = gameObject.AddComponent<AudioSource>();
            loop.playOnAwake = false;
            loop.loop = true;
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.volume = MusicVolume;

            // Loops use whole-number frequencies over whole seconds so they wrap without a click.
            engine = Make("engine", 1f, (t, d) =>
            {
                float wob = 0.75f + 0.25f * Mathf.Sin(t * 12f * 6.283f);
                float saw = (t * 70f) % 1f * 2f - 1f;
                return (saw * 0.5f + Mathf.Sin(t * 140f * 6.283f) * 0.3f) * wob;
            }, true);
            hum = Make("hum", 1f, (t, d) => Mathf.Sin(t * 55f * 6.283f) * 0.5f + Mathf.Sin(t * 110f * 6.283f) * 0.2f, true);
            tune = Make("tune", 4f, (t, d) =>
            {
                int n = Mathf.Min(15, (int)(t / 0.25f));
                float nt = t - n * 0.25f;
                float v = 0f;
                if (melody[n] > 0f)
                    v += (Mathf.Sin(t * melody[n] * 6.283f) + 0.3f * Mathf.Sin(t * melody[n] * 2f * 6.283f)) * Mathf.Exp(-nt * 9f) * 0.45f;
                int b = Mathf.Min(7, (int)(t / 0.5f));
                float bt = t - b * 0.5f;
                v += Mathf.Sin(t * bass[b] * 6.283f) * Mathf.Exp(-bt * 4f) * (1f - bt / 0.5f) * 0.4f;
                return v;
            }, true);
            step = Make("step", 0.07f, (t, d) => (Mathf.Sin(t * 130f * 6.283f) * 0.7f + Noise() * 0.3f) * Mathf.Exp(-t * 60f));
            ding = Make("ding", 0.9f, (t, d) =>
            {
                float f = t < 0.3f ? 1318f : 1046f;
                float lt = t < 0.3f ? t : t - 0.3f;
                return (Mathf.Sin(t * f * 6.283f) + 0.4f * Mathf.Sin(t * f * 2.76f * 6.283f)) * Mathf.Exp(-lt * 5f) * 0.5f;
            });
            door = Make("door", 0.45f, (t, d) => Noise() * Mathf.Sin(t / d * 3.1416f) * 0.35f);
            key = Make("key", 0.035f, (t, d) => (Noise() * 0.5f + Mathf.Sin(t * 2600f * 6.283f) * 0.5f) * Mathf.Exp(-t * 140f));
            pop = Make("pop", 0.14f, (t, d) => Sweep(t, 950f, 260f, d) * Mathf.Exp(-t * 28f) + Noise() * Mathf.Exp(-t * 90f) * 0.4f);
            clink = Make("clink", 0.12f, (t, d) => (Mathf.Sin(t * 2400f * 6.283f) * 0.6f + Mathf.Sin(t * 3700f * 6.283f) * 0.4f) * Mathf.Exp(-t * 40f));
            click = Make("click", 0.06f, (t, d) => Mathf.Sin(t * 1200f * 6.283f) * Mathf.Exp(-t * 70f));
            hurt = Make("hurt", 0.25f, (t, d) => Mathf.Sign(Mathf.Sin(t * 110f * 6.283f)) * 0.35f * (1f - t / d));
            win = Make("win", 0.9f, (t, d) =>
            {
                int i = Mathf.Min(3, (int)(t / 0.15f));
                float lt = t - i * 0.15f;
                return Mathf.Sin(t * notes[i] * 6.283f) * Mathf.Exp(-lt * (i == 3 ? 4f : 12f)) * 0.7f;
            });
            lose = Make("lose", 0.9f, (t, d) =>
            {
                float f = Mathf.Lerp(420f, 90f, t / d);
                float saw = (t * f) % 1f * 2f - 1f;
                return saw * 0.45f * (1f - t / d);
            });
        }

        static float Noise() { return Random.value * 2f - 1f; }

        static float Sweep(float t, float f0, float f1, float d)
        {
            // phase of a linear chirp
            float k = (f1 - f0) / d;
            return Mathf.Sin(6.283f * (f0 * t + 0.5f * k * t * t));
        }

        static AudioClip Make(string name, float dur, System.Func<float, float, float> f, bool looping = false)
        {
            int n = (int)(dur * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float fade = looping ? 1f : Mathf.Min(1f, (n - i) / 200f);
                data[i] = Mathf.Clamp(f(t, dur), -1f, 1f) * fade * 0.8f;
            }
            var clip = AudioClip.Create("DD_" + name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void Play(AudioClip clip, float pitch, float vol)
        {
            AudioSource s = sources[next];
            next = (next + 1) % sources.Length;
            s.pitch = pitch;
            s.volume = vol;
            s.clip = clip;
            s.Play();
        }

        public void Pop(float pitch) { Play(pop, pitch, 0.9f); }
        public void Clink() { Play(clink, Random.Range(0.95f, 1.1f), 0.7f); }
        public void Click() { Play(click, 1f, 0.6f); }
        public void Hurt() { Play(hurt, 1f, 0.5f); }
        public void Win() { Play(win, 1f, 0.9f); }
        public void Lose() { Play(lose, 1f, 0.9f); }
        public void Step() { Play(step, Random.Range(0.9f, 1.1f), 0.45f); }
        public void Ding() { Play(ding, 1f, 0.7f); }
        public void Door() { Play(door, Random.Range(0.9f, 1.1f), 0.6f); }
        public void Key() { Play(key, Random.Range(0.85f, 1.2f), 0.22f); }

        public void EngineOn() { LoopOn(engine, 0.35f); }
        public void LiftHumOn() { LoopOn(hum, 0.3f); }
        public void LoopOff() { loop.Stop(); }

        void LoopOn(AudioClip clip, float vol)
        {
            if (loop.clip == clip && loop.isPlaying) return;
            loop.clip = clip;
            loop.volume = vol;
            loop.Play();
        }

        public void MusicOn() { if (!music.isPlaying) { music.clip = tune; music.Play(); } }
        public void MusicOff() { music.Stop(); }
    }
}
