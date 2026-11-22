using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using PsyEval.Lift;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PsyEval.Calibration.EditorTools
{
    // Genereaza vocea naratorului pentru scena deschisa, cu Piper (text-to-speech offline, voce romaneasca).
    // Piper se descarca o data cu Tools/setup_piper.ps1.
    static class NarrationGenerator
    {
        const string PiperExe = "Tools/piper/piper/piper.exe";
        const string VoiceModel = "Tools/piper/ro_RO-mihai-medium.onnx";

        [MenuItem("PsyEval/Generează vocea pentru scena deschisă (Piper TTS)")]
        static void Generate()
        {
            string projectFolder = Path.GetDirectoryName(Application.dataPath);
            string exe = Path.Combine(projectFolder, PiperExe);
            string model = Path.Combine(projectFolder, VoiceModel);
            if (!File.Exists(exe) || !File.Exists(model))
            {
                EditorUtility.DisplayDialog("Piper lipsește", "Rulați o dată Tools/setup_piper.ps1 din folderul proiectului.", "OK");
                return;
            }

            // adunam toate textele din scena: id -> text
            var lines = new Dictionary<string, string>();

            CalibrationRoom room = Object.FindAnyObjectByType<CalibrationRoom>();
            if (room != null)
            {
                foreach (CalibrationQuestion q in room.questions)
                    lines[q.id] = q.text;

                if (room.startAutomatically) // doar camera de calibrare are mesaj de inceput si de final
                {
                    lines[CalibrationRoom.IntroKey] = CalibrationRoom.MessageSpeech(CalibrationRoom.IntroTitle, CalibrationRoom.IntroBody);
                    lines[CalibrationRoom.OutroKey] = CalibrationRoom.MessageSpeech(CalibrationRoom.OutroTitle, CalibrationRoom.OutroBody);
                }
            }

            LiftScenario lift = Object.FindAnyObjectByType<LiftScenario>();
            if (lift != null)
            {
                lines[LiftScenario.IntroKey] = CalibrationRoom.MessageSpeech(LiftScenario.IntroTitle, LiftScenario.IntroBody);
                lines[LiftScenario.DispatcherKey] = LiftScenario.DispatcherText;
                lines[LiftScenario.ChoiceKey] = LiftScenario.ChoiceQuestion;
            }

            if (lines.Count == 0)
            {
                EditorUtility.DisplayDialog("Nimic de citit", "Scena deschisă nu are întrebări pentru narator.", "OK");
                return;
            }

            // fisierele vechi se sterg si se fac din nou
            string folder = Path.Combine(Application.dataPath, "Resources/Narration", SceneManager.GetActiveScene().name);
            Directory.CreateDirectory(folder);
            foreach (string oldFile in Directory.GetFiles(folder, "*.wav*"))
                File.Delete(oldFile);

            int done = 0;
            foreach (var line in lines)
            {
                EditorUtility.DisplayProgressBar("Piper TTS", line.Key, (float)done / lines.Count);
                string text = line.Value.Replace("\n", " ");
                bool ok = RunPiper(exe, model, text, Path.Combine(folder, line.Key + ".wav"));
                if (!ok) break;
                done++;
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("Vocea a fost generata: " + done + " fisiere in " + folder);
        }

        static bool RunPiper(string exe, string model, string text, string wavPath)
        {
            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = exe;
            // length_scale > 1 = vorbeste mai rar, sentence_silence = pauza dupa fiecare propozitie
            info.Arguments = "--model \"" + model + "\" --output_file \"" + wavPath + "\" --length_scale 1.12 --sentence_silence 0.35";
            info.WorkingDirectory = Path.GetDirectoryName(exe);
            info.UseShellExecute = false;
            info.RedirectStandardInput = true;
            info.RedirectStandardError = true;
            info.CreateNoWindow = true;

            Process piper = Process.Start(info);

            // textul se trimite ca UTF-8, altfel se pierd diacriticele
            byte[] bytes = new UTF8Encoding(false).GetBytes(text + "\n");
            piper.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
            piper.StandardInput.Close();

            string log = piper.StandardError.ReadToEnd();
            piper.WaitForExit();

            if (piper.ExitCode != 0 || !File.Exists(wavPath))
            {
                UnityEngine.Debug.LogError("Piper nu a putut genera " + wavPath + "\n" + log);
                return false;
            }
            return true;
        }
    }
}
