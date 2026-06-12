using System.Collections.Generic;
using UnityEngine;

namespace BistroBurrow.Util
{
    /// <summary>
    /// 程序化音效合成器：运行时生成 PCM AudioClip，零音频资产、零包体成本。
    /// 走 AudioClip.Create + SetData（WebGL 的 Web Audio 后端支持运行时 PCM）。
    /// 风格：柔和的"小酒馆电子木琴"短音，避免刺耳方波。
    /// </summary>
    public static class SfxSynth
    {
        public enum Id
        {
            Click,    // UI/开火确认
            Serve,    // 上菜叮~
            Coin,     // 收钱（双音上行）
            Hit,      // 攻击命中（短促低音）
            Hurt,     // 玩家受击（下行）
            Pickup,   // 拾取（轻快泡泡音）
            Unlock,   // 解锁新菜谱（三音琶音）
            DayStart  // 开店铃
        }

        const int SampleRate = 22050; // 音效足够，内存减半
        static readonly Dictionary<Id, AudioClip> Cache = new();
        static AudioSource _source;

        static AudioSource Source
        {
            get
            {
                if (_source == null)
                {
                    var go = new GameObject("~Sfx");
                    go.hideFlags = HideFlags.HideInHierarchy;
                    Object.DontDestroyOnLoad(go);
                    _source = go.AddComponent<AudioSource>();
                    _source.playOnAwake = false;
                    _source.spatialBlend = 0f; // 2D
                }
                return _source;
            }
        }

        /// <summary>播放音效（PlayOneShot 可叠音，不打断前一个）。</summary>
        public static void Play(Id id, float volume = 0.5f)
        {
            AudioClip clip = GetClip(id);
            if (clip != null) Source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        static AudioClip GetClip(Id id)
        {
            if (Cache.TryGetValue(id, out AudioClip cached)) return cached;
            AudioClip clip = Synthesize(id);
            Cache[id] = clip;
            return clip;
        }

        static AudioClip Synthesize(Id id)
        {
            switch (id)
            {
                case Id.Click: return Tone(new[] { (740f, 0.00f, 0.07f) }, 0.09f);
                case Id.Serve: return Tone(new[] { (1047f, 0.00f, 0.16f), (1568f, 0.06f, 0.18f) }, 0.30f);
                case Id.Coin: return Tone(new[] { (988f, 0.00f, 0.08f), (1319f, 0.07f, 0.14f) }, 0.24f);
                case Id.Hit: return Noise(0.08f, 220f);
                case Id.Hurt: return Tone(new[] { (392f, 0.00f, 0.10f), (262f, 0.08f, 0.14f) }, 0.26f);
                case Id.Pickup: return Tone(new[] { (880f, 0.00f, 0.06f), (1175f, 0.05f, 0.08f) }, 0.16f);
                case Id.Unlock: return Tone(new[] { (784f, 0.00f, 0.12f), (988f, 0.09f, 0.12f), (1319f, 0.18f, 0.22f) }, 0.45f);
                case Id.DayStart: return Tone(new[] { (659f, 0.00f, 0.14f), (831f, 0.10f, 0.18f) }, 0.32f);
                default: return Tone(new[] { (440f, 0.00f, 0.1f) }, 0.12f);
            }
        }

        /// <summary>多音叠加合成：每个条目为 (频率Hz, 起始秒, 时长秒)，正弦+指数衰减包络。</summary>
        static AudioClip Tone((float freq, float start, float dur)[] notes, float totalSeconds)
        {
            int total = Mathf.CeilToInt(totalSeconds * SampleRate);
            var data = new float[total];
            foreach ((float freq, float start, float dur) in notes)
            {
                int s0 = Mathf.FloorToInt(start * SampleRate);
                int len = Mathf.Min(Mathf.CeilToInt(dur * SampleRate), total - s0);
                for (int i = 0; i < len; i++)
                {
                    float t = (float)i / SampleRate;
                    float env = Mathf.Exp(-t * 9f) * Mathf.Min(1f, i / (SampleRate * 0.004f)); // 4ms 起音防爆音
                    // 基波 + 轻微二次谐波，让音色更"木"
                    float v = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.8f
                            + Mathf.Sin(4f * Mathf.PI * freq * t) * 0.2f;
                    data[s0 + i] += v * env * 0.5f;
                }
            }
            return Bake("tone", data);
        }

        /// <summary>滤波噪声打击音（命中用）：白噪 + 低频正弦体感。</summary>
        static AudioClip Noise(float seconds, float bodyFreq)
        {
            int total = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[total];
            var rng = new System.Random(12345); // 固定种子保证每次合成一致
            float last = 0f;
            for (int i = 0; i < total; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 28f);
                float white = (float)(rng.NextDouble() * 2 - 1);
                last = Mathf.Lerp(last, white, 0.25f); // 一阶低通，闷一点
                data[i] = (last * 0.6f + Mathf.Sin(2f * Mathf.PI * bodyFreq * t) * 0.4f) * env * 0.6f;
            }
            return Bake("noise", data);
        }

        static AudioClip Bake(string name, float[] data)
        {
            // 防爆音兜底：全局软限幅
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i], -0.95f, 0.95f);
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
