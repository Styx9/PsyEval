using UnityEngine;

namespace PsyEval.Calibration
{
    // Naratorul: reda vocea pentru fiecare intrebare.
    // Fisierele audio sunt generate din editor (PsyEval > Genereaza vocea) si stau in
    // Assets/Resources/Narration/<numele scenei>/<id intrebare>.wav, de exemplu CAL-1.wav
    [RequireComponent(typeof(AudioSource))]
    public class CalibrationNarrator : MonoBehaviour
    {
        public bool muted;
        [Range(0f, 1f)] public float volume = 0.7f;

        AudioSource source;
        string lastKey;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // se aude in casca, nu dintr-un punct din scena
        }

        public bool IsSpeaking()
        {
            return source != null && source.isPlaying;
        }

        public void Speak(string key, string text)
        {
            lastKey = key;
            Stop();
            if (muted) return;

            AudioClip clip = Resources.Load<AudioClip>("Narration/" + gameObject.scene.name + "/" + key);
            if (clip == null)
            {
                Debug.LogWarning("Lipseste vocea pentru " + key + ". Rulati PsyEval > Genereaza vocea.");
                return;
            }
            source.clip = clip;
            source.volume = volume;
            source.Play();
        }

        public void Repeat()
        {
            if (lastKey != null) Speak(lastKey, "");
        }

        public void Stop()
        {
            if (source != null) source.Stop();
        }
    }
}
