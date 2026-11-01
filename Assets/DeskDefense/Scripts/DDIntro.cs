using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DeskDefense
{
    /// <summary>
    /// Story intro built from placeholder shapes at runtime: profile card -> bike ride -> stairs -> lift to 7th floor
    /// -> corridor to the office -> reception -> Dev Room -> sits at his desk. Tap = next shot, SKIP = skip all.
    /// </summary>
    public class DDIntro : MonoBehaviour
    {
        const int L = 100;            // base sorting order for the intro world
        const float FadeTime = 0.3f;

        public Camera cam;
        public Sprite square, round, circle;
        public Material mat;
        public Image fade;
        public GameObject uiRoot;
        public Text caption;
        public Button skipButton;
        public Text labelTemplate;
        public RectTransform canvasRect;
        public DDAudio sfx;

        // Lines shown one by one on the profile card.
        static readonly string[] ProfileLines =
        {
            "Hi, I'm Tahir. A game developer.",
            "Nine to six, I turn ideas into games... and fight a lot of bugs.",
            "So I turned my workday into a game. Let's go!"
        };

        class Shot
        {
            public Transform root;
            public string caption;
            public float duration;
            public System.Action begin;
            public System.Action<float> step;
            public Vector3[] labelPos;
            public string[] labelText;
            public int[] labelSize;
        }

        class Boy
        {
            public Transform root, legL, legR, arm;
            public bool walking, sitting;
        }

        Shot[] shots;
        Boy boy;
        Transform bike, wheelF, wheelB, worldRoot;
        Text[] labels;
        RectTransform skipRect;
        bool playing, advance, skipAll;
        float walkPhase, lastStepSign = 1f, keyTimer;
        int cue;   // per-shot sound cue progress
        System.Action onDone;
        readonly string[] floorStrings = { "1", "2", "3", "4", "5", "6", "7" };

        static readonly Color Skin = new Color(1f, 0.8f, 0.62f);
        static readonly Color Shirt = new Color(0.95f, 0.55f, 0.2f);
        static readonly Color Jeans = new Color(0.2f, 0.28f, 0.5f);
        static readonly Color Hair = new Color(0.15f, 0.08f, 0.05f);

        public void Play(System.Action done)
        {
            onDone = done;
            skipRect = skipButton.GetComponent<RectTransform>();
            skipButton.onClick.AddListener(() => skipAll = true);
            labels = new Text[5];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = Instantiate(labelTemplate, labelTemplate.transform.parent);
                labels[i].gameObject.SetActive(false);
            }
            Build();
            uiRoot.SetActive(true);
            playing = true;
            sfx.MusicOn();
            StartCoroutine(Sequence());
        }

        void Update()
        {
            if (!playing) return;
            Pointer p = Pointer.current;
            if (p != null && p.press.wasPressedThisFrame &&
                !RectTransformUtility.RectangleContainsScreenPoint(skipRect, p.position.ReadValue(), null))
                advance = true;

            if (boy.walking)
            {
                walkPhase += Time.deltaTime * 11f;
                float s = Mathf.Sin(walkPhase);
                boy.legL.localRotation = Quaternion.Euler(0f, 0f, s * 28f);
                boy.legR.localRotation = Quaternion.Euler(0f, 0f, -s * 28f);
                boy.arm.localRotation = Quaternion.Euler(0f, 0f, -s * 25f);
                float sign = s >= 0f ? 1f : -1f;
                if (sign != lastStepSign) { lastStepSign = sign; sfx.Step(); }
            }
            else if (boy.sitting)
            {
                keyTimer -= Time.deltaTime;
                if (keyTimer <= 0f) { keyTimer = Random.Range(0.07f, 0.16f); sfx.Key(); }
            }
        }

        IEnumerator Sequence()
        {
            for (int i = 0; i < shots.Length && !skipAll; i++)
            {
                Shot s = shots[i];
                cam.transform.position = s.root.position + new Vector3(0f, 0f, -10f);
                if (s.begin != null) s.begin();
                s.step(0f);
                PlaceLabels(s);
                caption.text = s.caption;
                advance = false;
                cue = 0;
                float t = 0f;
                while (t < s.duration && !advance && !skipAll)
                {
                    t += Time.deltaTime;
                    SetFade(1f - t / FadeTime);
                    s.step(Mathf.Clamp01(t / s.duration));
                    yield return null;
                }
                s.step(1f);
                float f = 0f;
                while (f < FadeTime && !skipAll)
                {
                    f += Time.deltaTime;
                    SetFade(f / FadeTime);
                    yield return null;
                }
            }
            Finish();
        }

        void Finish()
        {
            playing = false;
            boy.walking = false;
            boy.sitting = false;
            sfx.LoopOff();
            cam.transform.position = new Vector3(0f, 0f, -10f);
            worldRoot.gameObject.SetActive(false);
            uiRoot.SetActive(false);
            if (onDone != null) onDone();
        }

        void SetFade(float a)
        {
            Color c = fade.color;
            c.a = Mathf.Clamp01(a);
            fade.color = c;
        }

        void PlaceLabels(Shot s)
        {
            for (int i = 0; i < labels.Length; i++)
            {
                bool on = s.labelPos != null && i < s.labelPos.Length;
                labels[i].gameObject.SetActive(on);
                if (!on) continue;
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, cam.WorldToScreenPoint(s.root.position + s.labelPos[i]), null, out local);
                labels[i].rectTransform.anchoredPosition = local;
                labels[i].text = s.labelText[i];
                labels[i].fontSize = s.labelSize != null ? s.labelSize[i] : 52;
            }
        }

        // ---------- Shape helpers ----------

        Transform Node(string name, Transform parent, Vector2 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        SpriteRenderer Box(Transform parent, Vector2 pos, Vector2 size, Color c, int order, bool rounded = false)
        {
            var sr = Node("Box", parent, pos).gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = rounded ? round : square;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.color = c;
            sr.sortingOrder = L + order;
            sr.sharedMaterial = mat;
            return sr;
        }

        SpriteRenderer Disc(Transform parent, Vector2 pos, float d, Color c, int order)
        {
            var sr = Node("Disc", parent, pos).gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = circle;
            sr.transform.localScale = new Vector3(d, d, 1f);
            sr.color = c;
            sr.sortingOrder = L + order;
            sr.sharedMaterial = mat;
            return sr;
        }

        static float Ease(float t) { return t * t * (3f - 2f * t); }

        static float Seg(float t, float a, float b) { return Mathf.Clamp01((t - a) / (b - a)); }

        // ---------- Actors ----------

        Boy MakeBoy()
        {
            var b = new Boy();
            b.root = Node("Tahir", worldRoot, Vector2.zero);
            b.legL = Node("LegL", b.root, new Vector2(-0.12f, 1.0f));
            Box(b.legL, new Vector2(0f, -0.5f), new Vector2(0.28f, 1.0f), Jeans * 0.85f, 21, true);
            b.legR = Node("LegR", b.root, new Vector2(0.12f, 1.0f));
            Box(b.legR, new Vector2(0f, -0.5f), new Vector2(0.28f, 1.0f), Jeans, 24, true);
            Box(b.root, new Vector2(-0.48f, 1.55f), new Vector2(0.32f, 0.85f), new Color(0.25f, 0.6f, 0.35f), 21, true); // backpack
            Box(b.root, new Vector2(0f, 1.5f), new Vector2(0.85f, 1.1f), Shirt, 22, true);
            b.arm = Node("Arm", b.root, new Vector2(0.05f, 1.95f));
            Box(b.arm, new Vector2(0f, -0.4f), new Vector2(0.24f, 0.85f), Shirt * 0.85f, 25, true);
            Disc(b.arm, new Vector2(0f, -0.85f), 0.26f, Skin, 26);
            Disc(b.root, new Vector2(-0.06f, 2.58f), 0.82f, Hair, 22);
            Disc(b.root, new Vector2(0.05f, 2.45f), 0.8f, Skin, 23);
            Disc(b.root, new Vector2(0.27f, 2.5f), 0.11f, Color.black, 24);
            Box(b.root, new Vector2(0.15f, 2.82f), new Vector2(0.6f, 0.2f), Hair, 24, true);
            return b;
        }

        void Pose(bool walking, bool sitting, bool onBike)
        {
            boy.walking = walking;
            boy.sitting = sitting;
            if (!walking)
            {
                float leg = onBike ? 55f : sitting ? 85f : 0f;
                boy.legL.localRotation = Quaternion.Euler(0f, 0f, leg);
                boy.legR.localRotation = Quaternion.Euler(0f, 0f, leg);
                boy.arm.localRotation = Quaternion.Euler(0f, 0f, onBike || sitting ? 70f : 0f);
            }
        }

        void PlaceBoy(Transform parent, Vector2 pos, float facing)
        {
            boy.root.SetParent(parent, false);
            boy.root.localPosition = pos;
            boy.root.localScale = new Vector3(facing, 1f, 1f);
        }

        Transform MakeBike(Transform parent, Color body)
        {
            Transform t = Node("Bike", parent, Vector2.zero);
            Transform wb = Node("WheelB", t, new Vector2(-0.95f, 0.45f));
            Transform wf = Node("WheelF", t, new Vector2(0.95f, 0.45f));
            foreach (Transform w in new[] { wb, wf })
            {
                Disc(w, Vector2.zero, 0.95f, new Color(0.1f, 0.1f, 0.12f), 16);
                Disc(w, Vector2.zero, 0.55f, new Color(0.6f, 0.6f, 0.65f), 17);
                Box(w, Vector2.zero, new Vector2(0.55f, 0.08f), new Color(0.3f, 0.3f, 0.35f), 18);
            }
            Box(t, new Vector2(0f, 0.6f), new Vector2(0.75f, 0.4f), new Color(0.25f, 0.25f, 0.28f), 17, true);
            Box(t, new Vector2(0.05f, 0.92f), new Vector2(1.7f, 0.3f), body, 18, true);
            Box(t, new Vector2(-0.4f, 1.12f), new Vector2(0.75f, 0.16f), new Color(0.1f, 0.1f, 0.1f), 19, true);
            var handle = Box(t, new Vector2(0.8f, 1.25f), new Vector2(0.12f, 0.7f), new Color(0.5f, 0.5f, 0.55f), 19);
            handle.transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
            Disc(t, new Vector2(1.08f, 1.15f), 0.24f, new Color(1f, 0.95f, 0.5f), 20);
            if (wheelF == null) { wheelF = wf; wheelB = wb; }
            return t;
        }

        void Labels(Shot s, params object[] posText)
        {
            int n = posText.Length / 2;
            s.labelPos = new Vector3[n];
            s.labelText = new string[n];
            for (int i = 0; i < n; i++)
            {
                s.labelPos[i] = (Vector2)posText[i * 2];
                s.labelText[i] = (string)posText[i * 2 + 1];
            }
        }

        // ---------- Shots ----------

        void Build()
        {
            worldRoot = Node("IntroWorld", null, Vector2.zero);
            boy = MakeBoy();
            shots = new Shot[7];
            for (int i = 0; i < shots.Length; i++)
                shots[i] = new Shot { root = Node("Shot" + i, worldRoot, new Vector2(1000f + i * 40f, 0f)) };

            BuildProfile(shots[0]);
            BuildStreet(shots[1]);
            BuildStairs(shots[2]);
            BuildLift(shots[3]);
            BuildCorridor(shots[4]);
            BuildReception(shots[5]);
            BuildDevRoom(shots[6]);
        }

        /// <summary>CV-style card introducing Tahir, with his lines shown one at a time.</summary>
        void BuildProfile(Shot s)
        {
            Transform r = s.root;
            Box(r, Vector2.zero, new Vector2(16f, 22f), new Color(0.1f, 0.05f, 0.16f), 0);
            Box(r, new Vector2(0f, 1.2f), new Vector2(8.6f, 10.6f), new Color(0.98f, 0.75f, 0.25f), 1, true);
            Box(r, new Vector2(0f, 1.2f), new Vector2(8.3f, 10.3f), new Color(0.17f, 0.11f, 0.27f), 2, true);
            Disc(r, new Vector2(0f, 4.0f), 3.2f, new Color(0.98f, 0.75f, 0.25f), 3);
            Disc(r, new Vector2(0f, 4.0f), 2.95f, new Color(0.3f, 0.55f, 0.85f), 4);
            Disc(r, new Vector2(0f, 4.35f), 1.95f, Hair, 5);
            Disc(r, new Vector2(0f, 4.0f), 1.85f, Skin, 6);
            Box(r, new Vector2(0.1f, 4.75f), new Vector2(1.6f, 0.45f), Hair, 7, true);
            for (int i = -1; i <= 1; i += 2)
            {
                Disc(r, new Vector2(i * 0.38f, 4.05f), 0.55f, new Color(0.1f, 0.06f, 0.12f), 7);
                Disc(r, new Vector2(i * 0.38f, 4.05f), 0.42f, Color.white, 8);
                Disc(r, new Vector2(i * 0.36f, 4.0f), 0.16f, new Color(0.1f, 0.06f, 0.12f), 9);
            }
            Box(r, new Vector2(0f, 3.45f), new Vector2(0.5f, 0.12f), new Color(0.55f, 0.2f, 0.2f), 8, true);
            Box(r, new Vector2(0f, -2.3f), new Vector2(6.4f, 0.06f), new Color(1f, 1f, 1f, 0.25f), 3);

            s.caption = ProfileLines[0];
            float total = 0.8f;
            for (int i = 0; i < ProfileLines.Length; i++) total += LineLength(i) + 0.35f;
            s.duration = total;
            Labels(s, new Vector2(0f, 1.2f), "TAHIR", new Vector2(0f, 0.15f), "Game Developer",
                   new Vector2(0f, -1.0f), "9 AM - 6 PM", new Vector2(0f, -3.3f), "DESK DEFENSE");
            s.labelSize = new[] { 96, 54, 46, 40 };
            s.begin = () => { boy.root.SetParent(r, false); boy.root.localPosition = new Vector3(0f, -50f, 0f); };
            s.step = t =>
            {
                // which line should be on screen at this point of the shot
                float at = t * s.duration - 0.4f, start = 0f;
                int line = -1;
                for (int i = 0; i < ProfileLines.Length; i++)
                {
                    if (at >= start) line = i;
                    start += LineLength(i) + 0.35f;
                }
                if (line >= 0 && line + 1 != cue && t < 1f)
                {
                    cue = line + 1;
                    caption.text = ProfileLines[line];
                }
            };
        }

        float LineLength(int i)
        {
            return 2.8f;
        }

        void BuildStreet(Shot s)
        {
            Transform r = s.root;
            Box(r, new Vector2(0f, 0f), new Vector2(16f, 22f), new Color(0.45f, 0.72f, 0.95f), 0);
            Disc(r, new Vector2(-3.6f, 7.2f), 1.6f, new Color(1f, 0.92f, 0.5f), 1);
            Box(r, new Vector2(1.6f, 2.2f), new Vector2(6.2f, 13f), new Color(0.35f, 0.42f, 0.6f), 2);
            for (int y = 0; y < 9; y++)
                for (int x = 0; x < 4; x++)
                    Box(r, new Vector2(-0.6f + x * 1.45f, -2.6f + y * 1.25f), new Vector2(0.9f, 0.8f), y == 6 ? new Color(1f, 0.9f, 0.5f) : new Color(0.65f, 0.82f, 1f), 3);
            Box(r, new Vector2(1.6f, -3.9f), new Vector2(1.6f, 1.6f), new Color(0.2f, 0.2f, 0.25f), 4);
            Box(r, new Vector2(0f, -4.85f), new Vector2(16f, 0.5f), new Color(0.7f, 0.7f, 0.72f), 5);
            Box(r, new Vector2(0f, -7.2f), new Vector2(16f, 4.2f), new Color(0.22f, 0.22f, 0.26f), 5);
            for (int i = 0; i < 5; i++) Box(r, new Vector2(-6f + i * 3f, -7.2f), new Vector2(1.4f, 0.15f), Color.white, 6);
            Transform b = MakeBike(r, new Color(0.85f, 0.15f, 0.2f));
            s.caption = "9:00 AM. Tahir rides to the office.";
            s.duration = 3.2f;
            Labels(s, new Vector2(1.6f, 9.2f), "SOFTWARE HOUSE");
            s.begin = () => { PlaceBoy(b, new Vector2(-0.35f, 0.15f), 1f); Pose(false, false, true); sfx.EngineOn(); };
            s.step = t =>
            {
                b.localPosition = new Vector3(Mathf.Lerp(-7.5f, -1.6f, Ease(t)), -5.6f, 0f);
                wheelF.localRotation = wheelB.localRotation = Quaternion.Euler(0f, 0f, -t * 1400f);
            };
        }

        void BuildStairs(Shot s)
        {
            Transform r = s.root;
            Box(r, Vector2.zero, new Vector2(16f, 22f), new Color(0.82f, 0.76f, 0.66f), 0);
            Box(r, new Vector2(0f, -7.5f), new Vector2(16f, 5f), new Color(0.55f, 0.45f, 0.38f), 1);
            for (int i = 0; i < 7; i++)
                Box(r, new Vector2(-1.6f + i * 0.85f, -4.8f + i * 0.6f), new Vector2(1.75f, 0.6f), new Color(0.65f - i * 0.02f, 0.58f - i * 0.02f, 0.5f), 2 + i);
            Box(r, new Vector2(3.6f, -0.95f), new Vector2(4f, 0.6f), new Color(0.65f, 0.58f, 0.5f), 9);
            Box(r, new Vector2(3.9f, 1.3f), new Vector2(2.2f, 3.6f), new Color(0.7f, 0.72f, 0.76f), 3, true);
            Box(r, new Vector2(3.35f, 1.2f), new Vector2(1.0f, 3.2f), new Color(0.5f, 0.52f, 0.58f), 4);
            Box(r, new Vector2(4.45f, 1.2f), new Vector2(1.0f, 3.2f), new Color(0.5f, 0.52f, 0.58f), 4);
            Disc(r, new Vector2(2.4f, 1.4f), 0.35f, new Color(1f, 0.6f, 0.2f), 5);
            s.caption = "Up the stairs to the lift.";
            s.duration = 3.4f;
            Labels(s, new Vector2(3.9f, 3.6f), "LIFT");
            s.begin = () => { PlaceBoy(r, new Vector2(-5.5f, -5.0f), 1f); Pose(true, false, false); sfx.LoopOff(); };
            s.step = t =>
            {
                Vector2 a = new Vector2(-5.5f, -5.0f), b = new Vector2(-2.0f, -4.6f), c = new Vector2(3.2f, -0.65f), d = new Vector2(3.9f, -0.65f);
                Vector2 pos = t < 0.25f ? Vector2.Lerp(a, b, t / 0.25f) : t < 0.8f ? Vector2.Lerp(b, c, (t - 0.25f) / 0.55f) : Vector2.Lerp(c, d, (t - 0.8f) / 0.2f);
                if (t >= 0.25f && t < 0.8f) pos.y += Mathf.Abs(Mathf.Sin(t * 60f)) * 0.12f;
                boy.root.localPosition = pos;
            };
        }

        void BuildLift(Shot s)
        {
            Transform r = s.root;
            Box(r, Vector2.zero, new Vector2(16f, 22f), new Color(0.3f, 0.3f, 0.36f), 0);
            Box(r, new Vector2(0f, -0.5f), new Vector2(6.4f, 9.5f), new Color(0.72f, 0.74f, 0.8f), 1, true);
            Box(r, new Vector2(0f, -0.6f), new Vector2(5.6f, 8.6f), new Color(0.85f, 0.86f, 0.9f), 2);
            Box(r, new Vector2(0f, -4.9f), new Vector2(5.6f, 0.3f), new Color(0.45f, 0.45f, 0.5f), 3);
            Box(r, new Vector2(0f, 5.2f), new Vector2(2.2f, 1.2f), new Color(0.05f, 0.05f, 0.08f), 3, true);
            var doorL = Box(r, new Vector2(-1.4f, -0.6f), new Vector2(2.76f, 8.6f), new Color(0.6f, 0.62f, 0.68f), 30);
            var doorR = Box(r, new Vector2(1.4f, -0.6f), new Vector2(2.76f, 8.6f), new Color(0.56f, 0.58f, 0.64f), 30);
            Box(r, new Vector2(0f, -0.6f), new Vector2(0.06f, 8.6f), new Color(0.3f, 0.3f, 0.35f), 29);   // seam visible between closed doors
            s.caption = "Up to the 7th floor.";
            s.duration = 3.6f;
            Labels(s, new Vector2(0f, 5.2f), "1");
            s.begin = () => { PlaceBoy(r, new Vector2(0f, -4.75f), 1f); Pose(false, false, false); sfx.LoopOff(); };
            s.step = t =>
            {
                float open = t < 0.25f ? 1f - Seg(t, 0.08f, 0.25f) : Seg(t, 0.82f, 1f);
                float off = open * 2.7f;
                doorL.transform.localPosition = new Vector3(-1.4f - off, -0.6f, 0f);
                doorR.transform.localPosition = new Vector3(1.4f + off, -0.6f, 0f);
                if (cue == 0 && t > 0.08f) { cue = 1; sfx.Door(); }
                if (cue == 1 && t > 0.25f) { cue = 2; sfx.LiftHumOn(); }
                if (cue == 2 && t > 0.8f) { cue = 3; sfx.LoopOff(); sfx.Ding(); sfx.Door(); }
                int floor = Mathf.Clamp(Mathf.FloorToInt(Seg(t, 0.25f, 0.8f) * 6.99f), 0, 6);
                if (labels[0].text != floorStrings[floor]) labels[0].text = floorStrings[floor];
            };
        }

        void BuildCorridor(Shot s)
        {
            Transform r = s.root;
            Box(r, Vector2.zero, new Vector2(16f, 22f), new Color(0.9f, 0.86f, 0.78f), 0);
            Box(r, new Vector2(0f, -7f), new Vector2(16f, 6f), new Color(0.5f, 0.38f, 0.3f), 1);
            Box(r, new Vector2(0f, -4.1f), new Vector2(16f, 0.3f), new Color(0.4f, 0.3f, 0.24f), 2);
            float[] xs = { -3.6f, 0f, 3.6f };
            SpriteRenderer officeDoor = null;
            for (int i = 0; i < 3; i++)
            {
                Box(r, new Vector2(xs[i], -1.9f), new Vector2(2.0f, 4.2f), new Color(0.35f, 0.25f, 0.2f), 2, true);
                var d = Box(r, new Vector2(xs[i], -1.95f), new Vector2(1.7f, 3.9f), i == 2 ? new Color(0.3f, 0.6f, 0.9f) : new Color(0.6f, 0.42f, 0.28f), 3, true);
                Disc(r, new Vector2(xs[i] + 0.55f, -2.1f), 0.18f, new Color(0.95f, 0.8f, 0.3f), 4);
                if (i == 2) officeDoor = d;
            }
            Box(r, new Vector2(3.6f, -1.95f), new Vector2(1.7f, 3.9f), new Color(0.15f, 0.12f, 0.18f), 2);
            s.caption = "The office.";
            s.duration = 3.4f;
            Labels(s, new Vector2(3.6f, 0.9f), "OFFICE");
            s.begin = () => { PlaceBoy(r, new Vector2(-7f, -4.0f), 1f); Pose(true, false, false); };
            s.step = t =>
            {
                boy.root.localPosition = new Vector3(Mathf.Lerp(-7f, 3.6f, Seg(t, 0f, 0.8f)), -4.0f, 0f);
                float open = Seg(t, 0.7f, 0.85f);
                if (cue == 0 && t > 0.7f) { cue = 1; sfx.Door(); }
                officeDoor.size = new Vector2(Mathf.Lerp(1.7f, 0.25f, open), 3.9f);
                officeDoor.transform.localPosition = new Vector3(3.6f + Mathf.Lerp(0f, 0.72f, open), -1.95f, 0f);
                if (t > 0.85f) { boy.walking = false; boy.root.localScale = new Vector3(Mathf.Lerp(1f, 0.85f, Seg(t, 0.85f, 1f)), 1f, 1f); }
            };
        }

        void BuildReception(Shot s)
        {
            Transform r = s.root;
            Box(r, Vector2.zero, new Vector2(16f, 22f), new Color(0.75f, 0.85f, 0.88f), 0);
            Box(r, new Vector2(0f, -7f), new Vector2(16f, 6f), new Color(0.6f, 0.55f, 0.5f), 1);
            Box(r, new Vector2(-2.6f, -3.2f), new Vector2(2.8f, 1.6f), new Color(0.4f, 0.3f, 0.5f), 3, true);      // sofa
            Box(r, new Vector2(-2.6f, -2.2f), new Vector2(2.8f, 0.8f), new Color(0.45f, 0.35f, 0.55f), 2, true);
            Box(r, new Vector2(0.6f, -3.1f), new Vector2(2.2f, 1.8f), new Color(0.85f, 0.85f, 0.88f), 3, true);     // reception desk
            Box(r, new Vector2(-4.7f, -3.3f), new Vector2(0.6f, 1.4f), new Color(0.6f, 0.4f, 0.25f), 3, true);      // plant pot
            Disc(r, new Vector2(-4.7f, -2.2f), 1.3f, new Color(0.25f, 0.65f, 0.3f), 4);
            Box(r, new Vector2(1.2f, 2.0f), new Vector2(4f, 1.0f), new Color(0.15f, 0.35f, 0.6f), 2, true);        // logo board
            Box(r, new Vector2(4.2f, -1.9f), new Vector2(1.9f, 4.2f), new Color(0.15f, 0.12f, 0.18f), 2, true);    // dev room door
            s.caption = "Through reception to the Dev Room.";
            s.duration = 3.0f;
            Labels(s, new Vector2(4.2f, 0.9f), "DEV ROOM");
            s.begin = () => { PlaceBoy(r, new Vector2(-6.5f, -4.0f), 1f); Pose(true, false, false); };
            s.step = t => boy.root.localPosition = new Vector3(Mathf.Lerp(-6.5f, 4.2f, t), -4.0f, 0f);
        }

        void BuildDevRoom(Shot s)
        {
            Transform r = s.root;
            Box(r, Vector2.zero, new Vector2(16f, 22f), new Color(0.22f, 0.15f, 0.32f), 0);
            Box(r, new Vector2(0f, -7f), new Vector2(16f, 6f), new Color(0.14f, 0.09f, 0.2f), 1);
            float[] xs = { -3.8f, 1.2f };
            for (int i = 0; i < 2; i++)
            {
                Box(r, new Vector2(xs[i], -2.6f), new Vector2(3.4f, 0.3f), new Color(0.66f, 0.43f, 0.24f), 5, true);
                Box(r, new Vector2(xs[i] - 1.4f, -3.3f), new Vector2(0.2f, 1.3f), new Color(0.5f, 0.31f, 0.17f), 4);
                Box(r, new Vector2(xs[i] + 1.4f, -3.3f), new Vector2(0.2f, 1.3f), new Color(0.5f, 0.31f, 0.17f), 4);
                Box(r, new Vector2(xs[i] + 0.4f, -1.5f), new Vector2(1.8f, 1.3f), new Color(0.13f, 0.13f, 0.17f), 6, true);
                Box(r, new Vector2(xs[i] + 0.4f, -1.45f), new Vector2(1.55f, 1.0f), new Color(0.15f, 0.3f, 0.55f), 7);
            }
            // a colleague already typing at the first desk
            Box(r, new Vector2(-4.6f, -3.0f), new Vector2(0.8f, 1.2f), new Color(0.3f, 0.7f, 0.5f), 3, true);
            Disc(r, new Vector2(-4.6f, -2.0f), 0.75f, new Color(0.9f, 0.7f, 0.55f), 4);
            // Tahir's chair
            Box(r, new Vector2(0.2f, -3.3f), new Vector2(1.0f, 0.2f), new Color(0.2f, 0.2f, 0.25f), 8, true);
            Box(r, new Vector2(-0.25f, -2.5f), new Vector2(0.2f, 1.6f), new Color(0.2f, 0.2f, 0.25f), 8, true);
            Box(r, new Vector2(0.2f, -3.9f), new Vector2(0.12f, 1.0f), new Color(0.3f, 0.3f, 0.35f), 8);
            s.caption = "At his desk. Time to code!";
            s.duration = 3.4f;
            Labels(s, new Vector2(0.7f, 0f), "Tahir's Desk");
            s.begin = () => { PlaceBoy(r, new Vector2(-6.5f, -4.4f), 1f); Pose(true, false, false); };
            s.step = t =>
            {
                if (t < 0.6f)
                {
                    boy.root.localPosition = new Vector3(Mathf.Lerp(-6.5f, 0.2f, t / 0.6f), -4.4f, 0f);
                }
                else
                {
                    if (!boy.sitting) Pose(false, true, false);
                    boy.root.localPosition = new Vector3(0.1f, Mathf.Lerp(-4.4f, -4.25f, Seg(t, 0.6f, 0.7f)), 0f);
                    boy.arm.localRotation = Quaternion.Euler(0f, 0f, 70f + Mathf.Sin(t * 120f) * 8f);
                }
            };
        }
    }
}
