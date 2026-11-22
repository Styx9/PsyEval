# Proiect IMR

Proiect Realizat de : Costan Calin-George, Canur Andreea, Roman Florina, Adam Alessia-Nicoleta, Croitoriu Andrei-Bogdan.

VR pe Meta Quest , Unity **6000.6.3f1**.

## Setup

1. Instaleaza **Git LFS** (git-lfs.com) si ruleaza `git lfs install` **inainte** de clone
2. `git clone <URL>`
3. Deschide din Unity Hub cu exact versiunea **6000.6.3f1**
4. **Meta XR Core SDK**: Asset Store > Add to My Assets (acelasi cont Unity), apoi Package Manager > My Assets > Download + Install
5. **Meta XR Simulator**: se instaleaza separat pe PC (developers.meta.com/horizon/documentation/unity/), nu e pachet Unity

## Testare pe PC

1. Pornesti Meta XR Simulator si activezi comutatorul "runtime" din stanga-sus
2. Play in Unity
3. Click in fereastra Simulatorului inainte de taste

**Vocea (text-to-speech)** e generata offline cu [Piper](https://github.com/rhasspy/piper) (voce romaneasca) si salvata ca `.wav` in `Assets/Resources/Narration/Calibrare`, deci merge si pe Quest fara internet.
Fiecare scena are clipurile ei in `Assets/Resources/Narration/<scena>/<id intrebare>.wav` (ex. `CAL-1.wav`). Dupa ce modifici textul unei intrebari, regenereaza vocea:

1. O singura data: `powershell -ExecutionPolicy Bypass -File Tools/setup_piper.ps1`
2. In Unity, cu scena dorita deschisa (`Calibrare`, `Lift`): meniul **PsyEval > Generează vocea pentru scena deschisă (Piper TTS)**

## Credite

- Muzica: "Local Forecast - Elevator" Kevin MacLeod (incompetech.com), licenta Creative Commons: By Attribution 4.0 – http://creativecommons.org/licenses/by/4.0/
- Voce TTS: [Piper](https://github.com/rhasspy/piper), vocea `ro_RO-mihai-medium`

# Link Tema 5: Main modules : https://docs.google.com/document/d/1mD8t06BbJaWyDWqOIXX2oUErP4m-KTA5-IFM30miCEQ/edit?usp=sharing
