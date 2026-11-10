using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PsyEval.Calibration
{
    // Panoul cu intrebari din camera de calibrare.
    // Se pune pe un GameObject gol in scena; construieste singur un Canvas world-space in fata pacientului.
    //
    // Control (Quest):   joystick stanga/dreapta = alege, trigaci (index) = confirma, click pe joystick = recentreaza panoul
    // Control (PC):      sageti stanga/dreapta sau tastele 1-5 = alege, Enter/Space = confirma, R = recentreaza
    [DisallowMultipleComponent]
    public class CalibrationRoom : MonoBehaviour
    {
        [Header("Sesiune")]
        public string sessionId = "PACIENT-TEST";
        public bool startAutomatically = true;
        public bool saveJson = true;

        [Header("Întrebări (se completează automat cu setul implicit)")]
        public List<CalibrationQuestion> questions = new List<CalibrationQuestion>();

        [Header("Plasarea panoului")]
        [Tooltip("Optional. Daca e gol, panoul se pune in fata camerei.")]
        public Transform panelAnchor;
        public float distance = 1.6f;
        public float heightOffset = -0.1f;
        [Tooltip("Asteapta tracking-ul castii inainte de a pozitiona panoul.")]
        public float placementDelay = 0.6f;
        public float worldScale = 0.001f;

        [Header("Aspect")]
        [Tooltip("Optional. Daca e gol, se foloseste fontul integrat din Unity.")]
        public Font font;
        public Color backgroundColor = new Color32(0xF6, 0xF4, 0xEF, 0xF5);
        public Color textColor = new Color32(0x2B, 0x2D, 0x30, 0xFF);
        public Color mutedColor = new Color32(0x6B, 0x6F, 0x76, 0xFF);
        public Color tileColor = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        public Color tileBorderColor = new Color32(0xD3, 0xCF, 0xC6, 0xFF);
        public Color accentColor = new Color32(0x3F, 0x76, 0x9A, 0xFF);

        [Header("Evenimente")]
        [Tooltip("Se apeleaza cu textul afisat (pentru naratorul TTS).")]
        public UnityEvent<string> onTextShown = new UnityEvent<string>();
        public UnityEvent<CalibrationResult> onCompleted = new UnityEvent<CalibrationResult>();

        public CalibrationResult Result { get; private set; }
        public static CalibrationResult LastResult { get; private set; }

        const float PanelW = 1400f, PanelH = 820f;
        const float FadeDuration = 0.35f;

        Transform panelRoot;
        CanvasGroup canvasGroup;
        Text titleText, bodyText, headerLeft, headerRight, hintText;
        RectTransform progressFill;
        GameObject optionsRow;
        readonly List<Image> tileBorders = new List<Image>();
        readonly List<Image> tileFills = new List<Image>();
        readonly List<Text> tileNumbers = new List<Text>();
        readonly List<Text> tileLabels = new List<Text>();

        int selected = -1;
        bool stickArmed = true;
        bool running;

        void Reset()
        {
            questions = CalibrationQuestionBank.CreateDefault();
        }

        [ContextMenu("Reîncarcă întrebările implicite")]
        void ReloadDefaultQuestions()
        {
            questions = CalibrationQuestionBank.CreateDefault();
        }

        IEnumerator Start()
        {
            if (questions == null || questions.Count == 0)
                questions = CalibrationQuestionBank.CreateDefault();

            BuildUI();
            canvasGroup.alpha = 0f;

            yield return new WaitForSeconds(placementDelay);
            PlacePanel();

            if (startAutomatically)
                BeginSession();
        }

        public void BeginSession()
        {
            if (!running)
                StartCoroutine(RunSession());
        }

        // ---------------------------------------------------------------- flux

        IEnumerator RunSession()
        {
            running = true;
            Result = new CalibrationResult
            {
                sessionId = sessionId,
                startedAtUtc = DateTime.UtcNow.ToString("o")
            };

            ShowMessage(
                "Bine ați venit",
                "Vă aflați într-o cameră liniștită. Vă vom adresa câteva întrebări scurte.\n" +
                "Nu există răspunsuri bune sau greșite – alegeți varianta care vi se potrivește.",
                "Apăsați trăgaciul pentru a începe");
            yield return Fade(1f);
            onTextShown.Invoke(titleText.text + ". " + bodyText.text);
            yield return WaitForConfirm();
            yield return Fade(0f);

            for (int i = 0; i < questions.Count; i++)
                yield return AskQuestion(i);

            FinalizeResult();
            if (saveJson) SaveJson();

            SetProgress(1f);
            ShowMessage(
                "Vă mulțumim!",
                "Partea de calibrare s-a încheiat.\nRămâneți relaxat în continuare, urmează etapa următoare.",
                "");
            yield return Fade(1f);
            onTextShown.Invoke(titleText.text + " " + bodyText.text);

            LastResult = Result;
            running = false;
            onCompleted.Invoke(Result);
        }

        IEnumerator AskQuestion(int index)
        {
            CalibrationQuestion q = questions[index];

            ShowQuestion(q, index);
            selected = -1;
            RefreshTiles(false);
            yield return Fade(1f);
            onTextShown.Invoke(q.text);

            float shownAt = Time.unscaledTime;
            float firstMove = -1f;
            int changes = 0;

            while (true)
            {
                int prev = selected;
                int direct = ReadDirectKey();
                int step = ReadHorizontalStep();

                if (direct >= 0)
                    selected = direct;
                else if (step != 0)
                    selected = selected < 0
                        ? (step > 0 ? 0 : CalibrationQuestionBank.OptionCount - 1)
                        : Mathf.Clamp(selected + step, 0, CalibrationQuestionBank.OptionCount - 1);

                if (selected != prev)
                {
                    if (firstMove < 0f) firstMove = Time.unscaledTime - shownAt;
                    if (prev >= 0) changes++;
                    RefreshTiles(false);
                }

                if (RecenterPressed()) PlacePanel();
                if (selected >= 0 && ConfirmPressed()) break;

                yield return null;
            }

            float rt = Time.unscaledTime - shownAt;
            RecordAnswer(q, selected + 1, rt, firstMove, changes);

            RefreshTiles(true);
            hintText.text = "";
            yield return new WaitForSeconds(0.4f);
            yield return Fade(0f);
        }

        IEnumerator WaitForConfirm()
        {
            while (!ConfirmPressed())
            {
                if (RecenterPressed()) PlacePanel();
                yield return null;
            }
        }

        IEnumerator Fade(float target)
        {
            float start = canvasGroup.alpha;
            for (float t = 0f; t < FadeDuration; t += Time.unscaledDeltaTime)
            {
                canvasGroup.alpha = Mathf.Lerp(start, target, t / FadeDuration);
                yield return null;
            }
            canvasGroup.alpha = target;
        }

        // ---------------------------------------------------------------- rezultate

        void RecordAnswer(CalibrationQuestion q, int answer, float rt, float firstMove, int changes)
        {
            int scored = q.reverseScored ? (CalibrationQuestionBank.OptionCount + 1 - answer) : answer;
            bool isControl = q.expectedAnswer > 0;

            Result.answers.Add(new CalibrationAnswer
            {
                questionId = q.id,
                dimension = q.dimension.ToString(),
                answer = answer,
                score0to100 = Mathf.RoundToInt((scored - 1) * 100f / (CalibrationQuestionBank.OptionCount - 1)),
                responseTimeSec = rt,
                firstMoveTimeSec = firstMove,
                selectionChanges = changes,
                isControl = isControl,
                controlPassed = !isControl || answer == q.expectedAnswer,
                flagged = q.flagThreshold > 0 && answer >= q.flagThreshold
            });
        }

        void FinalizeResult()
        {
            Result.finishedAtUtc = DateTime.UtcNow.ToString("o");

            float sum = 0f, motorSum = 0f;
            int n = 0, motorN = 0;
            bool attentionOk = true;

            for (int i = 0; i < Result.answers.Count; i++)
            {
                CalibrationAnswer a = Result.answers[i];
                CalibrationQuestion q = questions[i];

                if (a.isControl)
                {
                    motorSum += a.responseTimeSec;
                    motorN++;
                    if (!a.controlPassed)
                    {
                        attentionOk = false;
                        Result.flags.Add($"{a.questionId}: întrebarea de control a primit {a.answer} în loc de {q.expectedAnswer}.");
                    }
                }
                if (q.countsForScore)
                {
                    sum += a.responseTimeSec;
                    n++;
                }
                if (a.flagged)
                    Result.flags.Add($"{a.questionId} ({a.dimension}): răspuns {a.answer} – posibilă prezentare favorabilă de sine.");
            }

            Result.meanResponseTimeSec = n > 0 ? sum / n : 0f;
            Result.motorBaselineSec = motorN > 0 ? motorSum / motorN : 0f;
            Result.attentionCheckPassed = attentionOk;
        }

        void SaveJson()
        {
            try
            {
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string path = Path.Combine(Application.persistentDataPath, $"calibrare_{sessionId}_{stamp}.json");
                File.WriteAllText(path, JsonUtility.ToJson(Result, true));
                Debug.Log($"[PsyEval] Rezultatele calibrării au fost salvate în: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PsyEval] Nu s-au putut salva rezultatele calibrării: {e.Message}");
            }
        }

        // ---------------------------------------------------------------- input

        static float StickX(OVRInput.Controller c) => OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, c).x;

        int ReadHorizontalStep()
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) return 1;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) return -1;

            float l = StickX(OVRInput.Controller.LTouch);
            float r = StickX(OVRInput.Controller.RTouch);
            float x = Mathf.Abs(l) > Mathf.Abs(r) ? l : r;

            if (stickArmed && Mathf.Abs(x) > 0.6f)
            {
                stickArmed = false;
                return x > 0f ? 1 : -1;
            }
            if (!stickArmed && Mathf.Abs(x) < 0.3f)
                stickArmed = true;
            return 0;
        }

        static int ReadDirectKey()
        {
            for (int i = 0; i < CalibrationQuestionBank.OptionCount; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                    return i;
            }
            return -1;
        }

        static bool ConfirmPressed()
        {
            return OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch)
                || OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Space);
        }

        static bool RecenterPressed()
        {
            return OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch)
                || OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch)
                || Input.GetKeyDown(KeyCode.R);
        }

        // ---------------------------------------------------------------- plasare

        public void PlacePanel()
        {
            if (panelRoot == null) return;

            if (panelAnchor != null)
            {
                panelRoot.SetPositionAndRotation(panelAnchor.position, panelAnchor.rotation);
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[PsyEval] Nu există Camera.main – panoul rămâne la poziția obiectului.");
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 pos = cam.transform.position + forward * distance + Vector3.up * heightOffset;
            panelRoot.SetPositionAndRotation(pos, Quaternion.LookRotation(forward, Vector3.up));
        }

        // ---------------------------------------------------------------- afisare

        void ShowMessage(string title, string body, string hint)
        {
            optionsRow.SetActive(false);
            bodyText.gameObject.SetActive(true);
            headerRight.text = "";
            titleText.text = title;
            titleText.fontSize = 60;
            SetRect(titleText.rectTransform, 80, 170, PanelW - 160, 120);
            bodyText.text = body;
            hintText.text = hint;
        }

        void ShowQuestion(CalibrationQuestion q, int index)
        {
            optionsRow.SetActive(true);
            bodyText.gameObject.SetActive(false);
            headerRight.text = $"Întrebarea {index + 1} din {questions.Count}";
            titleText.text = q.text;
            titleText.fontSize = 52;
            SetRect(titleText.rectTransform, 80, 150, PanelW - 160, 250);
            SetProgress((float)index / questions.Count);
        }

        void RefreshTiles(bool confirmed)
        {
            for (int i = 0; i < tileFills.Count; i++)
            {
                bool on = i == selected;
                tileBorders[i].color = on ? accentColor : tileBorderColor;
                tileFills[i].color = on ? (confirmed ? Color.Lerp(accentColor, Color.white, 0.15f) : accentColor) : tileColor;
                tileNumbers[i].color = on ? Color.white : textColor;
                tileLabels[i].color = on ? Color.white : mutedColor;
                tileBorders[i].rectTransform.localScale = Vector3.one * (on ? 1.07f : 1f);
            }

            if (confirmed) return;
            hintText.text = selected < 0
                ? "Mișcați joystick-ul stânga / dreapta pentru a alege un răspuns"
                : $"Ați ales „{selected + 1} – {CalibrationQuestionBank.ScaleLabels[selected]}”.  Apăsați trăgaciul pentru a confirma.";
        }

        void SetProgress(float p)
        {
            progressFill.anchorMax = new Vector2(Mathf.Clamp01(p), 1f);
        }

        // ---------------------------------------------------------------- constructie UI

        void BuildUI()
        {
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var root = new GameObject("CalibrationPanel",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            panelRoot = root.transform;

            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(PanelW, PanelH);
            rt.localScale = Vector3.one * worldScale;

            Image bg = CreateImage("Fundal", rt, backgroundColor);
            Stretch(bg.rectTransform, 0f);

            // progres
            Image progressBg = CreateImage("Progres", rt, tileBorderColor);
            SetRect(progressBg.rectTransform, 60, 50, PanelW - 120, 10);
            Image fill = CreateImage("ProgresUmplere", progressBg.rectTransform, accentColor);
            progressFill = fill.rectTransform;
            progressFill.anchorMin = Vector2.zero;
            progressFill.anchorMax = new Vector2(0f, 1f);
            progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;

            // antet
            headerLeft = CreateText("AntetStanga", rt, 30, FontStyle.Normal, mutedColor, TextAnchor.MiddleLeft);
            SetRect(headerLeft.rectTransform, 60, 75, 700, 50);
            headerLeft.text = "Camera de calibrare";
            headerRight = CreateText("AntetDreapta", rt, 30, FontStyle.Normal, mutedColor, TextAnchor.MiddleRight);
            SetRect(headerRight.rectTransform, PanelW - 760, 75, 700, 50);

            // titlu / intrebare
            titleText = CreateText("Intrebare", rt, 52, FontStyle.Bold, textColor, TextAnchor.MiddleCenter);
            SetRect(titleText.rectTransform, 80, 150, PanelW - 160, 250);

            // corp mesaj (intro / final)
            bodyText = CreateText("Mesaj", rt, 38, FontStyle.Normal, textColor, TextAnchor.UpperCenter);
            SetRect(bodyText.rectTransform, 110, 330, PanelW - 220, 300);
            bodyText.lineSpacing = 1.2f;

            // grila de raspunsuri 1-5
            var rowGo = new GameObject("Raspunsuri", typeof(RectTransform));
            rowGo.transform.SetParent(rt, false);
            optionsRow = rowGo;
            var row = (RectTransform)rowGo.transform;
            Stretch(row, 0f);

            const float tileW = 228f, tileH = 220f, gap = 20f, border = 4f;
            float totalW = CalibrationQuestionBank.OptionCount * tileW + (CalibrationQuestionBank.OptionCount - 1) * gap;
            float x0 = (PanelW - totalW) / 2f;

            for (int i = 0; i < CalibrationQuestionBank.OptionCount; i++)
            {
                Image b = CreateImage($"Varianta{i + 1}", row, tileBorderColor);
                SetRect(b.rectTransform, x0 + i * (tileW + gap), 440, tileW, tileH);
                b.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                b.rectTransform.anchoredPosition += new Vector2(tileW / 2f, -tileH / 2f);

                Image f = CreateImage("Interior", b.rectTransform, tileColor);
                Stretch(f.rectTransform, border);

                Text num = CreateText("Numar", f.rectTransform, 84, FontStyle.Bold, textColor, TextAnchor.MiddleCenter);
                SetRect(num.rectTransform, 0, 15, tileW - 2 * border, 110);
                num.text = (i + 1).ToString();

                Text lbl = CreateText("Eticheta", f.rectTransform, 28, FontStyle.Normal, mutedColor, TextAnchor.MiddleCenter);
                SetRect(lbl.rectTransform, 10, 125, tileW - 2 * border - 20, 80);
                lbl.text = CalibrationQuestionBank.ScaleLabels[i];

                tileBorders.Add(b);
                tileFills.Add(f);
                tileNumbers.Add(num);
                tileLabels.Add(lbl);
            }

            // indicatie jos
            hintText = CreateText("Indicatie", rt, 30, FontStyle.Normal, mutedColor, TextAnchor.MiddleCenter);
            SetRect(hintText.rectTransform, 60, 720, PanelW - 120, 60);
        }

        Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Text CreateText(string name, Transform parent, int size, FontStyle style, Color color, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        // pozitionare absoluta in pixeli, din coltul stanga-sus al parintelui
        static void SetRect(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
        }

        static void Stretch(RectTransform r, float inset)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset);
            r.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
