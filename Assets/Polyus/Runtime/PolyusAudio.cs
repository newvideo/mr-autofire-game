using UnityEngine;

namespace Polyus
{
    public sealed class PolyusAudio : MonoBehaviour
    {
        private AudioSource effects, music;
        private AudioClip shot, hit, explosion, flip, click, reward;
        private float lastShot;
        public bool Muted { get; private set; }
        private void Awake()
        {
            effects = gameObject.AddComponent<AudioSource>();
            music = gameObject.AddComponent<AudioSource>();
            effects.volume = .28f;
            music.volume = .16f;
            music.loop = true;
            music.clip = Resources.Load<AudioClip>("Polyus/Audio/ambient");
            shot = Load("shot"); hit = Load("hit"); explosion = Load("explosion");
            flip = Load("flip"); click = Load("click"); reward = Load("reward");
            Muted = PlayerPrefs.GetInt("Polyus.Muted", 0) == 1;
            SetMute();
            if (music.clip != null) music.Play();
        }
        private AudioClip Load(string name) => Resources.Load<AudioClip>("Polyus/Audio/" + name);
        private void Play(AudioClip clip, float volume = 1) { if (clip != null) effects.PlayOneShot(clip, volume); }
        public void Shot() { if (Time.unscaledTime - lastShot < .085f) return; lastShot = Time.unscaledTime; Play(shot, .55f); }
        public void Hit() => Play(hit, .7f);
        public void Explosion() => Play(explosion);
        public void Flip() => Play(flip);
        public void Click() => Play(click, .6f);
        public void Reward() => Play(reward);
        public void ToggleMute() { Muted = !Muted; PlayerPrefs.SetInt("Polyus.Muted", Muted ? 1 : 0); SetMute(); }
        private void SetMute() { effects.mute = Muted; music.mute = Muted; }
    }
}
