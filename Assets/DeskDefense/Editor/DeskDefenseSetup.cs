using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeskDefense.EditorTools
{
    /// <summary>One-click generator for Desk Defense: placeholder art, scene, UI and wiring.</summary>
    public static class DeskDefenseSetup
    {
        const string Root = "Assets/DeskDefense";
        const string ArtDir = Root + "/Art";
        const string ScenePath = Root + "/Scenes/DeskDefense.unity";
        const string MatPath = Root + "/Materials/DD_SpriteUnlit.mat";
        const string BuildPath = "Builds/Android/DeskDefense.apk";
        const string WebGLPath = "Builds/WebGL/DeskDefense";

        // Sorting layers (low to high): table 0-4, desk items 10-14, chair 20-22, developer 23-29,
        // zone health bars 30-31, chai power-up 35-38, roaches 40+ (one order per pooled roach), POOF stars 70.
        const int LayerBars = 30;

        static Sprite square, circle, worldRound, uiRound, roachSprite, starSprite;
        static Material mat;
        static Font font;

        [MenuItem("Desk Defense/Setup Everything", priority = 0)]
        public static void SetupEverything()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Desk Defense] Stop play mode before running Setup Everything.");
                return;
            }
            GenerateArt();
            BuildScene();
            Debug.Log("[Desk Defense] Setup complete -> " + ScenePath);
        }

        [MenuItem("Desk Defense/Build Android APK", priority = 20)]
        public static void BuildAndroid()
        {
            var target = NamedBuildTarget.Android;
            string oldName = PlayerSettings.productName;
            string oldId = PlayerSettings.GetApplicationIdentifier(target);
            try
            {
                // Temporary identity so the APK does not replace Pumpkin Evolution on the device.
                PlayerSettings.productName = "Desk Defense";
                PlayerSettings.SetApplicationIdentifier(target, "com.tahir.deskdefense");
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
                Directory.CreateDirectory(Path.GetDirectoryName(BuildPath));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = BuildPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                };
                var report = BuildPipeline.BuildPlayer(options);
                Debug.Log("[Desk Defense] Build " + report.summary.result + " -> " + BuildPath);
            }
            finally
            {
                PlayerSettings.productName = oldName;
                PlayerSettings.SetApplicationIdentifier(target, oldId);
            }
        }

        /// <summary>
        /// Browser build for itch.io (playable from a LinkedIn link). Gzip + decompression fallback works on
        /// static hosts that don't send Content-Encoding headers. The editor stays on the WebGL platform afterwards;
        /// only the product name is restored.
        /// </summary>
        [MenuItem("Desk Defense/Build WebGL (itch.io)", priority = 21)]
        public static void BuildWebGL()
        {
            string oldName = PlayerSettings.productName;
            try
            {
                PlayerSettings.productName = "Desk Defense";
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = true;
                PlayerSettings.defaultWebScreenWidth = 540;
                PlayerSettings.defaultWebScreenHeight = 960;
                Directory.CreateDirectory(WebGLPath);
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = WebGLPath,
                    target = BuildTarget.WebGL,
                    targetGroup = BuildTargetGroup.WebGL,
                    options = BuildOptions.None
                };
                var report = BuildPipeline.BuildPlayer(options);
                Debug.Log("[Desk Defense] WebGL build " + report.summary.result + " -> " + WebGLPath);
            }
            finally
            {
                PlayerSettings.productName = oldName;
            }
        }

        // ---------- Art ----------

        static void GenerateArt()
        {
            Directory.CreateDirectory(ArtDir);
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Scenes");

            Save(ArtDir + "/Square.png", SolidTex(8));
            Save(ArtDir + "/Circle.png", CircleTex(128));
            Save(ArtDir + "/RoundWorld.png", RoundTex(128, 36f));
            Save(ArtDir + "/RoundUI.png", RoundTex(128, 36f));
            Save(ArtDir + "/Roach.png", RoachTex());
            Save(ArtDir + "/Star.png", StarTex(64));
            AssetDatabase.Refresh();

            Configure(ArtDir + "/Square.png", 8, Vector4.zero);
            Configure(ArtDir + "/Circle.png", 128, Vector4.zero);
            Configure(ArtDir + "/RoundWorld.png", 400, new Vector4(40, 40, 40, 40));
            Configure(ArtDir + "/RoundUI.png", 100, new Vector4(40, 40, 40, 40));
            Configure(ArtDir + "/Roach.png", 100, Vector4.zero);
            Configure(ArtDir + "/Star.png", 64, Vector4.zero);

            if (AssetDatabase.LoadAssetAtPath<Material>(MatPath) == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
                AssetDatabase.CreateAsset(new Material(shader), MatPath);
            }
            AssetDatabase.SaveAssets();
        }

        static void Save(string path, Texture2D tex)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static void Configure(string path, float ppu, Vector4 border)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = ppu;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteBorder = border;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            imp.SetTextureSettings(settings);
            imp.SaveAndReimport();
        }

        static Texture2D NewTex(int w, int h, out Color[] px)
        {
            px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, 0f);
            return new Texture2D(w, h, TextureFormat.RGBA32, false);
        }

        static Texture2D Finish(Texture2D tex, Color[] px)
        {
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static void Blend(Color[] px, int w, int h, int x, int y, Color c, float a)
        {
            if (x < 0 || y < 0 || x >= w || y >= h || a <= 0f) return;
            a = Mathf.Clamp01(a) * c.a;
            int i = y * w + x;
            Color d = px[i];
            float outA = a + d.a * (1f - a);
            if (outA <= 0f) return;
            Color r = (c * a + d * d.a * (1f - a)) / outA;
            r.a = outA;
            px[i] = r;
        }

        static void Ellipse(Color[] px, int w, int h, float cx, float cy, float rx, float ry, Color c)
        {
            for (int y = (int)(cy - ry - 2); y <= cy + ry + 2; y++)
                for (int x = (int)(cx - rx - 2); x <= cx + rx + 2; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    Blend(px, w, h, x, y, c, (1f - d) * Mathf.Min(rx, ry) + 0.5f);
                }
        }

        static void Segment(Color[] px, int w, int h, Vector2 a, Vector2 b, float thick, Color c)
        {
            int x0 = (int)(Mathf.Min(a.x, b.x) - thick - 2), x1 = (int)(Mathf.Max(a.x, b.x) + thick + 2);
            int y0 = (int)(Mathf.Min(a.y, b.y) - thick - 2), y1 = (int)(Mathf.Max(a.y, b.y) + thick + 2);
            Vector2 ab = b - a;
            float len2 = Mathf.Max(0.0001f, ab.sqrMagnitude);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    float dist = Vector2.Distance(p, a + ab * t);
                    Blend(px, w, h, x, y, c, thick * 0.5f - dist + 0.5f);
                }
        }

        static Texture2D SolidTex(int s)
        {
            var tex = NewTex(s, s, out Color[] px);
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            return Finish(tex, px);
        }

        static Texture2D CircleTex(int s)
        {
            var tex = NewTex(s, s, out Color[] px);
            Ellipse(px, s, s, s * 0.5f, s * 0.5f, s * 0.5f - 1f, s * 0.5f - 1f, Color.white);
            return Finish(tex, px);
        }

        static Texture2D RoundTex(int s, float r)
        {
            var tex = NewTex(s, s, out Color[] px);
            float half = s * 0.5f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - r), qy = Mathf.Abs(y + 0.5f - half) - (half - r);
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                    Blend(px, s, s, x, y, Color.white, 0.5f - outside);
                }
            return Finish(tex, px);
        }

        static Texture2D StarTex(int s)
        {
            var tex = NewTex(s, s, out Color[] px);
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float rad = (i % 2 == 0 ? 0.48f : 0.21f) * s;
                pts[i] = new Vector2(s * 0.5f + Mathf.Cos(ang) * rad, s * 0.5f + Mathf.Sin(ang) * rad);
            }
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                            if (InPoly(pts, new Vector2(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f))) hits++;
                    Blend(px, s, s, x, y, Color.white, hits / 16f);
                }
            return Finish(tex, px);
        }

        static bool InPoly(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        static Texture2D RoachTex()
        {
            const int w = 128, h = 160;
            var tex = NewTex(w, h, out Color[] px);
            Color leg = new Color(0.22f, 0.11f, 0.05f);
            // legs (behind body)
            float[] ys = { 95f, 75f, 55f };
            Vector2[] knees = { new Vector2(46f, 108f), new Vector2(50f, 80f), new Vector2(46f, 48f) };
            Vector2[] feet = { new Vector2(58f, 122f), new Vector2(60f, 70f), new Vector2(54f, 24f) };
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < 3; i++)
                {
                    Vector2 hip = new Vector2(64f + s * 18f, ys[i]);
                    Vector2 knee = new Vector2(64f + s * knees[i].x, knees[i].y);
                    Vector2 foot = new Vector2(64f + s * feet[i].x, feet[i].y);
                    Segment(px, w, h, hip, knee, 6f, leg);
                    Segment(px, w, h, knee, foot, 4f, leg);
                }
            // antennae
            for (int s = -1; s <= 1; s += 2)
            {
                Segment(px, w, h, new Vector2(64f + s * 8f, 142f), new Vector2(64f + s * 22f, 152f), 3f, leg);
                Segment(px, w, h, new Vector2(64f + s * 22f, 152f), new Vector2(64f + s * 46f, 157f), 3f, leg);
            }
            Ellipse(px, w, h, 64f, 70f, 32f, 50f, new Color(0.56f, 0.31f, 0.13f));
            Segment(px, w, h, new Vector2(64f, 24f), new Vector2(64f, 110f), 3f, new Color(0.33f, 0.17f, 0.07f));
            Ellipse(px, w, h, 51f, 84f, 7f, 20f, new Color(0.85f, 0.6f, 0.38f, 0.7f));
            Ellipse(px, w, h, 77f, 84f, 7f, 20f, new Color(0.85f, 0.6f, 0.38f, 0.7f));
            Ellipse(px, w, h, 64f, 118f, 25f, 14f, new Color(0.43f, 0.23f, 0.1f));
            Ellipse(px, w, h, 64f, 136f, 17f, 12f, new Color(0.32f, 0.16f, 0.07f));
            for (int s = -1; s <= 1; s += 2)
            {
                Ellipse(px, w, h, 64f + s * 8f, 139f, 6.5f, 6.5f, Color.white);
                Ellipse(px, w, h, 64f + s * 8.5f, 141f, 3f, 3f, Color.black);
            }
            return Finish(tex, px);
        }

        static void LoadArt()
        {
            square = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/Square.png");
            circle = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/Circle.png");
            worldRound = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/RoundWorld.png");
            uiRound = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/RoundUI.png");
            roachSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/Roach.png");
            starSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/Star.png");
            mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        // ---------- World helpers ----------

        static Transform Node(string name, Transform parent, Vector2 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            return go.transform;
        }

        static SpriteRenderer Box(string name, Transform parent, Vector2 pos, Vector2 size, Color c, int order, bool round = false)
        {
            var sr = Node(name, parent, pos).gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = round ? worldRound : square;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.color = c;
            sr.sortingOrder = order;
            sr.sharedMaterial = mat;
            return sr;
        }

        static SpriteRenderer Disc(string name, Transform parent, Vector2 pos, float diameter, Color c, int order)
        {
            var sr = Node(name, parent, pos).gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = circle;
            sr.transform.localScale = new Vector3(diameter, diameter, 1f);
            sr.color = c;
            sr.sortingOrder = order;
            sr.sharedMaterial = mat;
            return sr;
        }

        static void CodeLines(Transform parent, Vector2 center, Vector2 area, int rows, int order, int seed)
        {
            var rng = new System.Random(seed);
            Color[] cols = { new Color(0.55f, 0.85f, 1f), new Color(1f, 0.6f, 0.85f), new Color(0.6f, 1f, 0.6f), new Color(1f, 0.85f, 0.45f) };
            float rowH = area.y / rows;
            for (int r = 0; r < rows; r++)
            {
                float x = center.x - area.x * 0.5f + (float)rng.NextDouble() * 0.3f * (r % 3);
                float y = center.y + area.y * 0.5f - rowH * (r + 0.5f);
                int words = 1 + rng.Next(3);
                for (int k = 0; k < words; k++)
                {
                    float len = 0.2f + (float)rng.NextDouble() * 0.5f;
                    if (x + len > center.x + area.x * 0.5f) break;
                    Box("Code", parent, new Vector2(x + len * 0.5f, y), new Vector2(len, rowH * 0.45f), cols[rng.Next(cols.Length)], order);
                    x += len + 0.1f;
                }
            }
        }

        static Zone MakeZone(Transform parent, string name, Vector2 pos, Vector2 size, float barY, float barW, SpriteRenderer[] flash)
        {
            Transform t = parent;
            t.localPosition = pos;
            var z = t.gameObject.AddComponent<Zone>();
            z.zoneName = name;
            z.size = size;
            z.flashParts = flash;
            z.barWidth = barW;
            z.barHeight = 0.18f;
            Box("BarBG", t, new Vector2(0f, barY), new Vector2(barW + 0.12f, 0.3f), new Color(0.08f, 0.04f, 0.1f, 0.9f), LayerBars, true);
            z.barFill = Box("BarFill", t, new Vector2(0f, barY), new Vector2(barW, 0.18f), new Color(0.35f, 0.9f, 0.4f), LayerBars + 1, true);
            return z;
        }

        // ---------- UI helpers ----------

        static RectTransform UI(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static RectTransform Stretch(string name, Transform parent)
        {
            var rt = UI(name, parent, Vector2.zero, Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static Text Label(string name, Transform parent, string text, int size, Color c, Vector2 anchor, Vector2 pos, Vector2 box, bool outline = true)
        {
            var t = UI(name, parent, anchor, pos, box).gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.color = c;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (outline)
            {
                var o = t.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0.08f, 0.03f, 0.12f, 0.9f);
                o.effectDistance = new Vector2(3f, -3f);
            }
            return t;
        }

        static Image Img(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color c, bool round = true)
        {
            var img = UI(name, parent, anchor, pos, size).gameObject.AddComponent<Image>();
            img.sprite = round ? uiRound : square;
            img.type = Image.Type.Sliced;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        static Button Btn(string name, Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color c, int fontSize = 64)
        {
            var img = Img(name, parent, anchor, pos, size, c);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
            b.colors = colors;
            Label("Label", img.transform, label, fontSize, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return b;
        }

        static GameObject Panel(string name, Transform parent)
        {
            var rt = Stretch(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = square;
            img.color = new Color(0.05f, 0.02f, 0.1f, 0.78f);
            img.raycastTarget = true;
            return rt.gameObject;
        }

        // ---------- Scene ----------

        static void BuildScene()
        {
            LoadArt();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            camGO.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9.6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.05f, 0.16f);
            camGO.AddComponent<AudioListener>();
            var shake = camGO.AddComponent<CameraShake>();

            // Room (top-down): floor, big table, chair + developer at the bottom
            Transform world = new GameObject("World").transform;
            Disc("Rug", world, new Vector2(0f, -6.2f), 7.5f, new Color(0.2f, 0.1f, 0.3f), -9);

            Transform desk = Node("Table", world, Vector2.zero);
            Box("Edge", desk, new Vector2(0f, 1.7f), new Vector2(10.5f, 8.5f), new Color(0.42f, 0.25f, 0.13f), 0, true);
            Box("Top", desk, new Vector2(0f, 1.75f), new Vector2(10.1f, 8.1f), new Color(0.66f, 0.43f, 0.24f), 1, true);
            for (int i = 0; i < 6; i++)
                Box("Grain", desk, new Vector2(-1.5f + (i % 3) * 1.6f, -1.6f + i * 1.25f), new Vector2(3.2f + (i % 2) * 2f, 0.05f), new Color(0.58f, 0.37f, 0.2f), 2);
            Box("Mouse", desk, new Vector2(2.6f, -1.25f), new Vector2(0.45f, 0.65f), new Color(0.85f, 0.85f, 0.9f), 4, true);

            // Zones
            Transform lcd = Node("LCD", world, Vector2.zero);
            Box("Base", lcd, new Vector2(0f, -1.25f), new Vector2(1.3f, 0.35f), new Color(0.3f, 0.3f, 0.35f), 10, true);
            Box("Stand", lcd, new Vector2(0f, -1.0f), new Vector2(0.3f, 0.4f), new Color(0.3f, 0.3f, 0.35f), 10);
            Box("Frame", lcd, Vector2.zero, new Vector2(2.9f, 1.95f), new Color(0.13f, 0.13f, 0.17f), 11, true);
            var lcdScreen = Box("Screen", lcd, new Vector2(0f, 0.04f), new Vector2(2.6f, 1.6f), new Color(0.12f, 0.2f, 0.36f), 12);
            CodeLines(lcd, new Vector2(0f, 0.04f), new Vector2(2.3f, 1.4f), 7, 13, 1);
            var zLcd = MakeZone(lcd, "LCD", new Vector2(-2.5f, 3.3f), new Vector2(2.8f, 1.9f), 1.3f, 2.4f, new[] { lcdScreen });

            Transform laptop = Node("Laptop", world, Vector2.zero);
            Box("Lid", laptop, new Vector2(0f, 0.3f), new Vector2(2.6f, 1.7f), new Color(0.76f, 0.78f, 0.82f), 10, true);
            var lapScreen = Box("Screen", laptop, new Vector2(0f, 0.33f), new Vector2(2.3f, 1.4f), new Color(0.1f, 0.12f, 0.2f), 11);
            CodeLines(laptop, new Vector2(0f, 0.33f), new Vector2(2.05f, 1.2f), 6, 12, 2);
            Box("Deck", laptop, new Vector2(0f, -0.75f), new Vector2(2.8f, 0.55f), new Color(0.62f, 0.64f, 0.68f), 12, true);
            var zLap = MakeZone(laptop, "Laptop", new Vector2(2.6f, 1.2f), new Vector2(2.7f, 1.9f), 1.45f, 2.4f, new[] { lapScreen });

            Transform kbd = Node("Keyboard", world, Vector2.zero);
            var kbdBody = Box("Body", kbd, Vector2.zero, new Vector2(3.6f, 0.8f), new Color(0.2f, 0.2f, 0.25f), 13, true);
            for (int r = 0; r < 3; r++)
                for (int k = 0; k < 11; k++)
                    Box("Key", kbd, new Vector2(-1.5f + k * 0.3f, 0.22f - r * 0.22f), new Vector2(0.24f, 0.17f), new Color(0.78f, 0.8f, 0.86f), 14, true);
            var zKbd = MakeZone(kbd, "Keyboard", new Vector2(0f, -1.2f), new Vector2(3.4f, 0.8f), 0.62f, 2.6f, new[] { kbdBody });

            // Chair + developer seen from behind
            Transform chair = Node("Chair", world, Vector2.zero);
            Disc("WheelBase", chair, new Vector2(0f, -6.9f), 2.6f, new Color(0.15f, 0.15f, 0.18f), 20);
            Box("Seat", chair, new Vector2(0f, -5.6f), new Vector2(3.4f, 1.6f), new Color(0.22f, 0.22f, 0.27f), 21, true);
            Box("Back", chair, new Vector2(0f, -5.0f), new Vector2(3.0f, 2.4f), new Color(0.3f, 0.3f, 0.38f), 22, true);

            Transform dev = Node("Developer", world, Vector2.zero);
            Color hoodie = new Color(0.25f, 0.55f, 0.88f), skin = new Color(1f, 0.8f, 0.62f), hair = new Color(0.18f, 0.1f, 0.06f);
            Box("Body", dev, new Vector2(0f, -4.0f), new Vector2(3.0f, 2.1f), hoodie, 23, true);
            Box("Hood", dev, new Vector2(0f, -3.55f), new Vector2(1.7f, 0.7f), new Color(0.2f, 0.45f, 0.75f), 24, true);
            Transform head = Node("Head", dev, new Vector2(0f, -2.95f));
            Disc("EarL", head, new Vector2(-0.72f, -0.05f), 0.36f, skin, 25);
            Disc("EarR", head, new Vector2(0.72f, -0.05f), 0.36f, skin, 25);
            Disc("Hair", head, Vector2.zero, 1.5f, hair, 26);
            Disc("Shine", head, new Vector2(-0.25f, 0.3f), 0.35f, new Color(0.32f, 0.2f, 0.12f), 27);
            Box("GlassArmL", head, new Vector2(-0.62f, 0.12f), new Vector2(0.28f, 0.06f), new Color(0.1f, 0.06f, 0.12f), 27);
            Box("GlassArmR", head, new Vector2(0.62f, 0.12f), new Vector2(0.28f, 0.06f), new Color(0.1f, 0.06f, 0.12f), 27);

            // Hands on the keyboard, sleeves reaching back to the shoulders
            Transform handL = Node("HandL", dev, new Vector2(-0.85f, -1.45f));
            Transform handR = Node("HandR", dev, new Vector2(0.85f, -1.45f));
            foreach (Transform hnd in new[] { handL, handR })
            {
                float side = Mathf.Sign(hnd.localPosition.x);
                var sleeve = Box("Sleeve", hnd, new Vector2(side * 0.18f, -0.95f), new Vector2(0.5f, 1.8f), hoodie, 28, true);
                sleeve.transform.localRotation = Quaternion.Euler(0f, 0f, side * 12f);
                Disc("Hand", hnd, Vector2.zero, 0.5f, skin, 29);
            }

            // Chai power-up (top-down cup on a saucer); shown by DDGame
            Transform chaiT = Node("Chai", world, Vector2.zero);
            Disc("Saucer", chaiT, Vector2.zero, 1.15f, new Color(0.92f, 0.92f, 0.95f), 35);
            Box("Handle", chaiT, new Vector2(0.48f, 0f), new Vector2(0.35f, 0.18f), Color.white, 35, true);
            Disc("Cup", chaiT, Vector2.zero, 0.85f, Color.white, 36);
            Disc("Tea", chaiT, Vector2.zero, 0.66f, new Color(0.78f, 0.52f, 0.3f), 37);
            Disc("Foam", chaiT, new Vector2(-0.1f, 0.08f), 0.22f, new Color(0.92f, 0.78f, 0.6f), 38);
            chaiT.gameObject.SetActive(false);

            // Templates
            var roachGO = new GameObject("RoachTemplate");
            var roachSr = roachGO.AddComponent<SpriteRenderer>();
            roachSr.sprite = roachSprite;
            roachSr.sortingOrder = DeskDefense.DDGame.LayerRoaches;
            roachSr.sharedMaterial = mat;
            var roach = roachGO.AddComponent<Roach>();
            roach.body = roachSr;
            roachGO.SetActive(false);

            var starGO = new GameObject("StarTemplate");
            var starSr = starGO.AddComponent<SpriteRenderer>();
            starSr.sprite = starSprite;
            starSr.sortingOrder = DeskDefense.DDGame.LayerStars;
            starSr.sharedMaterial = mat;
            starGO.SetActive(false);

            // UI
            var canvasGO = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;   // portrait: fit width so narrow phones never crop the HUD
            canvasGO.AddComponent<GraphicRaycaster>();
            Transform ct = canvasGO.transform;

            var fxLayer = Stretch("FxLayer", ct);
            var floatT = Label("FloatText", fxLayer, "+10", 60, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 100f));
            floatT.gameObject.SetActive(false);
            var roachLabel = Label("RoachLabel", fxLayer, "NullReference", 30, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 50f));
            roachLabel.gameObject.SetActive(false);

            var bubble = UI("Bubble", fxLayer, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 140f));
            Img("TailBig", bubble, new Vector2(0.5f, 1f), new Vector2(-40f, 22f), new Vector2(44f, 44f), Color.white);
            Img("TailSmall", bubble, new Vector2(0.5f, 1f), new Vector2(-20f, 58f), new Vector2(24f, 24f), Color.white);
            Img("BG", bubble, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 140f), Color.white);
            var bubbleText = Label("Text", bubble, "It's not a bug, it's a feature!", 46, new Color(0.15f, 0.08f, 0.2f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 120f), false);

            var safe = Stretch("SafeArea", ct);
            safe.gameObject.AddComponent<SafeArea>();
            var hud = UI("HUD", safe, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(1080f, 320f));
            var pause = Btn("PauseButton", hud, "II", new Vector2(0f, 1f), new Vector2(95f, -80f), new Vector2(120f, 120f), new Color(0.35f, 0.25f, 0.5f), 56);
            var score = Label("Score", hud, "SCORE 0", 58, Color.white, new Vector2(0.5f, 1f), new Vector2(-60f, -80f), new Vector2(460f, 100f));
            Img("TimerBG", hud, new Vector2(1f, 1f), new Vector2(-160f, -80f), new Vector2(270f, 110f), new Color(0.35f, 0.25f, 0.5f));
            var timer = Label("Timer", hud, "9:00 AM", 54, Color.white, new Vector2(1f, 1f), new Vector2(-160f, -80f), new Vector2(270f, 110f));
            Label("CodeLabel", hud, "LINES OF CODE", 40, new Color(0.75f, 0.9f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -175f), new Vector2(600f, 60f));
            var barBG = Img("CodeBarBG", hud, new Vector2(0.5f, 1f), new Vector2(0f, -245f), new Vector2(940f, 70f), new Color(0.08f, 0.04f, 0.12f, 0.95f));
            var fill = Img("CodeFill", barBG.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 50f), new Color(0.3f, 0.9f, 0.45f), false);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            var pct = Label("Percent", barBG.transform, "0%", 42, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 60f));
            var comboT = Label("Combo", safe, "x2 COMBO!", 110, new Color(1f, 0.85f, 0.2f), new Vector2(0.5f, 0.5f), new Vector2(0f, 560f), new Vector2(900f, 160f));
            comboT.gameObject.SetActive(false);

            // Panels
            var start = Panel("StartPanel", ct);
            Label("Title", start.transform, "DESK DEFENSE", 120, new Color(1f, 0.85f, 0.25f), new Vector2(0.5f, 0.5f), new Vector2(0f, 420f), new Vector2(1000f, 160f));
            Label("Subtitle", start.transform, "Dev vs Cockroaches", 64, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 290f), new Vector2(1000f, 100f));
            Label("Help", start.transform, "Tap the bugs!\nProtect the monitor, laptop and keyboard.\nShip 100% of the code before 6 PM.", 44, new Color(0.85f, 0.85f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1000f, 240f));
            var play = Btn("PlayButton", start.transform, "PLAY", new Vector2(0.5f, 0.5f), new Vector2(0f, -200f), new Vector2(520f, 170f), new Color(0.25f, 0.75f, 0.4f), 80);

            var pausePanel = Panel("PausePanel", ct);
            Label("Title", pausePanel.transform, "PAUSED", 120, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(1000f, 160f));
            var resume = Btn("ResumeButton", pausePanel.transform, "RESUME", new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(520f, 160f), new Color(0.25f, 0.75f, 0.4f));
            var pauseRetry = Btn("RetryButton", pausePanel.transform, "RETRY", new Vector2(0.5f, 0.5f), new Vector2(0f, -160f), new Vector2(520f, 160f), new Color(0.85f, 0.45f, 0.25f));
            pausePanel.SetActive(false);

            var endPanel = Panel("EndPanel", ct);
            var endTitle = Label("Title", endPanel.transform, "DEPLOYED!", 120, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 470f), new Vector2(1000f, 160f));
            var endScore = Label("Score", endPanel.transform, "Score: 0", 70, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 320f), new Vector2(1000f, 100f));
            var endBest = Label("Best", endPanel.transform, "Best: 0", 56, new Color(1f, 0.85f, 0.3f), new Vector2(0.5f, 0.5f), new Vector2(0f, 225f), new Vector2(1000f, 90f));
            Img("StatsBG", endPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(760f, 260f), new Color(1f, 1f, 1f, 0.08f));
            var endStats = Label("Stats", endPanel.transform, "Bugs fixed: 0", 50, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(720f, 240f));
            endStats.lineSpacing = 1.15f;
            var endRetry = Btn("RetryButton", endPanel.transform, "RETRY", new Vector2(0.5f, 0.5f), new Vector2(0f, -250f), new Vector2(520f, 170f), new Color(0.25f, 0.75f, 0.4f), 80);
            Label("Credit", endPanel.transform, "Desk Defense - made by Tahir", 36, new Color(1f, 1f, 1f, 0.6f), new Vector2(0.5f, 0.5f), new Vector2(0f, -420f), new Vector2(1000f, 60f));
            endPanel.SetActive(false);

            // Story intro overlay (world shots are built at runtime by DDIntro)
            var introUI = Stretch("IntroUI", ct);
            var introLabels = Stretch("Labels", introUI);
            var introLabel = Label("LabelTemplate", introLabels, "708", 52, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 80f));
            introLabel.gameObject.SetActive(false);
            var fade = Stretch("Fade", introUI).gameObject.AddComponent<Image>();
            fade.sprite = square;
            fade.color = Color.black;
            fade.raycastTarget = false;
            var capBox = Img("CaptionBox", introUI, new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(1000f, 200f), new Color(0.06f, 0.03f, 0.1f, 0.85f));
            var capText = Label("Caption", capBox.transform, "", 50, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 180f));
            capText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var skip = Btn("SkipButton", introUI, "SKIP >>", new Vector2(1f, 1f), new Vector2(-150f, -110f), new Vector2(240f, 110f), new Color(0.35f, 0.25f, 0.5f), 46);
            introUI.gameObject.SetActive(false);

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

            // Game
            var gameGO = new GameObject("Game");
            var audio = gameGO.AddComponent<DDAudio>();
            var g = gameGO.AddComponent<DDGame>();
            g.cam = cam;
            g.shake = shake;
            g.sfx = audio;
            g.zones = new[] { zLcd, zLap, zKbd };
            g.roachTemplate = roach;
            g.starTemplate = starSr;
            g.devHead = head;
            g.handL = handL;
            g.handR = handR;
            g.canvasRect = (RectTransform)ct;
            g.codeFill = fill;
            g.percentText = pct;
            g.timerText = timer;
            g.scoreText = score;
            g.comboText = comboT;
            g.pauseButton = pause;
            g.floatTemplate = floatT;
            g.bubble = bubble;
            g.bubbleText = bubbleText;
            g.startPanel = start;
            g.pausePanel = pausePanel;
            g.endPanel = endPanel;
            g.playButton = play;
            g.resumeButton = resume;
            g.pauseRetryButton = pauseRetry;
            g.endRetryButton = endRetry;
            g.endTitle = endTitle;
            g.endScore = endScore;
            g.endBest = endBest;
            g.hud = hud.gameObject;
            g.endStats = endStats;
            g.roachLabelTemplate = roachLabel;
            g.chai = chaiT;

            var intro = gameGO.AddComponent<DDIntro>();
            intro.cam = cam;
            intro.square = square;
            intro.round = worldRound;
            intro.circle = circle;
            intro.mat = mat;
            intro.uiRoot = introUI.gameObject;
            intro.fade = fade;
            intro.caption = capText;
            intro.skipButton = skip;
            intro.labelTemplate = introLabel;
            intro.canvasRect = (RectTransform)ct;
            intro.sfx = audio;
            g.intro = intro;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }
    }
}
