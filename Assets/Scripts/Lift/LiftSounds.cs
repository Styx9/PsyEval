using UnityEngine;

namespace PsyEval.Lift
{
    // Sunete de lift generate din cod (greybox). Se pot inlocui ulterior cu inregistrari de pe Freesound:
    // fiecare camp AudioClip din LiftScenario, daca e completat, are prioritate fata de varianta generata.
    public static class LiftSounds
    {
        const int Rate = 44100;

        // "Ding" de sosire: doua tonuri de clopotel care se sting.
        public static AudioClip Chime()
        {
            return Build("Chime", 1.6f, (t, i) =>
                Bell(t, 0f, 784f) * 0.5f +     // sol
                Bell(t, 0.35f, 659f) * 0.5f);  // mi
        }

        // Bip scurt la apasarea butonului.
        public static AudioClip Beep()
        {
            return Build("Beep", 0.12f, (t, i) =>
                Mathf.Sin(2f * Mathf.PI * 1250f * t) * Envelope(t, 0.005f, 0.12f) * 0.35f);
        }

        // Motorul usilor: un "fasait" mecanic, cu o bufnitura usoara la final.
        public static AudioClip DoorMotor(float seconds)
        {
            var rng = new System.Random(7);
            float lp = 0f;
            return Build("DoorMotor", seconds + 0.25f, (t, i) =>
            {
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * 0.08f; // zgomot filtrat (mai "infundat")
                float whirr = Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 190f * t) * 0.12f;
                float body = (lp * 0.9f + whirr) * Envelope(t, 0.25f, seconds) * 0.35f;
                float thud = t > seconds ? Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-(t - seconds) * 25f) * 0.5f : 0f;
                return body + thud;
            });
        }

        // Oprirea brusca a liftului, ca un defect serios:
        // lovitura puternica + metal care vibreaza + scrasnet metal pe metal + a doua lovitura (ceva s-a desprins)
        public static AudioClip Clunk()
        {
            var rng = new System.Random(11);
            float[] metalFreqs = { 187f, 516f, 1011f, 1670f, 2490f }; // frecvente nearmonice, ca o bara de metal lovita

            return Build("Clunk", 3f, (t, i) =>
            {
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

                // 1. lovitura: bufnitura grava + zgomot scurt
                float hit = Mathf.Sin(2f * Mathf.PI * 48f * t) * Mathf.Exp(-t * 5f) * 0.8f
                          + noise * Mathf.Exp(-t * 12f) * 0.6f;

                // 2. metalul continua sa vibreze dupa lovitura
                float metal = 0f;
                for (int k = 0; k < metalFreqs.Length; k++)
                    metal += Mathf.Sin(2f * Mathf.PI * metalFreqs[k] * t) * Mathf.Exp(-t * (1.5f + k)) * 0.12f;

                // 3. scrasnet: un ton ascutit care coboara (2400 Hz -> ~800 Hz), "zgariat" de zgomot
                float screech = 0f;
                if (t < 1.2f)
                {
                    float phase = 2f * Mathf.PI * (2400f * t - 650f * t * t);
                    screech = Mathf.Sin(phase) * (0.6f + 0.4f * noise) * Mathf.Min(1f, t / 0.03f) * Mathf.Exp(-t * 2.2f) * 0.35f;
                }

                // 4. a doua lovitura, mai mica, dupa 0.4 s
                float second = 0f;
                float t2 = t - 0.4f;
                if (t2 > 0f)
                    second = (Mathf.Sin(2f * Mathf.PI * 90f * t2) * 0.5f + noise * 0.4f) * Mathf.Exp(-t2 * 10f);

                return hit + metal + screech + second;
            });
        }

        // Bazait electric (ca un tub de neon defect): 100 Hz cu multe armonice, sunet "aspru". Bucla de 1 s.
        public static AudioClip Buzz()
        {
            return Build("Buzz", 1f, (t, i) =>
            {
                float s = 0f;
                for (int k = 1; k <= 14; k++)
                    s += Mathf.Sin(2f * Mathf.PI * 100f * k * t) / k;
                return s * 0.25f;
            });
        }

        // Scanteie: pocnituri scurte si dese, peste un bazait care se stinge repede.
        public static AudioClip Spark()
        {
            var rng = new System.Random(5);
            float crackle = 0f;
            return Build("Spark", 0.45f, (t, i) =>
            {
                if (rng.NextDouble() < 0.004) // din cand in cand apare o pocnitura noua
                    crackle = (float)(rng.NextDouble() * 2.0 - 1.0);
                crackle *= 0.97f;             // fiecare pocnitura se stinge foarte repede

                float buzz = 0f;
                for (int k = 1; k <= 10; k++)
                    buzz += Mathf.Sin(2f * Mathf.PI * 100f * k * t) / k;

                return (crackle * 0.9f + buzz * 0.3f) * Mathf.Exp(-t * 6f);
            });
        }

        // Zumzetul cabinei in miscare. Bucla perfecta: doar frecvente intregi pe o durata de 1 s.
        public static AudioClip Hum()
        {
            var rng = new System.Random(3);
            var freqs = new float[24];
            var phases = new float[24];
            var amps = new float[24];
            for (int k = 0; k < freqs.Length; k++)
            {
                freqs[k] = 40 + rng.Next(0, 160);            // 40-200 Hz, zgomot grav
                phases[k] = (float)(rng.NextDouble() * Mathf.PI * 2f);
                amps[k] = 0.02f + (float)rng.NextDouble() * 0.03f;
            }
            return Build("Hum", 1f, (t, i) =>
            {
                float s = Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.12f + Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.06f;
                for (int k = 0; k < freqs.Length; k++)
                    s += Mathf.Sin(2f * Mathf.PI * freqs[k] * t + phases[k]) * amps[k];
                return s * 0.8f;
            });
        }

        static float Bell(float t, float start, float freq)
        {
            if (t < start) return 0f;
            float x = t - start;
            float env = Mathf.Exp(-x * 3.2f) * Mathf.Min(1f, x / 0.004f);
            return (Mathf.Sin(2f * Mathf.PI * freq * x) + 0.35f * Mathf.Sin(2f * Mathf.PI * freq * 2.01f * x)) * env;
        }

        static float Envelope(float t, float fade, float length)
        {
            return Mathf.Clamp01(t / fade) * Mathf.Clamp01((length - t) / fade);
        }

        static AudioClip Build(string name, float seconds, System.Func<float, int, float> sample)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
                data[i] = Mathf.Clamp(sample((float)i / Rate, i), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
