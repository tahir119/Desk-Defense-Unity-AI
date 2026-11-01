using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DeskDefense
{
    /// <summary>Game loop: idle coding progress, roach spawning, tap handling, combo, UI, win/lose.</summary>
    public class DDGame : MonoBehaviour
    {
        enum State { Ready, Playing, Paused, Over }

        const float RoundTime = 60f;
        const float FullSpeedSeconds = 34f;   // time to 100% with all zones healthy
        const float ComboWindow = 2f;
        const int MaxRoaches = 25;
        const int MaxStars = 120;
        const int MaxFloats = 14;
        const string BestKey = "dd_best";
        public const int LayerRoaches = 40;   // each pooled roach gets LayerRoaches + index so they never flicker
        public const int LayerStars = 70;
        const float ChaiSlow = 0.35f, ChaiSlowTime = 3.5f, ChaiLife = 4.5f;
        const int ClockSteps = 54;            // 9:00 AM -> 6:00 PM in 10-minute steps
        const float DesignHalfHeight = 9.6f, DesignHalfWidth = 5.4f;   // 1080x1920 at 100 px per unit
        const float TableTop = 5.9f, TableBottom = -1.8f, TableHalfWidth = 4.8f;   // matches Table in DeskDefenseSetup

        [Header("World")]
        public Camera cam;
        public CameraShake shake;
        public DDAudio sfx;
        public Zone[] zones;
        public Roach roachTemplate;
        public SpriteRenderer starTemplate;
        public Transform devHead, handL, handR;
        public Transform chai;

        [Header("Story")]
        public DDIntro intro;

        [Header("HUD")]
        public GameObject hud;
        public RectTransform canvasRect;
        public Image codeFill;
        public Text percentText, timerText, scoreText, comboText;
        public Button pauseButton;

        [Header("FX UI")]
        public Text floatTemplate;
        public Text roachLabelTemplate;
        public RectTransform bubble;
        public Text bubbleText;

        [Header("Panels")]
        public GameObject startPanel, pausePanel, endPanel;
        public Button playButton, resumeButton, pauseRetryButton, endRetryButton;
        public Text endTitle, endScore, endBest, endStats;

        static readonly string[] Lines =
        {
            "It's not a bug, it's a feature!", "Deadline is tomorrow!", "Works on my machine!", "One more chai...",
            "Ctrl+S, Ctrl+S, Ctrl+S", "Is Stack Overflow down?!", "Just a 5-minute fix..."
        };
        static readonly string[] HurtLines = { "My monitor!!", "Save the laptop!", "Off my keyboard!" };
        static readonly string[] NormalNames = { "NullReference", "IndexOutOfRange", "Typo", "404", "Missing ;", "Off-by-one" };
        static readonly Color NormalLabel = new Color(1f, 1f, 1f, 0.9f);
        static readonly Color SpeedyLabel = new Color(1f, 0.7f, 0.4f);
        static readonly Color ArmoredLabel = new Color(0.65f, 0.8f, 1f);

        State state;
        float timeLeft, elapsed, progress, spawnTimer, lastKillTime, comboAnim, bubbleTimer, bubbleNext, typePhase;
        float chaiTimer, chaiLife, slowTimer;
        int score, combo, shownPercent = -1, shownSeconds = -1, level, bugsFixed, bestCombo;
        bool lastTypeUp;
        float lastAspect;

        Roach[] roaches;
        Text[] roachLabels;
        RectTransform[] roachLabelRects;
        Vector3 chaiBaseScale;
        Transform[] stars;
        SpriteRenderer[] starRenderers;
        Vector3[] starVel;
        float[] starLife;
        RectTransform[] floats;
        Text[] floatTexts;
        float[] floatLife;
        Vector2[] floatStart;
        int nextStar, nextFloat;

        Vector3 headBase, handLBase, handRBase;
        RectTransform pauseRect;
        Vector3 bubbleScale = Vector3.one;

        readonly string[] percentStrings = new string[101];
        readonly string[] clockStrings = new string[ClockSteps + 1];
        readonly string[] comboStrings = new string[11];
        readonly string[] pointStrings = new string[11];

        void Awake()
        {
            Application.targetFrameRate = 60;
            FitCamera();
            for (int i = 0; i <= 100; i++) percentStrings[i] = i + "%";
            for (int i = 0; i <= ClockSteps; i++)
            {
                int minutes = 9 * 60 + i * 10, h = minutes / 60, m = minutes % 60;
                clockStrings[i] = (h > 12 ? h - 12 : h) + ":" + m.ToString("00") + (h >= 12 ? " PM" : " AM");
            }
            for (int i = 0; i <= 10; i++) { comboStrings[i] = "x" + i + " COMBO!"; pointStrings[i] = "+" + (i * 10); }

            roaches = new Roach[MaxRoaches];
            Transform roachParent = new GameObject("Roaches").transform;
            for (int i = 0; i < MaxRoaches; i++)
            {
                roaches[i] = Instantiate(roachTemplate, roachParent);
                roaches[i].body.sortingOrder = LayerRoaches + i;
                roaches[i].gameObject.SetActive(false);
            }
            roachLabels = new Text[MaxRoaches];
            roachLabelRects = new RectTransform[MaxRoaches];
            for (int i = 0; i < MaxRoaches; i++)
            {
                roachLabels[i] = Instantiate(roachLabelTemplate, roachLabelTemplate.transform.parent);
                roachLabelRects[i] = roachLabels[i].rectTransform;
                roachLabels[i].gameObject.SetActive(false);
            }
            chaiBaseScale = chai.localScale;

            stars = new Transform[MaxStars];
            starRenderers = new SpriteRenderer[MaxStars];
            starVel = new Vector3[MaxStars];
            starLife = new float[MaxStars];
            Transform starParent = new GameObject("Stars").transform;
            for (int i = 0; i < MaxStars; i++)
            {
                starRenderers[i] = Instantiate(starTemplate, starParent);
                stars[i] = starRenderers[i].transform;
                starRenderers[i].gameObject.SetActive(false);
            }

            floats = new RectTransform[MaxFloats];
            floatTexts = new Text[MaxFloats];
            floatLife = new float[MaxFloats];
            floatStart = new Vector2[MaxFloats];
            for (int i = 0; i < MaxFloats; i++)
            {
                floatTexts[i] = Instantiate(floatTemplate, floatTemplate.transform.parent);
                floats[i] = floatTexts[i].rectTransform;
                floatTexts[i].gameObject.SetActive(false);
            }

            headBase = devHead.localPosition;
            handLBase = handL.localPosition;
            handRBase = handR.localPosition;
            pauseRect = pauseButton.GetComponent<RectTransform>();

            playButton.onClick.AddListener(StartGame);
            pauseButton.onClick.AddListener(Pause);
            resumeButton.onClick.AddListener(Resume);
            pauseRetryButton.onClick.AddListener(Retry);
            endRetryButton.onClick.AddListener(Retry);
        }

        void Start()
        {
            // Bubble sits just below/right of the developer's head (developer is at the bottom, facing the table).
            Vector2 local;
            Vector3 headTop = devHead.position + new Vector3(1.4f, -2.1f, 0f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, cam.WorldToScreenPoint(headTop), null, out local);
            bubble.anchoredPosition = local;
            ResetGame();
            state = State.Ready;
            if (intro != null)
            {
                hud.SetActive(false);
                intro.Play(OnIntroDone);
            }
            else startPanel.SetActive(true);
        }

        /// <summary>Keeps the whole table (10.8 units wide) visible on tall/narrow phones and browser windows.</summary>
        void FitCamera()
        {
            lastAspect = cam.aspect;
            cam.orthographicSize = Mathf.Max(DesignHalfHeight, DesignHalfWidth / lastAspect);
        }

        void OnIntroDone()
        {
            hud.SetActive(true);
            startPanel.SetActive(true);
        }

        // ---------- State ----------

        void ResetGame()
        {
            Time.timeScale = 1f;
            for (int i = 0; i < roaches.Length; i++) { roaches[i].gameObject.SetActive(false); roachLabels[i].gameObject.SetActive(false); }
            chai.gameObject.SetActive(false);
            chaiTimer = 8f;
            slowTimer = 0f;
            bugsFixed = 0;
            bestCombo = 0;
            for (int i = 0; i < stars.Length; i++) { starLife[i] = 0f; starRenderers[i].gameObject.SetActive(false); }
            for (int i = 0; i < floats.Length; i++) { floatLife[i] = 0f; floatTexts[i].gameObject.SetActive(false); }
            for (int i = 0; i < zones.Length; i++) zones[i].ResetZone();
            timeLeft = RoundTime;
            elapsed = 0f;
            progress = 0f;
            spawnTimer = 1.2f;
            score = 0;
            combo = 0;
            lastKillTime = -10f;
            level = 0;
            shownPercent = -1;
            shownSeconds = -1;
            comboText.gameObject.SetActive(false);
            bubble.gameObject.SetActive(false);
            bubbleNext = 2.5f;
            scoreText.text = "SCORE 0";
            timerText.color = Color.white;
            RefreshHud();
            startPanel.SetActive(false);
            pausePanel.SetActive(false);
            endPanel.SetActive(false);
        }

        public void StartGame()
        {
            sfx.Click();
            ResetGame();
            state = State.Playing;
            sfx.MusicOn();
            Say("Let's write some code!");
        }

        public void Pause()
        {
            if (state != State.Playing) return;
            sfx.Click();
            state = State.Paused;
            Time.timeScale = 0f;
            pausePanel.SetActive(true);
        }

        public void Resume()
        {
            if (state != State.Paused) return;
            sfx.Click();
            state = State.Playing;
            Time.timeScale = 1f;
            pausePanel.SetActive(false);
        }

        public void Retry()
        {
            StartGame();
        }

        void EndGame(bool won)
        {
            state = State.Over;
            sfx.MusicOff();
            comboText.gameObject.SetActive(false);
            if (won)
            {
                float hp = 0f;
                for (int i = 0; i < zones.Length; i++) hp += zones[i].health;
                score += Mathf.CeilToInt(timeLeft) * 25 + Mathf.RoundToInt(hp);
                sfx.Win();
                Say("Deployed! Chai time!");
            }
            else
            {
                sfx.Lose();
                shake.Shake(0.35f, 0.5f);
                Say("The server is down...");
            }
            int best = PlayerPrefs.GetInt(BestKey, 0);
            bool newBest = score > best;
            if (newBest) { best = score; PlayerPrefs.SetInt(BestKey, best); PlayerPrefs.Save(); }

            endTitle.text = won ? "DEPLOYED!" : "SERVER DOWN";
            endTitle.color = won ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.35f, 0.3f);
            endScore.text = "Score: " + score;
            scoreText.text = "SCORE " + score;
            endBest.text = newBest ? "NEW BEST: " + best : "Best: " + best;
            endStats.text = "Bugs fixed: " + bugsFixed +
                            (won ? "\nDeployed at: " + clockStrings[ClockIndex()] : "\nCode written: " + percentStrings[shownPercent]) +
                            "\nBest combo: x" + Mathf.Max(1, bestCombo);
            for (int i = 0; i < roachLabels.Length; i++) roachLabels[i].gameObject.SetActive(false);
            chai.gameObject.SetActive(false);
            endPanel.SetActive(true);
        }

        // ---------- Loop ----------

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (state == State.Playing) Pause();
                else if (state == State.Paused) Resume();
            }

            if (!Mathf.Approximately(cam.aspect, lastAspect)) FitCamera();

            float dt = Time.deltaTime;
            TickFx(dt);
            if (state != State.Playing) return;

            elapsed += dt;
            timeLeft -= dt;
            level = Mathf.Min(9, (int)(elapsed / 10f));

            spawnTimer -= dt;
            if (spawnTimer <= 0f)
            {
                SpawnRoach();
                spawnTimer = 1.7f * Mathf.Pow(0.87f, level) * Random.Range(0.8f, 1.2f);
            }

            float total = 0f;
            for (int i = 0; i < zones.Length; i++) total += zones[i].health;
            float efficiency = total / (Zone.MaxHealth * zones.Length);
            progress += dt / FullSpeedSeconds * efficiency;
            AnimateDeveloper(dt, efficiency);
            CheckZoneWarnings();

            HandleTap();
            TickChai(dt);

            float roachDt = slowTimer > 0f ? dt * ChaiSlow : dt;
            for (int i = 0; i < roaches.Length; i++)
                if (roaches[i].gameObject.activeSelf) roaches[i].Tick(roachDt, this);
            UpdateRoachLabels();

            if (combo > 0 && elapsed - lastKillTime > ComboWindow)
            {
                combo = 0;
                comboText.gameObject.SetActive(false);
            }

            RefreshHud();

            if (progress >= 1f) { progress = 1f; RefreshHud(); EndGame(true); }
            else if (total <= 0f || timeLeft <= 0f) { timeLeft = Mathf.Max(0f, timeLeft); RefreshHud(); EndGame(false); }
        }

        void RefreshHud()
        {
            int pct = Mathf.Clamp((int)(progress * 100f), 0, 100);
            if (pct != shownPercent)
            {
                shownPercent = pct;
                percentText.text = percentStrings[pct];
                codeFill.fillAmount = progress;
            }
            int clock = ClockIndex();
            if (clock != shownSeconds)
            {
                shownSeconds = clock;
                timerText.text = clockStrings[clock];
                timerText.color = clock >= ClockSteps - 6 ? new Color(1f, 0.4f, 0.35f) : Color.white;
            }
        }

        /// <summary>The 60 s round is shown as a workday clock, 9:00 AM to 6:00 PM.</summary>
        int ClockIndex()
        {
            return Mathf.Clamp((int)((RoundTime - timeLeft) / RoundTime * ClockSteps), 0, ClockSteps);
        }

        void UpdateRoachLabels()
        {
            Vector2 local;
            for (int i = 0; i < roaches.Length; i++)
            {
                bool show = roaches[i].Tappable;
                if (roachLabels[i].gameObject.activeSelf != show) roachLabels[i].gameObject.SetActive(show);
                if (!show) continue;
                Vector3 p = roaches[i].transform.position;
                p.y += 0.6f;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, cam.WorldToScreenPoint(p), null, out local);
                roachLabelRects[i].anchoredPosition = local;
            }
        }

        // ---------- Chai power-up ----------

        void TickChai(float dt)
        {
            if (slowTimer > 0f) slowTimer -= dt;
            if (chai.gameObject.activeSelf)
            {
                chaiLife -= dt;
                float pulse = 1f + Mathf.Sin(elapsed * 10f) * 0.08f;
                chai.localScale = chaiBaseScale * pulse;
                if (chaiLife <= 0f) chai.gameObject.SetActive(false);
                return;
            }
            chaiTimer -= dt;
            if (chaiTimer > 0f) return;
            chaiTimer = Random.Range(9f, 12f);
            chaiLife = ChaiLife;
            chai.position = new Vector3(Random.Range(-TableHalfWidth + 0.8f, TableHalfWidth - 0.8f), Random.Range(0.5f, TableTop - 1.2f), 0f);
            chai.gameObject.SetActive(true);
        }

        bool TryTapChai(Vector3 world)
        {
            if (!chai.gameObject.activeSelf) return false;
            Vector3 d = chai.position - world;
            d.z = 0f;
            if (d.sqrMagnitude > 0.8f * 0.8f) return false;
            chai.gameObject.SetActive(false);
            slowTimer = ChaiSlowTime;
            sfx.Ding();
            EmitStars(chai.position, 8, 3.5f, new Color(1f, 0.85f, 0.6f));
            ShowFloat(chai.position + new Vector3(0f, 0.5f, 0f), "CHAI TIME! SLOW-MO", new Color(1f, 0.8f, 0.5f), 56);
            Say("Chai = debugging fuel!");
            return true;
        }

        void AnimateDeveloper(float dt, float efficiency)
        {
            typePhase += dt * (6f + 14f * efficiency);
            float a = Mathf.Sin(typePhase), b = Mathf.Sin(typePhase + 2.4f);
            bool up = a >= 0f;
            if (up != lastTypeUp) { lastTypeUp = up; if (efficiency > 0f) sfx.Key(); }
            handL.localPosition = handLBase + new Vector3(0f, Mathf.Max(0f, a) * 0.12f, 0f);
            handR.localPosition = handRBase + new Vector3(0f, Mathf.Max(0f, b) * 0.12f, 0f);
            devHead.localPosition = headBase + new Vector3(0f, Mathf.Sin(typePhase * 0.5f) * 0.03f, 0f);
            devHead.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * 1.3f) * 4f);
        }

        void CheckZoneWarnings()
        {
            for (int i = 0; i < zones.Length; i++)
            {
                if (!zones[i].warned && zones[i].health < 50f)
                {
                    zones[i].warned = true;
                    sfx.Hurt();
                    Say(HurtLines[i % HurtLines.Length]);
                }
            }
        }

        // ---------- Spawning ----------

        public Zone RandomAliveZone()
        {
            int alive = 0;
            for (int i = 0; i < zones.Length; i++) if (zones[i].Alive) alive++;
            if (alive == 0) return null;
            int pick = Random.Range(0, alive);
            for (int i = 0; i < zones.Length; i++)
            {
                if (!zones[i].Alive) continue;
                if (pick == 0) return zones[i];
                pick--;
            }
            return null;
        }

        void SpawnRoach()
        {
            Roach r = null;
            int index = -1;
            for (int i = 0; i < roaches.Length; i++)
                if (!roaches[i].gameObject.activeSelf) { r = roaches[i]; index = i; break; }
            if (r == null) return;
            Zone z = RandomAliveZone();
            if (z == null) return;

            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            Vector3 c = cam.transform.position;
            Vector3 pos;
            // Only from the table's left, right and top edges (never from the bottom where the dev sits).
            switch (Random.Range(0, 3))
            {
                case 0: pos = new Vector3(-halfW - 0.6f, Random.Range(TableBottom, TableTop), 0f); break;
                case 1: pos = new Vector3(halfW + 0.6f, Random.Range(TableBottom, TableTop), 0f); break;
                default: pos = new Vector3(Random.Range(-TableHalfWidth, TableHalfWidth), TableTop + 0.6f, 0f); break;
            }
            pos.x += c.x;
            pos.y += c.y;

            float roll = Random.value;
            float armoredChance = level >= 3 ? 0.1f + 0.02f * level : 0f;
            float speedyChance = level >= 1 ? 0.2f : 0f;
            RoachType type = roll < armoredChance ? RoachType.Armored : roll < armoredChance + speedyChance ? RoachType.Speedy : RoachType.Normal;
            r.Spawn(type, pos, z);

            Text label = roachLabels[index];
            label.text = type == RoachType.Armored ? "Merge Conflict" : type == RoachType.Speedy ? "Race Condition" : NormalNames[Random.Range(0, NormalNames.Length)];
            label.color = type == RoachType.Armored ? ArmoredLabel : type == RoachType.Speedy ? SpeedyLabel : NormalLabel;
        }

        // ---------- Tap ----------

        void HandleTap()
        {
            Pointer p = Pointer.current;
            if (p == null || !p.press.wasPressedThisFrame) return;
            TapAt(p.position.ReadValue());
        }

        public void TapAt(Vector2 screen)
        {
            if (state != State.Playing) return;
            if (RectTransformUtility.RectangleContainsScreenPoint(pauseRect, screen, null)) return;

            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
            world.z = 0f;
            if (TryTapChai(world)) return;
            Roach best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < roaches.Length; i++)
            {
                Roach r = roaches[i];
                if (!r.Tappable) continue;
                Vector3 d = r.transform.position - world;
                d.z = 0f;
                float sq = d.sqrMagnitude;
                float reach = r.radius + 0.4f;
                if (sq < reach * reach && sq < bestD) { bestD = sq; best = r; }
            }
            if (best == null) return;

            Vector3 rp = best.transform.position;
            best.hp--;
            if (best.hp > 0)
            {
                best.Hit();
                sfx.Clink();
                shake.Shake(0.06f, 0.08f);
                EmitStars(rp, 3, 2.5f, new Color(0.8f, 0.9f, 1f));
                return;
            }

            best.Kill();
            bugsFixed++;
            combo = elapsed - lastKillTime <= ComboWindow ? combo + 1 : 1;
            lastKillTime = elapsed;
            int mult = Mathf.Min(combo, 10);
            if (mult > bestCombo) bestCombo = mult;
            score += 10 * mult;
            scoreText.text = "SCORE " + score;

            EmitStars(rp, 9, 4.5f, new Color(1f, 0.9f, 0.2f));
            ShowFloat(rp + new Vector3(0f, 0.3f, 0f), "POOF!", new Color(1f, 0.92f, 0.25f), 70);
            ShowFloat(rp + new Vector3(0.5f, 0.9f, 0f), pointStrings[mult], Color.white, 56);
            sfx.Pop(1f + (mult - 1) * 0.09f);
            shake.Shake(0.08f + 0.012f * mult, 0.12f);

            if (mult >= 2)
            {
                comboText.text = comboStrings[mult];
                comboText.gameObject.SetActive(true);
                comboAnim = 1f;
            }
        }

        // ---------- FX ----------

        void EmitStars(Vector3 pos, int count, float speed, Color color)
        {
            for (int n = 0; n < count; n++)
            {
                int i = nextStar;
                nextStar = (nextStar + 1) % MaxStars;
                float ang = (n / (float)count + Random.value * 0.15f) * 6.283f;
                starVel[i] = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * speed * Random.Range(0.6f, 1.1f);
                starLife[i] = 0.45f;
                stars[i].position = pos;
                stars[i].localScale = Vector3.one * Random.Range(0.35f, 0.6f);
                stars[i].rotation = Quaternion.Euler(0f, 0f, Random.value * 360f);
                starRenderers[i].color = color;
                starRenderers[i].gameObject.SetActive(true);
            }
        }

        void ShowFloat(Vector3 world, string text, Color color, int size)
        {
            int i = nextFloat;
            nextFloat = (nextFloat + 1) % MaxFloats;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, cam.WorldToScreenPoint(world), null, out local);
            floatStart[i] = local;
            floats[i].anchoredPosition = local;
            floats[i].localScale = Vector3.one * 0.5f;
            floatTexts[i].text = text;
            floatTexts[i].color = color;
            floatTexts[i].fontSize = size;
            floatLife[i] = 0.8f;
            floatTexts[i].gameObject.SetActive(true);
        }

        void Say(string line)
        {
            bubbleText.text = line;
            bubble.gameObject.SetActive(true);
            bubbleTimer = 2.2f;
            bubble.localScale = Vector3.one * 0.2f;
            bubbleNext = Random.Range(6f, 9f);
        }

        void TickFx(float dt)
        {
            for (int i = 0; i < MaxStars; i++)
            {
                if (starLife[i] <= 0f) continue;
                starLife[i] -= dt;
                if (starLife[i] <= 0f) { starRenderers[i].gameObject.SetActive(false); continue; }
                starVel[i] *= 1f - 5f * dt;
                stars[i].position += starVel[i] * dt;
                stars[i].Rotate(0f, 0f, 400f * dt);
                Color c = starRenderers[i].color;
                c.a = Mathf.Clamp01(starLife[i] / 0.25f);
                starRenderers[i].color = c;
            }

            for (int i = 0; i < MaxFloats; i++)
            {
                if (floatLife[i] <= 0f) continue;
                floatLife[i] -= dt;
                if (floatLife[i] <= 0f) { floatTexts[i].gameObject.SetActive(false); continue; }
                float t = 1f - floatLife[i] / 0.8f;
                floats[i].anchoredPosition = floatStart[i] + new Vector2(0f, 160f * t);
                float s = t < 0.15f ? Mathf.Lerp(0.5f, 1.2f, t / 0.15f) : Mathf.Lerp(1.2f, 1f, (t - 0.15f) / 0.85f);
                floats[i].localScale = new Vector3(s, s, 1f);
                Color c = floatTexts[i].color;
                c.a = t > 0.6f ? 1f - (t - 0.6f) / 0.4f : 1f;
                floatTexts[i].color = c;
            }

            if (comboAnim > 0f)
            {
                comboAnim -= dt * 4f;
                float s = 1f + Mathf.Max(0f, comboAnim) * 0.6f;
                comboText.rectTransform.localScale = new Vector3(s, s, 1f);
            }

            if (state != State.Playing && state != State.Over) return;
            if (bubbleTimer > 0f)
            {
                bubbleTimer -= dt;
                float s = Mathf.Min(1f, bubble.localScale.x + dt * 8f);
                bubbleScale.x = s; bubbleScale.y = s;
                bubble.localScale = bubbleScale;
                if (bubbleTimer <= 0f && state == State.Playing) bubble.gameObject.SetActive(false);
            }
            else if (state == State.Playing)
            {
                bubbleNext -= dt;
                if (bubbleNext <= 0f) Say(Lines[Random.Range(0, Lines.Length)]);
            }
        }
    }
}
