using UnityEngine;
using UnityEngine.UI;

namespace PsyEval.Calibration
{
    // Sta pe prefab-ul PanouIntrebari si tine legaturile catre textele si imaginile din panou.
    // Asezarea lor se modifica direct in editor, pe prefab.
    public class QuestionPanelUI : MonoBehaviour
    {
        public CanvasGroup canvasGroup;

        [Header("Texte")]
        public Text headerLeft;
        public Text headerRight;
        public Text titleText;
        public Text bodyText;
        public Text hintText;

        [Header("Bara de progres")]
        public RectTransform progressFill;

        [Header("Variantele 1-5")]
        public GameObject optionsRow;
        public Image[] tileBorders = new Image[5];
        public Image[] tileFills = new Image[5];
        public Text[] tileNumbers = new Text[5];
        public Text[] tileLabels = new Text[5];
    }
}
