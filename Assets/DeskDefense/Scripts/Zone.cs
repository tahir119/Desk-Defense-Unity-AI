using UnityEngine;

namespace DeskDefense
{
    /// <summary>A desk device (LCD / Laptop / Keyboard) that roaches chew on.</summary>
    public class Zone : MonoBehaviour
    {
        public const float MaxHealth = 100f;

        public string zoneName = "Zone";
        public Vector2 size = new Vector2(2f, 1.5f);
        public SpriteRenderer[] flashParts;
        public SpriteRenderer barFill;
        public float barWidth = 2f;
        public float barHeight = 0.18f;

        [System.NonSerialized] public float health = MaxHealth;
        [System.NonSerialized] public bool warned;

        Color[] baseColors;
        float flash;
        float shownHealth = -1f;
        Vector3 basePos;

        static readonly Color BrokenColor = new Color(0.22f, 0.04f, 0.06f);
        static readonly Color HitColor = new Color(1f, 0.25f, 0.25f);

        public bool Alive => health > 0f;

        void Awake()
        {
            basePos = transform.localPosition;
            baseColors = new Color[flashParts.Length];
            for (int i = 0; i < flashParts.Length; i++) baseColors[i] = flashParts[i].color;
        }

        public void ResetZone()
        {
            health = MaxHealth;
            warned = false;
            flash = 0f;
            shownHealth = -1f;
            transform.localPosition = basePos;
            for (int i = 0; i < flashParts.Length; i++) flashParts[i].color = baseColors[i];
            RefreshBar();
        }

        public void Damage(float amount)
        {
            if (health <= 0f) return;
            health -= amount;
            if (health < 0f) health = 0f;
            flash = 0.25f;
        }

        public Vector3 RandomPoint()
        {
            Vector3 p = transform.position;
            p.x += Random.Range(-size.x * 0.4f, size.x * 0.4f);
            p.y += Random.Range(-size.y * 0.35f, size.y * 0.35f);
            return p;
        }

        void Update()
        {
            if (flash > 0f)
            {
                flash -= Time.deltaTime;
                float t = Mathf.Clamp01(flash / 0.25f);
                transform.localPosition = basePos + (Vector3)(Random.insideUnitCircle * 0.04f * t);
                for (int i = 0; i < flashParts.Length; i++)
                {
                    Color target = health <= 0f ? BrokenColor : baseColors[i];
                    flashParts[i].color = Color.Lerp(target, HitColor, t * 0.7f);
                }
                if (flash <= 0f) transform.localPosition = basePos;
            }
            if (!Mathf.Approximately(shownHealth, health)) RefreshBar();
        }

        void RefreshBar()
        {
            shownHealth = health;
            float h = health / MaxHealth;
            barFill.size = new Vector2(Mathf.Max(0.0001f, barWidth * h), barHeight);
            barFill.transform.localPosition = new Vector3(-barWidth * 0.5f + barWidth * h * 0.5f, barFill.transform.localPosition.y, 0f);
            barFill.color = h > 0.6f ? new Color(0.35f, 0.9f, 0.4f) : h > 0.3f ? new Color(1f, 0.8f, 0.2f) : new Color(1f, 0.3f, 0.25f);
            barFill.enabled = h > 0f;
        }
    }
}
