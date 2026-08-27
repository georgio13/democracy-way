#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DemocracyWay.EditorTools
{
    // ══════════════════════════════════════════════════════════════════════════
    // Asset fallback — procedural KRZ-style placeholders.
    //
    // Design philosophy: the fallback IS the final aesthetic, not a debug
    // placeholder. Every scene ships playable with a cohesive moody visual
    // language even if zero hand-drawn art ever arrives. When real art does
    // arrive (same file name at the canonical path), it wins automatically.
    //
    //     Resolution order for every id:
    //       1. Assets/Art/Backgrounds/BG_X.png           (real art, if present)
    //       2. Assets/Art/_Generated/Backgrounds/BG_X.png (cached placeholder)
    //       3. Generate now → save → import → return.
    //
    // The generators are deterministic: hash(id) → everything. Same id
    // regenerates byte-identical PNGs, so version control diffs stay clean.
    //
    // Palette is Kentucky-Route-Zero-adjacent: deep cold navy, near-black
    // silhouettes, single cream accent for lights and rim-lighting.
    // ══════════════════════════════════════════════════════════════════════════

    public static partial class DemocracyWayInit
    {
        // ── Folders ──
        private const string RealBgFolder        = "Assets/Art/Backgrounds";
        private const string RealCharFolder      = "Assets/Art/Characters";
        private const string RealAudioMusic      = "Assets/Audio/Music";
        private const string RealAudioDialogues  = "Assets/Audio/Dialogues";
        private const string RealAudioSfx        = "Assets/Audio/SFX";
        private const string GeneratedBgFolder   = "Assets/Art/_Generated/Backgrounds";
        private const string GeneratedCharFolder = "Assets/Art/_Generated/Characters";
        private const string GeneratedAudioFolder = "Assets/Audio/_Generated";

        // ── KRZ palette ──
        private static readonly Color KrzNavy     = new Color32(0x0A, 0x14, 0x20, 0xFF); // deep sky
        private static readonly Color KrzMidnight = new Color32(0x14, 0x26, 0x3D, 0xFF); // mid gradient
        private static readonly Color KrzBlack    = new Color32(0x03, 0x09, 0x12, 0xFF); // silhouettes
        private static readonly Color KrzCream    = new Color32(0xE8, 0xD8, 0xA8, 0xFF); // lights / rim
        private static readonly Color KrzTeal     = new Color32(0x2A, 0x43, 0x58, 0xFF); // horizon band
        private static readonly Color KrzWarm     = new Color32(0xC9, 0x8A, 0x4B, 0xFF); // lit windows

        private const int BgW = 1920, BgH = 1080;
        private const int ChW = 512,  ChH = 1024;

        // ═══════════════════════ Public entry points ═══════════════════════

        /// <summary>Returns the Sprite for <paramref name="bgId"/>. Falls back to a
        /// procedural KRZ-style placeholder (cached under _Generated) if no real
        /// art exists at <c>Assets/Art/Backgrounds/{id}.png</c>.</summary>
        private static Sprite LoadBgOrFallback(string bgId)
        {
            if (string.IsNullOrEmpty(bgId)) return null;

            string realPath = $"{RealBgFolder}/{bgId}.png";
            var real = AssetDatabase.LoadAssetAtPath<Sprite>(realPath);
            if (real != null) return real;

            string genPath = $"{GeneratedBgFolder}/{bgId}.png";
            var cached = AssetDatabase.LoadAssetAtPath<Sprite>(genPath);
            if (cached != null) return cached;

            GenerateBackgroundPng(bgId, genPath);
            return LoadFreshSprite(genPath);
        }

        /// <summary>Char sprite with procedural silhouette fallback.</summary>
        private static Sprite LoadCharOrFallback(string charId)
        {
            if (string.IsNullOrEmpty(charId)) return null;

            string realPath = $"{RealCharFolder}/{charId}.png";
            var real = AssetDatabase.LoadAssetAtPath<Sprite>(realPath);
            if (real != null) return real;

            string genPath = $"{GeneratedCharFolder}/{charId}.png";
            var cached = AssetDatabase.LoadAssetAtPath<Sprite>(genPath);
            if (cached != null) return cached;

            GenerateCharacterPng(charId, genPath);
            return LoadFreshSprite(genPath);
        }

        /// <summary>
        /// Loads the Sprite for a PNG that was just written.
        ///
        /// Right after generation the asset can still be registered as a plain
        /// texture, in which case asking for the Sprite sub-asset comes back
        /// null. A forced synchronous reimport settles it. A null return here
        /// is worth shouting about: it silently becomes an empty image slot in
        /// a database, which is much harder to diagnose later.
        /// </summary>
        private static Sprite LoadFreshSprite(string assetPath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                AssetDatabase.ImportAsset(assetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            }

            if (sprite == null)
                Debug.LogWarning($"[Init/Fallback] Generated {assetPath} but could not load it as a Sprite.");

            return sprite;
        }

        /// <summary>AudioClip resolver that falls back to a silent 1-second WAV,
        /// so a caller never has to null-check a clip that content references by
        /// id. Currently unused by Init — kept because voiced dialogue is the
        /// obvious next thing to wire in.</summary>
        private static AudioClip LoadAudioOrFallback(string audioId)
        {
            if (string.IsNullOrEmpty(audioId)) return null;

            var real = ResolveAudioClip(audioId);
            if (real != null) return real;

            string genPath = $"{GeneratedAudioFolder}/{audioId}.wav";
            var cached = AssetDatabase.LoadAssetAtPath<AudioClip>(genPath);
            if (cached != null) return cached;

            GenerateSilentWav(genPath, durationSeconds: 1f);
            Debug.Log($"[Init/Fallback] Generated silent placeholder: {genPath}");
            return AssetDatabase.LoadAssetAtPath<AudioClip>(genPath);
        }

        /// <summary>Looks for <c>{id}.wav</c> / <c>{id}.ogg</c> under each of the
        /// real audio folders. Returns null when nothing matches.</summary>
        private static AudioClip ResolveAudioClip(string audioId)
        {
            string[] folders = { RealAudioMusic, RealAudioDialogues, RealAudioSfx };
            string[] extensions = { ".wav", ".ogg", ".mp3" };

            foreach (var folder in folders)
            {
                foreach (var ext in extensions)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{folder}/{audioId}{ext}");
                    if (clip != null) return clip;
                }
            }
            return null;
        }

        // ═══════════════════════ Background generator ═══════════════════════

        private static void GenerateBackgroundPng(string bgId, string assetPath)
        {
            EnsureFolder(GeneratedBgFolder);
            var tex = new Texture2D(BgW, BgH, TextureFormat.RGBA32, mipChain: false, linear: false);
            var px = new Color[BgW * BgH];
            var rng = new DeterministicRng(bgId);

            // ── 1. Vertical gradient: navy (top) → near-black (bottom) ──
            for (int y = 0; y < BgH; y++)
            {
                float t = 1f - (y / (float)(BgH - 1));          // 0 bottom → 1 top
                float eased = Mathf.Pow(t, 1.2f);               // slightly compress sky
                var col = Color.Lerp(KrzBlack, KrzMidnight, eased);
                // soft horizon band around 60% up
                float hband = 1f - Mathf.Abs((t - 0.62f) * 6f);
                hband = Mathf.Clamp01(hband);
                col = Color.Lerp(col, KrzTeal, hband * 0.28f);
                int row = y * BgW;
                for (int x = 0; x < BgW; x++) px[row + x] = col;
            }

            // ── 2. Scene-flavour: pick silhouette style from id keywords ──
            BgFlavour flavour = ClassifyBgFlavour(bgId);
            DrawSilhouettes(px, flavour, rng);

            // ── 3. Sparse cream stars/lights in upper third ──
            int stars = rng.Range(18, 48);
            for (int i = 0; i < stars; i++)
            {
                int sx = rng.Range(0, BgW);
                int sy = rng.Range((int)(BgH * 0.55f), BgH - 4);
                float brightness = rng.Range01() * 0.6f + 0.3f;
                PlotSoftDot(px, sx, sy, 1, KrzCream, brightness);
            }

            // ── 4. Warm window lights for interior flavours ──
            if (flavour == BgFlavour.Interior || flavour == BgFlavour.Civic)
            {
                int lights = rng.Range(2, 6);
                for (int i = 0; i < lights; i++)
                {
                    int lx = rng.Range(BgW / 10, BgW - BgW / 10);
                    int ly = rng.Range((int)(BgH * 0.30f), (int)(BgH * 0.55f));
                    PlotSoftDot(px, lx, ly, 4, KrzWarm, 0.95f);
                }
            }

            tex.SetPixels(px);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            WritePng(tex, assetPath);
            ImportAsSprite(assetPath);
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private enum BgFlavour { Landscape, Interior, Civic, Harbour }

        private static BgFlavour ClassifyBgFlavour(string bgId)
        {
            string lower = bgId.ToLowerInvariant();
            if (lower.Contains("apartment") || lower.Contains("office") || lower.Contains("desk") ||
                lower.Contains("tv") || lower.Contains("workshop") || lower.Contains("community"))
                return BgFlavour.Interior;
            if (lower.Contains("pnyx") || lower.Contains("agora") || lower.Contains("heliaia") ||
                lower.Contains("bouleuterion") || lower.Contains("polling") || lower.Contains("plaza"))
                return BgFlavour.Civic;
            if (lower.Contains("shipyard") || lower.Contains("piraeus") || lower.Contains("harbour"))
                return BgFlavour.Harbour;
            return BgFlavour.Landscape;
        }

        private static void DrawSilhouettes(Color[] px, BgFlavour flavour, DeterministicRng rng)
        {
            // Draw a ground silhouette band in KrzBlack, then add distinct shapes
            // riding on top. All heights are measured from the bottom of the image.
            int groundTop = (int)(BgH * 0.28f);
            for (int y = 0; y < groundTop; y++)
            {
                int row = y * BgW;
                for (int x = 0; x < BgW; x++) px[row + x] = KrzBlack;
            }

            int shapes = rng.Range(4, 9);
            for (int i = 0; i < shapes; i++)
            {
                int cx = rng.Range(0, BgW);
                int baseY = groundTop + rng.Range(-8, 12);
                switch (flavour)
                {
                    case BgFlavour.Interior:
                        DrawRect(px, cx - rng.Range(40, 160), baseY,
                                     cx + rng.Range(40, 160), baseY + rng.Range(80, 280), KrzBlack);
                        break;
                    case BgFlavour.Civic:
                        DrawColumn(px, cx, baseY, rng.Range(18, 38), rng.Range(180, 520), rng);
                        break;
                    case BgFlavour.Harbour:
                        DrawHull(px, cx, baseY, rng.Range(90, 260), rng.Range(40, 80));
                        DrawMast(px, cx + rng.Range(-40, 40), baseY + 40, rng.Range(260, 480));
                        break;
                    default: // Landscape
                        DrawHill(px, cx, baseY, rng.Range(220, 520), rng.Range(60, 180));
                        break;
                }
            }
        }

        // ── Shape primitives (all draw in KrzBlack, no anti-aliasing — KRZ is flat) ──

        private static void DrawRect(Color[] px, int x0, int y0, int x1, int y1, Color c)
        {
            if (x0 > x1) { int t = x0; x0 = x1; x1 = t; }
            if (y0 > y1) { int t = y0; y0 = y1; y1 = t; }
            x0 = Mathf.Clamp(x0, 0, BgW - 1); x1 = Mathf.Clamp(x1, 0, BgW - 1);
            y0 = Mathf.Clamp(y0, 0, BgH - 1); y1 = Mathf.Clamp(y1, 0, BgH - 1);
            for (int y = y0; y <= y1; y++)
            {
                int row = y * BgW;
                for (int x = x0; x <= x1; x++) px[row + x] = c;
            }
        }

        private static void DrawColumn(Color[] px, int cx, int baseY, int halfW, int height, DeterministicRng rng)
        {
            DrawRect(px, cx - halfW, baseY, cx + halfW, baseY + height, KrzBlack);
            // Capital — slightly wider block at the top
            int capH = Mathf.Min(18, height / 12);
            DrawRect(px, cx - halfW - 6, baseY + height - capH, cx + halfW + 6, baseY + height, KrzBlack);
        }

        private static void DrawHull(Color[] px, int cx, int baseY, int halfW, int height)
        {
            // Trapezoidal hull — wider on top than bottom.
            for (int y = 0; y < height; y++)
            {
                float t = y / (float)height;
                int hw = Mathf.RoundToInt(Mathf.Lerp(halfW * 0.4f, halfW, t));
                DrawRect(px, cx - hw, baseY + y, cx + hw, baseY + y, KrzBlack);
            }
        }

        private static void DrawMast(Color[] px, int cx, int baseY, int height)
        {
            DrawRect(px, cx - 2, baseY, cx + 2, baseY + height, KrzBlack);
            // crossbeam
            DrawRect(px, cx - height / 4, baseY + height - 30, cx + height / 4, baseY + height - 24, KrzBlack);
        }

        private static void DrawHill(Color[] px, int cx, int baseY, int halfW, int height)
        {
            // Elliptical cap — gives a soft natural horizon curve.
            for (int dy = 0; dy <= height; dy++)
            {
                float t = dy / (float)height;
                int hw = Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)) * halfW);
                DrawRect(px, cx - hw, baseY + dy, cx + hw, baseY + dy, KrzBlack);
            }
        }

        private static void PlotSoftDot(Color[] px, int cx, int cy, int radius, Color tint, float intensity)
        {
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = cx + dx, y = cy + dy;
                if (x < 0 || y < 0 || x >= BgW || y >= BgH) continue;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Max(0.5f, radius);
                if (d > 1f) continue;
                float a = (1f - d) * intensity;
                int idx = y * BgW + x;
                px[idx] = Color.Lerp(px[idx], tint, Mathf.Clamp01(a));
            }
        }

        // ═══════════════════════ Character generator ═══════════════════════

        private static void GenerateCharacterPng(string charId, string assetPath)
        {
            EnsureFolder(GeneratedCharFolder);
            var tex = new Texture2D(ChW, ChH, TextureFormat.RGBA32, mipChain: false, linear: false);
            var px = new Color[ChW * ChH];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);

            var rng = new DeterministicRng(charId);
            float heightScale = rng.Range01() * 0.15f + 0.88f;        // 0.88–1.03
            float bodyWidth   = rng.Range01() * 0.35f + 0.82f;        // 0.82–1.17
            bool rimFromLeft  = rng.Range01() < 0.5f;

            int footY = 40;
            int topY  = Mathf.RoundToInt((ChH - 60) * heightScale);
            int midX  = ChW / 2;
            int headR = Mathf.RoundToInt(48 * bodyWidth);
            int shoulderHalf = Mathf.RoundToInt(110 * bodyWidth);
            int waistHalf    = Mathf.RoundToInt(78  * bodyWidth);

            int headCY = topY - headR - 6;
            int neckY  = headCY - headR - 4;
            int shoulderY = neckY - 8;
            int hipY   = footY + (topY - footY) / 3;

            // Head — filled circle
            FillCircle(px, midX, headCY, headR, KrzBlack);
            // Neck
            DrawRectCh(px, midX - 14, neckY, midX + 14, shoulderY + 2, KrzBlack);
            // Body — tapered from shoulder to hip, then skirt/legs to feet
            for (int y = hipY; y <= shoulderY; y++)
            {
                float t = (y - hipY) / (float)Mathf.Max(1, shoulderY - hipY);
                int hw = Mathf.RoundToInt(Mathf.Lerp(waistHalf, shoulderHalf, t));
                DrawRectCh(px, midX - hw, y, midX + hw, y, KrzBlack);
            }
            // Legs / robe — straight column from feet to hip
            DrawRectCh(px, midX - waistHalf, footY, midX + waistHalf, hipY, KrzBlack);

            // Rim light — thin cream line on one side
            int rimX = rimFromLeft ? -1 : 1;
            for (int y = 1; y < ChH - 1; y++)
            {
                for (int x = 1; x < ChW - 1; x++)
                {
                    int idx = y * ChW + x;
                    if (px[idx].a < 0.5f) continue;
                    int nIdx = y * ChW + (x + rimX);
                    if (nIdx < 0 || nIdx >= px.Length) continue;
                    if (px[nIdx].a < 0.5f)
                    {
                        // this is a silhouette edge on the rim side → paint cream
                        px[idx] = KrzCream;
                    }
                }
            }

            tex.SetPixels(px);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            WritePng(tex, assetPath);
            ImportAsSprite(assetPath);
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void DrawRectCh(Color[] px, int x0, int y0, int x1, int y1, Color c)
        {
            if (x0 > x1) { int t = x0; x0 = x1; x1 = t; }
            if (y0 > y1) { int t = y0; y0 = y1; y1 = t; }
            x0 = Mathf.Clamp(x0, 0, ChW - 1); x1 = Mathf.Clamp(x1, 0, ChW - 1);
            y0 = Mathf.Clamp(y0, 0, ChH - 1); y1 = Mathf.Clamp(y1, 0, ChH - 1);
            for (int y = y0; y <= y1; y++)
            {
                int row = y * ChW;
                for (int x = x0; x <= x1; x++) px[row + x] = c;
            }
        }

        private static void FillCircle(Color[] px, int cx, int cy, int r, Color c)
        {
            int r2 = r * r;
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (dx * dx + dy * dy > r2) continue;
                int x = cx + dx, y = cy + dy;
                if (x < 0 || y < 0 || x >= ChW || y >= ChH) continue;
                px[y * ChW + x] = c;
            }
        }

        // ═══════════════════════ Audio generator (silent WAV) ═══════════════════════

        private static void GenerateSilentWav(string assetPath, float durationSeconds)
        {
            EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
            const int sampleRate = 44100;
            const short channels = 1;
            const short bitsPerSample = 16;
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * durationSeconds));
            int dataBytes = sampleCount * channels * (bitsPerSample / 8);

            using (var fs = new FileStream(assetPath, FileMode.Create, FileAccess.Write))
            using (var w  = new BinaryWriter(fs))
            {
                // RIFF header
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { 'W', 'A', 'V', 'E' });
                // fmt chunk
                w.Write(new[] { 'f', 'm', 't', ' ' });
                w.Write(16);                                 // PCM chunk size
                w.Write((short)1);                           // PCM
                w.Write(channels);
                w.Write(sampleRate);
                w.Write(sampleRate * channels * (bitsPerSample / 8)); // byte rate
                w.Write((short)(channels * (bitsPerSample / 8)));     // block align
                w.Write(bitsPerSample);
                // data chunk
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);
                // PCM payload — silence (already zeroed by FileStream, but write explicitly)
                for (int i = 0; i < sampleCount; i++) w.Write((short)0);
            }
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        // ═══════════════════════ IO helpers ═══════════════════════

        private static void WritePng(Texture2D tex, string assetPath)
        {
            EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void ImportAsSprite(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            // Single, explicitly: the project default is Multiple, and in
            // Multiple mode a PNG exposes no top-level Sprite at all — so
            // LoadAssetAtPath<Sprite> comes back null and every artwork slot
            // silently ends up empty.
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        // EnsureFolder(string) is defined in DemocracyWayInit.cs (partial class).

        // ═══════════════════════ Deterministic RNG ═══════════════════════
        // Seeded from id string — identical id produces identical output across
        // runs, keeping version-controlled generated PNGs stable.

        private struct DeterministicRng
        {
            private uint _state;
            public DeterministicRng(string seedKey)
            {
                unchecked
                {
                    uint h = 2166136261u;
                    foreach (char c in seedKey ?? string.Empty) h = (h ^ c) * 16777619u;
                    _state = h == 0 ? 1u : h;
                }
            }
            public uint Next()
            {
                // xorshift32
                uint x = _state;
                x ^= x << 13; x ^= x >> 17; x ^= x << 5;
                _state = x;
                return x;
            }
            public float Range01() => (Next() & 0x00FFFFFF) / (float)0x01000000;
            public int Range(int minInclusive, int maxExclusive)
                => minInclusive + (int)(Range01() * (maxExclusive - minInclusive));
        }
    }
}
#endif
