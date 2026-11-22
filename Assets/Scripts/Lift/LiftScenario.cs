using System.Collections;
using PsyEval.Calibration;
using UnityEngine;
using UnityEngine.UI;

namespace PsyEval.Lift
{
    // Scenariul "Liftul blocat". Deocamdata sunt facute fazele 0, 1 si 2:
    // faza 0: hol -> cheama liftul -> intra in cabina -> liftul urca -> prima intrebare (calm)
    // faza 1: liftul se opreste brusc intre etaje -> liniste -> intrebarea despre starea de acum
    // faza 2: lumina palpaie -> pana de curent, lumina de avarie -> dispecerul la interfon -> decizie -> intrebare
    //
    // In modelul elevator0.2.fbx holul si cabina sunt unul langa altul, asa ca la pornire
    // mutam piesele cabinei in spatele usii din hol.
    public class LiftScenario : MonoBehaviour
    {
        [Header("Legaturi")]
        public CalibrationRoom panel;
        public Transform elevatorModel;
        public BackgroundMusic cabinMusic;
        public Light[] buildingLights; // luminile din hol, se sting la pana de curent

        [Header("Piesele din model")]
        public string[] cabinParts =
        {
            "Cube.010", "Cube.012", "Cube.013", "Cube.014", "Cube.018", "Plane.001", "Cylinder",
            "Cylinder.002", "Cylinder.003", "Cylinder.004", "Cylinder.005", "Cylinder.006", "Cylinder.007",
            "Cylinder.008", "Cylinder.009", "Cylinder.010", "Cylinder.011", "Cylinder.012", "Cylinder.013", "Cylinder.014"
        };
        public string leftDoor = "Plane.002";
        public string rightDoor = "Plane.003";
        public string callButton = "Cylinder.001";

        [Header("Pozitii (fata de model)")]
        public Vector3 cabinOffset = new Vector3(-2.535f, -0.09f, -2.88f); // cat mutam cabina
        public Vector3 hallStandPoint = new Vector3(0f, 0.57f, -0.5f);
        public Vector3 cabinStandPoint = new Vector3(0f, 0.57f, -3.45f);
        public Vector3 cabinLightPoint = new Vector3(-0.06f, 2.45f, -3.31f);
        public Vector3 floorDisplayPoint = new Vector3(0f, 2.495f, -2.34f);
        public float eyeHeight = 1.65f; // inaltimea ochilor, in picioare

        [Header("Usi si calatorie")]
        public float doorTravel = 0.76f; // cat gliseaza fiecare usa
        public float doorSeconds = 2f;
        public float callWaitSeconds = 3f;
        public float secondsPerFloor = 3.5f;
        public string[] floorLabels = { "P", "1", "2", "3", "4" };
        public float stopDelaySeconds = 2f; // dupa etajul 4, cat mai urca pana se blocheaza
        public float silenceSeconds = 4f;   // liniste dupa oprire, inainte de intrebare

        [Header("Oprirea brusca")]
        public float joltDrop = 0.06f;    // cat cade cabina (in unitatile modelului)
        public float joltShake = 0.035f;  // cat de tare tremura la inceput
        public float joltSeconds = 0.9f;  // cat dureaza tremuratul

        [Header("Sunete (daca sunt goale, se genereaza din cod)")]
        public AudioClip chimeClip;
        public AudioClip beepClip;
        public AudioClip doorClip;
        public AudioClip humClip;
        public AudioClip stopClip;
        public AudioClip buzzClip;
        public AudioClip sparkClip;
        [Range(0f, 1f)] public float buzzVolume = 0.35f;
        [Range(0f, 1f)] public float humVolume = 0.2f;
        [Range(0f, 1f)] public float effectsVolume = 0.6f;
        [Range(0f, 1f)] public float stopVolume = 1f;

        // textul de la inceput (il foloseste si generatorul de voce)
        public const string IntroKey = "lift-intro";
        public const string IntroTitle = "Liftul";
        public const string IntroBody = "Vă aflați în holul unei clădiri de birouri. Urmează să urcați cu liftul până la etajul 8.";
        const string IntroHint = "Apăsați trăgaciul pentru a chema liftul   ·   B / Y = ascultați din nou";

        // faza 2: vocea dispecerului si decizia
        public const string DispatcherKey = "LIFT-DISPECER";
        public const string DispatcherText = "Aici dispeceratul. Am primit semnalul. Un tehnician ajunge în aproximativ 20 de minute.";
        public const string ChoiceKey = "LIFT-ALEGERE";
        public const string ChoiceQuestion = "Liftul s-a blocat între etaje. Ce faceți?";
        public static readonly string[] ChoiceOptions =
        {
            "Apăs butonul de alarmă",
            "Vorbesc din nou la interfon",
            "Încerc să forțez ușile",
            "Aștept calm"
        };
        const int RiskyChoice = 3; // "Încerc să forțez ușile"

        OVRCameraRig rig;
        ScreenFader fader;
        Transform cabin;
        Transform doorLeft, doorRight, button;
        Vector3 doorLeftClosed, doorRightClosed;
        Color buttonColor;
        AudioSource hum;
        Text floorText;
        bool passedLastFloor;
        Light cabinLight;
        AudioSource intercom;
        AudioSource buzz;

        IEnumerator Start()
        {
            rig = FindAnyObjectByType<OVRCameraRig>();
            fader = ScreenFader.Create(rig.centerEyeAnchor);
            fader.Set(1f); // incepem cu ecranul negru

            SetupModel();
            CreateFloorDisplay();
            SetupSounds();

            while (!panel.IsReady()) yield return null;
            yield return new WaitForSeconds(0.6f);

            PlaceRig(hallStandPoint, Vector3.back); // in hol, cu fata spre usa liftului
            yield return fader.FadeTo(0f, 1.5f);

            yield return Phase0();
            yield return Phase1();
            yield return Phase2();
        }

        // ---------- faza 0: intrarea in lift ----------

        IEnumerator Phase0()
        {
            panel.BeginExternalSession();
            panel.PlacePanel(-elevatorModel.forward); // in hol: spre usa liftului
            yield return panel.ShowNotice(IntroKey, IntroTitle, IntroBody, IntroHint);

            // pacientul cheama liftul
            PlaySound(beepClip, button.position);
            SetButtonLight(true);
            yield return new WaitForSeconds(callWaitSeconds);

            // liftul a sosit
            PlaySound(chimeClip, DoorCenter());
            yield return new WaitForSeconds(0.6f);
            yield return MoveDoors(true);
            yield return new WaitForSeconds(1.2f);

            // intra in cabina: ecranul se intuneca, il mutam, apoi se lumineaza (fara rau de miscare)
            yield return fader.FadeTo(1f, 0.8f);
            PlaceRig(cabinStandPoint, Vector3.forward); // cu fata spre usi
            SetButtonLight(false);
            yield return new WaitForSeconds(0.4f);
            yield return fader.FadeTo(0f, 0.8f);

            yield return new WaitForSeconds(1.5f);
            yield return MoveDoors(false);

            // liftul porneste
            yield return new WaitForSeconds(0.8f);
            StartCoroutine(Ride());
            yield return new WaitForSeconds(3f);

            panel.PlacePanel(elevatorModel.forward); // in cabina: spre usi
            yield return panel.AskQuestionById("LIFT-SPATIU");

            Debug.Log("Liftul: faza 0 s-a terminat.");
        }

        // liftul urca: muzica, zumzet si afisajul numara etajele
        IEnumerator Ride()
        {
            if (cabinMusic != null) cabinMusic.Play();
            hum.volume = humVolume;
            hum.Play();

            for (int i = 0; i < floorLabels.Length; i++)
            {
                floorText.text = "▲ " + floorLabels[i];
                yield return new WaitForSeconds(secondsPerFloor);
            }
            passedLastFloor = true; // a trecut de etajul 4, faza 1 il poate opri
        }

        // ---------- faza 1: liftul se blocheaza intre etaje ----------

        IEnumerator Phase1()
        {
            // asteptam ca liftul sa treaca de ultimul etaj afisat (daca pacientul a raspuns repede)
            while (!passedLastFloor) yield return null;
            yield return new WaitForSeconds(stopDelaySeconds);

            // oprirea brusca: zgomot, vibratie, smucitura
            AudioSource.PlayClipAtPoint(stopClip, rig.centerEyeAnchor.position, stopVolume);
            if (cabinMusic != null) cabinMusic.StopNow();
            hum.Stop();
            floorText.text = "– –";
            StartCoroutine(VibrateControllers(0.6f));
            StartCoroutine(LightDip());
            yield return JoltCabin();

            // cateva secunde de liniste, apoi intrebarea
            yield return new WaitForSeconds(silenceSeconds);
            panel.PlacePanel(elevatorModel.forward);
            yield return panel.AskQuestionById("LIFT-STARE");

            Debug.Log("Liftul: faza 1 s-a terminat.");
        }

        // ---------- faza 2: asteptarea (lumina de avarie, interfonul, decizia) ----------

        IEnumerator Phase2()
        {
            yield return new WaitForSeconds(2f);
            yield return EmergencyLights();
            yield return new WaitForSeconds(2f);

            // dispecerul vorbeste prin interfon (doua bipuri, apoi vocea)
            PlaySound(beepClip, intercom.transform.position);
            yield return new WaitForSeconds(0.3f);
            PlaySound(beepClip, intercom.transform.position);
            yield return new WaitForSeconds(0.8f);
            intercom.Play();
            while (intercom.isPlaying) yield return null;
            yield return new WaitForSeconds(1.5f);

            // decizia sub presiune, apoi cum o evalueaza singur
            panel.PlacePanel(elevatorModel.forward);
            yield return panel.AskChoice(ChoiceKey, ChoiceQuestion, ChoiceOptions, RiskyChoice);
            yield return panel.AskQuestionById("LIFT-DECIZIE");

            Debug.Log("Liftul: faza 2 s-a terminat.");
        }

        // lumina palpaie, se ia curentul, apoi porneste lumina rosie de avarie
        // palpaitul e lent (sub 3 ori pe secunda), ca sa fie sigur pentru persoanele fotosensibile
        IEnumerator EmergencyLights()
        {
            Vector3 lamp = cabinLight.transform.position;
            buzz.volume = buzzVolume;
            buzz.Play(); // bazaitul electric tine cat palpaie lumina

            float normal = cabinLight.intensity;
            for (int i = 0; i < 4; i++)
            {
                cabinLight.intensity = 0.2f;
                PlaySound(sparkClip, lamp); // pocnitura la fiecare clipire
                yield return new WaitForSeconds(0.2f);
                cabinLight.intensity = normal;
                yield return new WaitForSeconds(Random.Range(0.4f, 0.9f));
            }

            // pana de curent: o scanteie mai mare, apoi se sting toate luminile, inclusiv cele din cladire
            AudioSource.PlayClipAtPoint(sparkClip, lamp, 1f);
            buzz.Stop();
            cabinLight.intensity = 0f;
            foreach (Light l in buildingLights)
                l.enabled = false;
            RenderSettings.ambientLight = new Color(0.08f, 0.08f, 0.08f);
            yield return new WaitForSeconds(1f);

            // lumina de avarie
            cabinLight.color = new Color(1f, 0.12f, 0.08f);
            cabinLight.intensity = 1f;
        }

        // cabina cade brusc cativa centimetri si tremura, iar tremuratul scade treptat
        IEnumerator JoltCabin()
        {
            Vector3 start = cabin.localPosition;
            Vector3 end = start + Vector3.down * joltDrop; // ramane putin mai jos

            for (float t = 0f; t < joltSeconds; t += Time.deltaTime)
            {
                float fall = Mathf.Min(1f, t / 0.08f);    // caderea are loc in primele 0.08 s
                float strength = 1f - t / joltSeconds;    // tremuratul se stinge
                Vector3 shake = Random.insideUnitSphere * joltShake * strength;
                cabin.localPosition = Vector3.Lerp(start, end, fall) + shake;
                yield return null;
            }
            cabin.localPosition = end;
        }

        // lumina se stinge o clipa la impact (un singur clipit)
        IEnumerator LightDip()
        {
            float normal = cabinLight.intensity;
            cabinLight.intensity = 0.15f;
            yield return new WaitForSeconds(0.15f);
            cabinLight.intensity = normal;
        }

        IEnumerator VibrateControllers(float seconds)
        {
            OVRInput.SetControllerVibration(1f, 0.8f, OVRInput.Controller.LTouch);
            OVRInput.SetControllerVibration(1f, 0.8f, OVRInput.Controller.RTouch);
            yield return new WaitForSeconds(seconds);
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
        }

        // ---------- usi si buton ----------

        Vector3 DoorCenter()
        {
            return (doorLeft.position + doorRight.position) / 2f;
        }

        IEnumerator MoveDoors(bool open)
        {
            PlaySound(doorClip, DoorCenter());

            // distanta e in unitatile modelului, ca sa mearga si daca modelul e scalat
            Vector3 side = elevatorModel.TransformVector(Vector3.right * doorTravel);
            Vector3 leftStart = doorLeft.position;
            Vector3 rightStart = doorRight.position;
            Vector3 leftEnd = open ? doorLeftClosed - side : doorLeftClosed;
            Vector3 rightEnd = open ? doorRightClosed + side : doorRightClosed;

            for (float t = 0f; t < doorSeconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / doorSeconds); // porneste si se opreste lin
                doorLeft.position = Vector3.Lerp(leftStart, leftEnd, k);
                doorRight.position = Vector3.Lerp(rightStart, rightEnd, k);
                yield return null;
            }
            doorLeft.position = leftEnd;
            doorRight.position = rightEnd;
        }

        void SetButtonLight(bool on)
        {
            Renderer r = button.GetComponent<Renderer>();
            if (on)
                r.material.color = new Color(1f, 0.72f, 0.2f); // portocaliu
            else
                r.material.color = buttonColor;
        }

        // muta pacientul: ochii deasupra punctului dat, privind in directia data
        void PlaceRig(Vector3 point, Vector3 direction)
        {
            Transform head = rig.centerEyeAnchor;

            // capul poate fi deja intors fata de rig, asa ca rotim rig-ul cu diferenta
            float headTurn = head.localEulerAngles.y;
            rig.transform.rotation = Quaternion.LookRotation(elevatorModel.TransformDirection(direction)) * Quaternion.Euler(0f, -headTurn, 0f);

            // la fel si cu pozitia: capul poate fi putin in lateral fata de centrul rig-ului
            Vector3 headOffset = head.position - rig.transform.position;
            headOffset.y = 0f;
            Vector3 target = elevatorModel.TransformPoint(point) + Vector3.up * eyeHeight;
            rig.transform.position = target - headOffset;
        }

        void PlaySound(AudioClip clip, Vector3 position)
        {
            AudioSource.PlayClipAtPoint(clip, position, effectsVolume);
        }

        // ---------- pregatirea scenei ----------

        void SetupModel()
        {
            // fisierul FBX vine cu o camera si o lumina, care ar incurca scena VR
            foreach (Camera c in elevatorModel.GetComponentsInChildren<Camera>(true))
                c.gameObject.SetActive(false);
            foreach (Light l in elevatorModel.GetComponentsInChildren<Light>(true))
                l.gameObject.SetActive(false);

            // grupam piesele cabinei si le mutam in spatele usii
            cabin = new GameObject("Cabina").transform;
            cabin.SetParent(elevatorModel, false);
            foreach (string part in cabinParts)
            {
                Transform t = elevatorModel.Find(part);
                if (t != null) t.SetParent(cabin, true);
                else Debug.LogWarning("Nu gasesc piesa " + part + " in modelul liftului.");
            }
            cabin.localPosition = cabinOffset;

            // lumina din tavanul cabinei
            GameObject lightObject = new GameObject("Lumina cabina");
            lightObject.transform.position = elevatorModel.TransformPoint(cabinLightPoint);
            lightObject.transform.SetParent(cabin, true);
            cabinLight = lightObject.AddComponent<Light>();
            cabinLight.type = LightType.Point;
            cabinLight.range = 4f;
            cabinLight.intensity = 1.2f;
            cabinLight.color = new Color(1f, 0.96f, 0.88f);

            doorLeft = elevatorModel.Find(leftDoor);
            doorRight = elevatorModel.Find(rightDoor);
            button = elevatorModel.Find(callButton);
            doorLeftClosed = doorLeft.position;
            doorRightClosed = doorRight.position;
            buttonColor = button.GetComponent<Renderer>().material.color;
        }

        // afisajul cu etajul de deasupra usii (un panou mic cu text portocaliu)
        void CreateFloorDisplay()
        {
            GameObject display = new GameObject("Afisaj etaj");
            display.transform.position = elevatorModel.TransformPoint(floorDisplayPoint);
            display.transform.rotation = elevatorModel.rotation;
            display.transform.SetParent(cabin, true);

            Canvas canvas = display.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            display.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            RectTransform rect = display.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260f, 110f);
            rect.localScale = Vector3.one * 0.001f; // 260 x 110 mm

            Image background = new GameObject("Fundal").AddComponent<Image>();
            background.transform.SetParent(display.transform, false);
            background.rectTransform.sizeDelta = rect.sizeDelta;
            background.color = new Color(0.06f, 0.06f, 0.07f);

            floorText = new GameObject("Etaj").AddComponent<Text>();
            floorText.transform.SetParent(display.transform, false);
            floorText.rectTransform.sizeDelta = rect.sizeDelta;
            floorText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            floorText.fontSize = 72;
            floorText.fontStyle = FontStyle.Bold;
            floorText.alignment = TextAnchor.MiddleCenter;
            floorText.color = new Color(1f, 0.62f, 0.1f);
            floorText.text = floorLabels[0];
        }

        void SetupSounds()
        {
            if (chimeClip == null) chimeClip = LiftSounds.Chime();
            if (beepClip == null) beepClip = LiftSounds.Beep();
            if (doorClip == null) doorClip = LiftSounds.DoorMotor(doorSeconds);
            if (humClip == null) humClip = LiftSounds.Hum();
            if (stopClip == null) stopClip = LiftSounds.Clunk();
            if (buzzClip == null) buzzClip = LiftSounds.Buzz();
            if (sparkClip == null) sparkClip = LiftSounds.Spark();

            // bazaitul vine din lampa cabinei
            buzz = cabinLight.gameObject.AddComponent<AudioSource>();
            buzz.clip = buzzClip;
            buzz.loop = true;
            buzz.playOnAwake = false;
            buzz.spatialBlend = 1f;

            hum = cabin.gameObject.AddComponent<AudioSource>();
            hum.clip = humClip;
            hum.loop = true;

            // difuzorul interfonului, pe panoul cu butoane din cabina
            GameObject speaker = new GameObject("Interfon");
            speaker.transform.position = cabin.Find("Cube.018").position;
            speaker.transform.SetParent(cabin, true);
            intercom = speaker.AddComponent<AudioSource>();
            intercom.playOnAwake = false;
            intercom.spatialBlend = 0.7f; // se aude dinspre panou, dar destul de tare
            intercom.clip = Resources.Load<AudioClip>("Narration/Lift/" + DispatcherKey);
            if (intercom.clip == null)
                Debug.LogWarning("Lipseste vocea dispecerului. Rulati PsyEval > Genereaza vocea.");

            // filtre ca vocea sa sune ca printr-un difuzor mic de interfon
            speaker.AddComponent<AudioHighPassFilter>().cutoffFrequency = 500f;
            speaker.AddComponent<AudioLowPassFilter>().cutoffFrequency = 3000f;
            speaker.AddComponent<AudioDistortionFilter>().distortionLevel = 0.35f;
        }
    }
}
