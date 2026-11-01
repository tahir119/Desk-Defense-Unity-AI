using UnityEngine;

namespace DeskDefense
{
    public enum RoachType { Normal, Speedy, Armored }

    /// <summary>Pooled cockroach. Ticked by DDGame (no per-roach Update).</summary>
    public class Roach : MonoBehaviour
    {
        public SpriteRenderer body;

        [System.NonSerialized] public RoachType type;
        [System.NonSerialized] public int hp;
        [System.NonSerialized] public float radius;
        [System.NonSerialized] public bool dying;
        [System.NonSerialized] public Zone target;

        Transform tr;
        Vector3 basePos, targetPos;
        float speed, dps, scale, phase, wiggleFreq, punch, dyingT;
        bool chewing;
        Color tint;

        public bool Tappable => gameObject.activeSelf && !dying;

        void Awake() { tr = transform; }

        public void Spawn(RoachType t, Vector3 pos, Zone zone)
        {
            if (tr == null) tr = transform;
            type = t;
            switch (t)
            {
                case RoachType.Speedy:
                    hp = 1; speed = 3.0f; dps = 2.5f; scale = 0.42f; wiggleFreq = 22f; tint = new Color(1f, 0.62f, 0.45f); break;
                case RoachType.Armored:
                    hp = 3; speed = 1.0f; dps = 6f; scale = 0.66f; wiggleFreq = 9f; tint = new Color(0.55f, 0.68f, 0.95f); break;
                default:
                    hp = 1; speed = 1.5f; dps = 4f; scale = 0.5f; wiggleFreq = 14f; tint = Color.white; break;
            }
            radius = 0.62f * scale / 0.5f * 0.6f + 0.2f;
            body.color = tint;
            basePos = pos;
            tr.position = pos;
            tr.localScale = new Vector3(scale, scale, 1f);
            phase = Random.value * 6.28f;
            dying = false;
            chewing = false;
            punch = 0f;
            SetTarget(zone);
            gameObject.SetActive(true);
        }

        public void SetTarget(Zone zone)
        {
            target = zone;
            chewing = false;
            if (zone != null) targetPos = zone.RandomPoint();
        }

        /// <summary>Non-lethal hit (armored).</summary>
        public void Hit()
        {
            punch = 1f;
            float k = hp / 3f;
            body.color = Color.Lerp(new Color(1f, 0.85f, 0.85f), tint, k);
            // knock back a little
            Vector3 away = basePos - targetPos;
            if (away.sqrMagnitude > 0.0001f) basePos += away.normalized * 0.35f;
            chewing = false;
        }

        public void Kill()
        {
            dying = true;
            dyingT = 0f;
        }

        public void Tick(float dt, DDGame game)
        {
            if (dying)
            {
                dyingT += dt;
                float t = dyingT / 0.14f;
                tr.localScale = new Vector3(scale * (1f + 0.6f * t), scale * Mathf.Max(0.05f, 1f - 0.85f * t), 1f);
                if (t >= 1f) gameObject.SetActive(false);
                return;
            }

            if (target == null || !target.Alive)
            {
                Zone z = game.RandomAliveZone();
                if (z == null) return;
                SetTarget(z);
            }

            phase += dt * wiggleFreq;
            float s = Mathf.Sin(phase);
            Vector3 to = targetPos - basePos;
            float dist = to.magnitude;

            if (!chewing)
            {
                float step = speed * dt;
                if (dist <= step)
                {
                    basePos = targetPos;
                    chewing = true;
                }
                else
                {
                    Vector3 dir = to / dist;
                    basePos += dir * step;
                    Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
                    tr.position = basePos + perp * (s * 0.12f);
                    float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f + s * 14f;
                    tr.rotation = Quaternion.Euler(0f, 0f, ang);
                }
            }
            if (chewing)
            {
                tr.position = basePos + new Vector3(s * 0.03f, Mathf.Sin(phase * 1.7f) * 0.03f, 0f);
                tr.rotation = Quaternion.Euler(0f, 0f, tr.eulerAngles.z + s * 2f);
                target.Damage(dps * dt);
            }

            if (punch > 0f)
            {
                punch -= dt * 6f;
                float p = Mathf.Max(0f, punch);
                tr.localScale = new Vector3(scale * (1f + 0.35f * p), scale * (1f - 0.35f * p), 1f);
            }
        }
    }
}
