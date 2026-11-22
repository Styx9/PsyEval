using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

namespace PsyEval.Calibration
{
    // Panoul cu intrebari. In camera de calibrare porneste singur,
    // in alte scene (ex. liftul) este controlat de scriptul scenei.
    public class CalibrationRoom : MonoBehaviour
    {
        [Header("Sesiune")]
        public string sessionId = "PACIENT-TEST";
        public bool startAutomatically = true; // debifat in lift
        public bool saveJson = true;
        public string resultFilePrefix = "calibrare";
        public string panelTitle = "Camera de calibrare";

        [Header("Intrebari")]
        public List<CalibrationQuestion> questions = new List<CalibrationQuestion>();

        [Header("Panou")]
        public QuestionPanelUI panelPrefab;
        public float distance = 1.6f;      // cat de departe de pacient apare panoul
        public float heightOffset = -0.1f;
        public float worldScale = 0.001f;  // 1 pixel din panou = 1 mm
        public float placementDelay = 0.6f;

        [Header("Culori pentru variantele de raspuns")]
        public Color textColor = new Color32(0x2B, 0x2D, 0x30, 0xFF);
        public Color mutedColor = new Color32(0x6B, 0x6F, 0x76, 0xFF);
        public Color tileColor = Color.white;
        public Color tileBorderColor = new Color32(0xD3, 0xCF, 0xC6, 0xFF);
        public Color accentColor = new Color32(0x3F, 0x76, 0x9A, 0xFF);

        [Header("Narator")]
        public CalibrationNarrator narrator;

        public UnityEvent<CalibrationResult> onCompleted = new UnityEvent<CalibrationResult>();

        [HideInInspector] public CalibrationResult Result;
        public static CalibrationResult LastResult;

        // textele de inceput si de final (le foloseste si generatorul de voce)
        public const string IntroKey = "intro";
        public const string IntroTitle = "Bine ați venit";
        public const string IntroBody =
            "Vă aflați într-o cameră liniștită. Vă vom adresa câteva întrebări scurte.\n" +
            "Nu există răspunsuri bune sau greșite – alegeți varianta care vi se potrivește.";
        public const string OutroKey = "final";
        public const string OutroTitle = "Vă mulțumim!";
        public const string OutroBody =
            "Partea de calibrare s-a încheiat.\nRămâneți relaxat în continuare, urmează etapa următoare.";

        const float FadeDuration = 0.35f;

        QuestionPanelUI ui;
        string[] currentOptions;   // textele variantelor afisate acum
        int selected = -1;         // varianta aleasa acum (de la 0), -1 = nimic
        float answerTime;          // cat a durat pana a confirmat
        float firstMoveTime;       // cand a miscat prima data selectia
        int selectionChanges;      // de cate ori si-a schimbat alegerea
        bool stickReady = true;
        bool running;

        // textul citit de narator pentru un mesaj: titlu + corp
        public static string MessageSpeech(string title, string body)
        {
            if (title.EndsWith("!") || title.EndsWith("."))
                return title + " " + body;
            return title + ". " + body;
        }

        void Awake()
        {
            if (narrator == null)
                narrator = GetComponent<CalibrationNarrator>();
            if (narrator == null)
                narrator = gameObject.AddComponent<CalibrationNarrator>();
        }

        IEnumerator Start()
        {
            if (questions.Count == 0)
                questions = CalibrationQuestionBank.CreateDefault();

            // panoul este un prefab facut in editor (Assets/Prefabs/PanouIntrebari)
            ui = Instantiate(panelPrefab, transform);
            ui.transform.localScale = Vector3.one * worldScale;
            ui.headerLeft.text = panelTitle;
            ui.canvasGroup.alpha = 0f;

            yield return new WaitForSeconds(placementDelay); // asteptam sa porneasca tracking-ul castii
            PlacePanel();

            if (startAutomatically)
                BeginSession();
        }

        public void BeginSession()
        {
            if (!running)
                StartCoroutine(RunSession());
        }

        IEnumerator RunSession()
        {
            running = true;
            BeginExternalSession();

            yield return ShowNotice(IntroKey, IntroTitle, IntroBody, "Apăsați trăgaciul pentru a începe   ·   B / Y = ascultați din nou");

            for (int i = 0; i < questions.Count; i++)
                yield return AskQuestion(i);

            FinishExternalSession();

            SetProgress(1f);
            ShowMessage(OutroTitle, OutroBody, "");
            yield return Fade(1f);
            narrator.Speak(OutroKey, MessageSpeech(OutroTitle, OutroBody));
            running = false;
        }

        // ---------- functii folosite si de alte scene (ex. liftul) ----------

        public bool IsReady()
        {
            return ui != null;
        }

        public void BeginExternalSession()
        {
            Result = new CalibrationResult();
            Result.sessionId = sessionId;
            Result.startedAtUtc = DateTime.UtcNow.ToString("o");
        }

        // arata un mesaj, il citeste si asteapta tragaciul
        public IEnumerator ShowNotice(string key, string title, string body, string hint)
        {
            ShowMessage(title, body, hint);
            yield return Fade(1f);
            narrator.Speak(key, MessageSpeech(title, body));
            yield return WaitForConfirm();
            narrator.Stop();
            yield return Fade(0f);
        }

        public IEnumerator AskQuestionById(string id)
        {
            for (int i = 0; i < questions.Count; i++)
            {
                if (questions[i].id == id)
                {
                    yield return AskQuestion(i);
                    yield break;
                }
            }
            Debug.LogError("Intrebarea " + id + " nu exista in lista panoului.");
        }

        public void FinishExternalSession()
        {
            FinalizeResult();
            if (saveJson) SaveJson();
            LastResult = Result;
            onCompleted.Invoke(Result);
        }

        // ---------- o intrebare ----------

        IEnumerator AskQuestion(int index)
        {
            CalibrationQuestion q = questions[index];

            ShowQuestion(q, index);
            ShowOptions(CalibrationQuestionBank.ScaleLabels);
            yield return Fade(1f);
            narrator.Speak(q.id, q.text);

            yield return WaitForAnswer();
            RecordAnswer(q, selected + 1, answerTime, firstMoveTime, selectionChanges);

            yield return new WaitForSeconds(0.4f);
            yield return Fade(0f);
        }

        // ---------- o decizie (ex. in lift: ce faceti?) ----------

        // variantele apar in locul scalei 1-5; riskyOption = numarul variantei periculoase (de la 1), 0 = niciuna
        public IEnumerator AskChoice(string id, string question, string[] options, int riskyOption)
        {
            ui.optionsRow.SetActive(true);
            ui.bodyText.gameObject.SetActive(false);
            ui.headerRight.text = "Decizie";
            ui.titleText.text = question;
            ui.titleText.fontSize = 52;
            ShowOptions(options);
            yield return Fade(1f);
            narrator.Speak(id, question);

            yield return WaitForAnswer();

            ChoiceAnswer c = new ChoiceAnswer();
            c.id = id;
            c.question = question;
            c.choice = selected + 1;
            c.choiceText = options[selected];
            c.risky = selected + 1 == riskyOption;
            c.responseTimeSec = answerTime;
            c.firstMoveTimeSec = firstMoveTime;
            c.selectionChanges = selectionChanges;
            Result.choices.Add(c);
            if (c.risky)
                Result.flags.Add(id + ": a ales o variantă riscantă („" + c.choiceText + "”).");

            yield return new WaitForSeconds(0.4f);
            yield return Fade(0f);
            ShowOptions(CalibrationQuestionBank.ScaleLabels); // panoul revine la scala 1-5
        }

        // asteapta pana cand pacientul alege si confirma o varianta; masoara timpii
        IEnumerator WaitForAnswer()
        {
            selected = -1;
            RefreshTiles(false);

            float shownAt = Time.time;
            firstMoveTime = -1f;
            selectionChanges = 0;

            while (true)
            {
                int previous = selected;
                int step = ReadStep();
                int key = ReadNumberKey();
                int last = currentOptions.Length - 1;

                if (key >= 0 && key <= last)
                    selected = key;
                else if (step != 0)
                {
                    if (selected < 0)
                        selected = step > 0 ? 0 : last;
                    else
                        selected = Mathf.Clamp(selected + step, 0, last);
                }

                if (selected != previous)
                {
                    if (firstMoveTime < 0f) firstMoveTime = Time.time - shownAt;
                    if (previous >= 0) selectionChanges++;
                    RefreshTiles(false);
                }

                if (RecenterPressed()) PlacePanel();
                if (RepeatPressed()) narrator.Repeat();
                if (selected >= 0 && ConfirmPressed()) break;

                yield return null;
            }

            answerTime = Time.time - shownAt;
            narrator.Stop();
            RefreshTiles(true);
            ui.hintText.text = "";
        }

        // pune textele variantelor in casute; daca sunt mai putin de 5, le ascundem pe cele in plus
        void ShowOptions(string[] options)
        {
            currentOptions = options;
            for (int i = 0; i < 5; i++)
            {
                bool visible = i < options.Length;
                ui.tileBorders[i].gameObject.SetActive(visible);
                if (visible) ui.tileLabels[i].text = options[i];
            }

            // centram randul: fiecare casuta lipsa = jumatate de casuta (124 px) spre dreapta
            RectTransform row = ui.optionsRow.GetComponent<RectTransform>();
            row.anchoredPosition = new Vector2((5 - options.Length) * 124f, 0f);
        }

        IEnumerator WaitForConfirm()
        {
            while (!ConfirmPressed())
            {
                if (RecenterPressed()) PlacePanel();
                if (RepeatPressed()) narrator.Repeat();
                yield return null;
            }
        }

        IEnumerator Fade(float target)
        {
            float start = ui.canvasGroup.alpha;
            for (float t = 0f; t < FadeDuration; t += Time.deltaTime)
            {
                ui.canvasGroup.alpha = Mathf.Lerp(start, target, t / FadeDuration);
                yield return null;
            }
            ui.canvasGroup.alpha = target;
        }

        // ---------- rezultate ----------

        void RecordAnswer(CalibrationQuestion q, int answer, float time, float firstMove, int changes)
        {
            int scored = answer;
            if (q.reverseScored)
                scored = 6 - answer; // 5 devine 1, 4 devine 2 ...

            CalibrationAnswer a = new CalibrationAnswer();
            a.questionId = q.id;
            a.dimension = q.dimension.ToString();
            a.answer = answer;
            a.score0to100 = (scored - 1) * 25; // 1 -> 0, 2 -> 25 ... 5 -> 100
            a.responseTimeSec = time;
            a.firstMoveTimeSec = firstMove;
            a.selectionChanges = changes;
            a.isControl = q.expectedAnswer > 0;
            a.controlPassed = !a.isControl || answer == q.expectedAnswer;
            a.flagged = q.flagThreshold > 0 && answer >= q.flagThreshold;
            Result.answers.Add(a);
        }

        void FinalizeResult()
        {
            Result.finishedAtUtc = DateTime.UtcNow.ToString("o");

            float sum = 0f, controlSum = 0f;
            int count = 0, controlCount = 0;
            Result.attentionCheckPassed = true;

            foreach (CalibrationAnswer a in Result.answers)
            {
                CalibrationQuestion q = FindQuestion(a.questionId);

                if (a.isControl)
                {
                    controlSum += a.responseTimeSec;
                    controlCount++;
                    if (!a.controlPassed)
                    {
                        Result.attentionCheckPassed = false;
                        Result.flags.Add(a.questionId + ": întrebarea de control a primit " + a.answer + " în loc de " + q.expectedAnswer + ".");
                    }
                }
                if (q.countsForScore)
                {
                    sum += a.responseTimeSec;
                    count++;
                }
                if (a.flagged)
                    Result.flags.Add(a.questionId + " (" + a.dimension + "): răspuns " + a.answer + " – posibilă prezentare favorabilă de sine.");
            }

            if (count > 0) Result.meanResponseTimeSec = sum / count;
            if (controlCount > 0) Result.motorBaselineSec = controlSum / controlCount;
        }

        CalibrationQuestion FindQuestion(string id)
        {
            foreach (CalibrationQuestion q in questions)
                if (q.id == id) return q;
            return null;
        }

        void SaveJson()
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(Application.persistentDataPath, resultFilePrefix + "_" + sessionId + "_" + stamp + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(Result, true));
            Debug.Log("Rezultatele au fost salvate in: " + path);
        }

        // ---------- input (controller + tastatura pentru test pe PC) ----------

        int ReadStep()
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) return 1;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) return -1;

            // joystick-ul din stanga sau din dreapta, care e mai miscat
            float left = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch).x;
            float right = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch).x;
            float x = Mathf.Abs(left) > Mathf.Abs(right) ? left : right;

            // un pas pe miscare: trebuie sa lase joystick-ul inapoi la mijloc inainte de urmatorul pas
            if (stickReady && Mathf.Abs(x) > 0.6f)
            {
                stickReady = false;
                return x > 0f ? 1 : -1;
            }
            if (Mathf.Abs(x) < 0.3f)
                stickReady = true;
            return 0;
        }

        int ReadNumberKey()
        {
            for (int i = 0; i < 5; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                    return i;
            }
            return -1;
        }

        bool ConfirmPressed()
        {
            return OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch)
                || OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.Space);
        }

        // click pe joystick sau R = aduce panoul din nou in fata
        bool RecenterPressed()
        {
            return OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch)
                || OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch)
                || Input.GetKeyDown(KeyCode.R);
        }

        // B / Y sau T = asculta din nou
        bool RepeatPressed()
        {
            return OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch)
                || OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch)
                || Input.GetKeyDown(KeyCode.T);
        }

        // ---------- afisare ----------

        // pune panoul in fata pacientului, in directia in care se uita
        public void PlacePanel()
        {
            if (Camera.main == null) return;
            PlacePanel(Camera.main.transform.forward);
        }

        // pune panoul in fata pacientului, intr-o directie data (ex. in lift: spre usi)
        public void PlacePanel(Vector3 direction)
        {
            if (ui == null || Camera.main == null) return;

            Vector3 forward = direction;
            forward.y = 0f;
            forward.Normalize();

            Vector3 head = Camera.main.transform.position;
            ui.transform.position = head + forward * distance + Vector3.up * heightOffset;
            ui.transform.rotation = Quaternion.LookRotation(forward);
        }

        void ShowMessage(string title, string body, string hint)
        {
            ui.optionsRow.SetActive(false);
            ui.bodyText.gameObject.SetActive(true);
            ui.headerRight.text = "";
            ui.titleText.text = title;
            ui.titleText.fontSize = 60;
            ui.bodyText.text = body;
            ui.hintText.text = hint;
        }

        void ShowQuestion(CalibrationQuestion q, int index)
        {
            ui.optionsRow.SetActive(true);
            ui.bodyText.gameObject.SetActive(false);
            ui.headerRight.text = "Întrebarea " + (index + 1) + " din " + questions.Count;
            ui.titleText.text = q.text;
            ui.titleText.fontSize = 52;
            SetProgress((float)index / questions.Count);
        }

        // coloreaza varianta aleasa
        void RefreshTiles(bool confirmed)
        {
            for (int i = 0; i < currentOptions.Length; i++)
            {
                bool isSelected = i == selected;
                if (isSelected)
                {
                    ui.tileBorders[i].color = accentColor;
                    ui.tileFills[i].color = confirmed ? Color.Lerp(accentColor, Color.white, 0.15f) : accentColor;
                    ui.tileNumbers[i].color = Color.white;
                    ui.tileLabels[i].color = Color.white;
                    ui.tileBorders[i].transform.localScale = Vector3.one * 1.07f;
                }
                else
                {
                    ui.tileBorders[i].color = tileBorderColor;
                    ui.tileFills[i].color = tileColor;
                    ui.tileNumbers[i].color = textColor;
                    ui.tileLabels[i].color = mutedColor;
                    ui.tileBorders[i].transform.localScale = Vector3.one;
                }
            }

            if (confirmed) return;
            if (selected < 0)
                ui.hintText.text = "Mișcați joystick-ul stânga / dreapta pentru a alege   ·   B / Y = ascultați din nou";
            else
                ui.hintText.text = "Ați ales „" + (selected + 1) + " – " + currentOptions[selected] + "”.  Apăsați trăgaciul pentru a confirma.";
        }

        void SetProgress(float progress)
        {
            ui.progressFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
        }
    }
}
