using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PsyEval.Lift
{
    // Intunecare a ecranului in casca (pentru mutarea pacientului fara rau de miscare).
    // Un panou negru, foarte aproape de ochi, atasat de camera.
    public class ScreenFader : MonoBehaviour
    {
        CanvasGroup group;
        Canvas canvas;

        public float Alpha => group != null ? group.alpha : 0f;

        public static ScreenFader Create(Transform head)
        {
            var go = new GameObject("Fade ecran", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            go.transform.SetParent(head, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.15f);
            go.transform.localRotation = Quaternion.identity;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 1000; // peste panoul cu intrebari

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(2000f, 2000f); // 2 m x 2 m la 15 cm = acopera tot campul vizual
            rt.localScale = Vector3.one * 0.001f;

            var img = new GameObject("Negru", typeof(RectTransform), typeof(Image));
            img.transform.SetParent(go.transform, false);
            var ir = (RectTransform)img.transform;
            ir.anchorMin = Vector2.zero;
            ir.anchorMax = Vector2.one;
            ir.offsetMin = ir.offsetMax = Vector2.zero;
            var image = img.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;

            var fader = go.AddComponent<ScreenFader>();
            fader.group = go.GetComponent<CanvasGroup>();
            fader.canvas = canvas;
            fader.group.interactable = false;
            fader.group.blocksRaycasts = false;
            return fader;
        }

        public void Set(float alpha)
        {
            group.alpha = alpha;
            canvas.enabled = alpha > 0f; // nu desenam nimic cand e complet transparent
        }

        public IEnumerator FadeTo(float target, float seconds)
        {
            canvas.enabled = true;
            float start = group.alpha;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(start, target, t / seconds);
                yield return null;
            }
            Set(target);
        }
    }
}
