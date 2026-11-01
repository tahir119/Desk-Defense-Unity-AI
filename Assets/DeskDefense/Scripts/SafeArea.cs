using UnityEngine;

namespace DeskDefense
{
    /// <summary>Keeps this RectTransform inside the device safe area.</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        RectTransform rect;
        Rect lastSafeArea;
        int lastWidth, lastHeight;

        void Awake()
        {
            rect = GetComponent<RectTransform>();
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != lastSafeArea || Screen.width != lastWidth || Screen.height != lastHeight) Apply();
        }

        void Apply()
        {
            lastSafeArea = Screen.safeArea;
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            if (lastWidth <= 0 || lastHeight <= 0) return;
            Vector2 min = lastSafeArea.position;
            Vector2 max = lastSafeArea.position + lastSafeArea.size;
            min.x /= lastWidth; min.y /= lastHeight;
            max.x /= lastWidth; max.y /= lastHeight;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
