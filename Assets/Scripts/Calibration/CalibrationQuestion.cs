using System;
using System.Collections.Generic;
using UnityEngine;

namespace PsyEval.Calibration
{
    // Cele 7 dimensiuni din raport (scoruri 0-100).
    public enum Dimension
    {
        Atentie,
        RezistentaLaStres,
        DeciziiSubPresiune,
        Empatie,
        ControlulFuriei,
        Sinceritate,
        Aptitudine
    }

    [Serializable]
    public class CalibrationQuestion
    {
        public string id;

        [TextArea(2, 4)]
        public string text;

        public Dimension dimension;

        [Tooltip("Bifat daca un raspuns mare (5) inseamna un scor MIC pe dimensiune.")]
        public bool reverseScored;

        [Tooltip("Intrebare de control cu raspuns corect (1-5). 0 = nu exista raspuns corect.")]
        [Range(0, 5)]
        public int expectedAnswer;

        [Tooltip("Raspuns >= acest prag este marcat in raport (ex. scala de minciuna). 0 = fara marcaj.")]
        [Range(0, 5)]
        public int flagThreshold;

        [Tooltip("Id-urile intrebarilor-pereche din scenele de stres (pentru detectarea contradictiilor).")]
        public string pairedWith;

        [Tooltip("Debifat pentru intrebarea de proba (nu intra in media timpilor de raspuns).")]
        public bool countsForScore = true;
    }

    public static class CalibrationQuestionBank
    {
        public const int OptionCount = 5;

        public static readonly string[] ScaleLabels =
        {
            "Deloc de acord",
            "Mai degrabă nu",
            "Nici da, nici nu",
            "Mai degrabă da",
            "Total de acord"
        };

        // Setul implicit pentru camera de calibrare. Formularile sunt neutre ca gen
        // ("ma simt in largul meu" in loc de "calm/calma").
        public static List<CalibrationQuestion> CreateDefault()
        {
            return new List<CalibrationQuestion>
            {
                // 0. Proba: invata controlul + masoara timpul de reactie "motor" si atentia.
                new CalibrationQuestion
                {
                    id = "CAL-0",
                    text = "Întrebare de probă: ca să exersați, alegeți varianta 5 – „Total de acord”.",
                    dimension = Dimension.Atentie,
                    expectedAnswer = 5,
                    countsForScore = false,
                    pairedWith = ""
                },
                // 1. Starea de baza. Aceeasi intrebare se repeta in fiecare scena de stres -> diferenta = reactia la stres.
                new CalibrationQuestion
                {
                    id = "CAL-1",
                    text = "Chiar acum mă simt în largul meu.",
                    dimension = Dimension.RezistentaLaStres,
                    pairedWith = "BIROU-STARE, TRAFIC-STARE, INCENDIU-STARE, LIFT-STARE"
                },
                // 2. Controlul furiei (trasatura declarata la calm).
                new CalibrationQuestion
                {
                    id = "CAL-2",
                    text = "Când cineva ridică tonul la mine, reușesc să îmi păstrez calmul.",
                    dimension = Dimension.ControlulFuriei,
                    pairedWith = "BIROU-FURIE"
                },
                // 3. Decizii sub presiune (autoevaluare, comparata cu alegerile reale din incendiu/trafic).
                new CalibrationQuestion
                {
                    id = "CAL-3",
                    text = "Când trebuie să iau o decizie repede, de obicei aleg varianta potrivită.",
                    dimension = Dimension.DeciziiSubPresiune,
                    pairedWith = "INCENDIU-DECIZIE, TRAFIC-DECIZIE"
                },
                // 4. Empatie.
                new CalibrationQuestion
                {
                    id = "CAL-4",
                    text = "Observ ușor când o persoană din jurul meu este supărată sau speriată.",
                    dimension = Dimension.Empatie,
                    pairedWith = "TRAFIC-EMPATIE, BIROU-EMPATIE"
                },
                // 5. Sinceritate: item de tip "scala de minciuna" (ca in EPQ-R). Aproape nimeni nu poate raspunde onest 4-5.
                new CalibrationQuestion
                {
                    id = "CAL-5",
                    text = "Nu mi s-a întâmplat niciodată să mă enervez pe cineva.",
                    dimension = Dimension.Sinceritate,
                    reverseScored = true,
                    flagThreshold = 4,
                    pairedWith = "CAL-2, BIROU-FURIE"
                }
            };
        }
    }

    [Serializable]
    public class CalibrationAnswer
    {
        public string questionId;
        public string dimension;
        public int answer;              // 1-5, exact ce a ales pacientul
        public int score0to100;         // dupa inversare (reverseScored), 1->0 ... 5->100
        public float responseTimeSec;   // de la afisare pana la confirmare
        public float firstMoveTimeSec;  // de la afisare pana la prima miscare a selectiei
        public int selectionChanges;    // de cate ori si-a schimbat alegerea (ezitare)
        public bool isControl;
        public bool controlPassed;
        public bool flagged;
    }

    [Serializable]
    public class CalibrationResult
    {
        public string sessionId;
        public string startedAtUtc;
        public string finishedAtUtc;
        public float meanResponseTimeSec;   // media pe intrebarile reale (baseline pentru scenele de stres)
        public float motorBaselineSec;      // timpul pe intrebarea de proba
        public bool attentionCheckPassed;
        public List<string> flags = new List<string>();
        public List<CalibrationAnswer> answers = new List<CalibrationAnswer>();
    }
}
