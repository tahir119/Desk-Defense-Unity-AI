using UnityEngine;

namespace DeskDefense
{
    public class CameraShake : MonoBehaviour
    {
        Vector3 basePos;
        float time, duration, amplitude;

        void Awake() { basePos = transform.localPosition; }

        public void Shake(float amp, float dur)
        {
            if (amp < amplitude * (duration > 0f ? time / duration : 0f)) return;
            amplitude = amp;
            duration = dur;
            time = dur;
        }

        void LateUpdate()
        {
            if (time <= 0f) return;
            time -= Time.unscaledDeltaTime;
            if (time <= 0f) { transform.localPosition = basePos; return; }
            float k = time / duration;
            transform.localPosition = basePos + (Vector3)(Random.insideUnitCircle * amplitude * k);
        }
    }
}
