using UnityEngine;

namespace PsyEval.Calibration
{
    // Muzica de fundal. Porneste incet (fade-in) si se aude mai incet cat timp vorbeste naratorul.
    public class BackgroundMusic : MonoBehaviour
    {
        public AudioClip clip; // daca e gol, se ia din Resources/<resourcePath>
        public string resourcePath = "Music/LocalForecast_Elevator";
        public bool playOnStart = true; // in lift muzica porneste abia dupa ce se inchid usile

        [Range(0f, 1f)] public float volume = 0.15f;
        [Range(0f, 1f)] public float duckedVolume = 0.05f; // volumul cand vorbeste naratorul
        public float fadeInSeconds = 3f;

        public CalibrationNarrator narrator;

        AudioSource source;
        float fade; // 0 -> 1 in primele secunde

        void Start()
        {
            if (narrator == null)
                narrator = FindAnyObjectByType<CalibrationNarrator>();
            if (playOnStart)
                Play();
        }

        public void Play()
        {
            if (clip == null)
                clip = Resources.Load<AudioClip>(resourcePath);
            if (source == null)
                source = gameObject.AddComponent<AudioSource>();

            source.clip = clip;
            source.loop = true;
            source.volume = 0f;
            fade = 0f;
            source.Play();
        }

        // opreste muzica brusc (de ex. cand se blocheaza liftul)
        public void StopNow()
        {
            if (source != null) source.Stop();
        }

        void Update()
        {
            if (source == null || !source.isPlaying) return;
            if (narrator == null) narrator = FindAnyObjectByType<CalibrationNarrator>();

            fade = Mathf.Min(1f, fade + Time.deltaTime / fadeInSeconds);

            float target = volume;
            if (narrator != null && narrator.IsSpeaking())
                target = duckedVolume;

            // volumul se schimba lin, nu brusc
            source.volume = Mathf.MoveTowards(source.volume, target * fade, 1.5f * Time.deltaTime);
        }
    }
}
