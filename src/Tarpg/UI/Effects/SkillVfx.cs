using SadConsole;
using SadRogue.Primitives;
using Tarpg.Core;
using Tarpg.Skills;

namespace Tarpg.UI.Effects;

// UI-side implementation of ISkillVfx. Owns three independent effect
// channels:
//   - Area highlights: tinted bg overlays on world cells, fading out.
//   - Screen shake:    additive pixel jitter on top of the camera offset.
//   - Screen flash:    fullscreen translucent fill on a dedicated overlay
//                      console child between the world and HUD layers.
//
// GameScreen drives this with a simple lifecycle: Tick(deltaSec) every
// frame to advance timers, Render() after world repaint to apply current
// frame's tints / overlays, GetShakeOffsetPx() during camera update,
// Clear() on floor regen.
public sealed class SkillVfx : ISkillVfx
{
    private const float HighlightMaxTint = 0.7f;
    private const byte FlashPeakAlpha = 160;

    private readonly ICellSurface _worldSurface;
    private readonly ICellSurface _flashSurface;

    private readonly List<Highlight> _highlights = new();

    private float _shakeIntensityPx;
    private float _shakeDurationSec;
    private float _shakeRemainingSec;

    private Color _flashColor;
    private float _flashDurationSec;
    private float _flashRemainingSec;
    private bool _flashSurfacePopulated;

    public SkillVfx(ICellSurface worldSurface, ICellSurface flashSurface)
    {
        _worldSurface = worldSurface;
        _flashSurface = flashSurface;
    }

    public void PlayAreaHighlight(IEnumerable<Position> tiles, Color color, float lifeSec)
    {
        foreach (var tile in tiles)
            _highlights.Add(new Highlight
            {
                Tile = tile,
                Color = color,
                Lifetime = lifeSec,
            });
    }

    public void PlayScreenShake(float intensityPx, float durationSec)
    {
        // Stack-by-max so a small shake mid-decay can't shorten a louder
        // one in flight, and a louder shake takes priority cleanly.
        _shakeIntensityPx = MathF.Max(_shakeIntensityPx, intensityPx);
        _shakeDurationSec = MathF.Max(_shakeDurationSec, durationSec);
        _shakeRemainingSec = MathF.Max(_shakeRemainingSec, durationSec);
    }

    public void PlayScreenFlash(Color color, float durationSec)
    {
        // Flash overwrites: a new flash mid-decay replaces the old one
        // (matches how shouts in flight cancel each other in most ARPGs).
        _flashColor = color;
        _flashDurationSec = durationSec;
        _flashRemainingSec = durationSec;
    }

    public void Tick(float deltaSec)
    {
        for (var i = _highlights.Count - 1; i >= 0; i--)
        {
            var h = _highlights[i];
            h.Age += deltaSec;
            if (h.Age >= h.Lifetime) _highlights.RemoveAt(i);
        }

        if (_shakeRemainingSec > 0f)
        {
            _shakeRemainingSec = MathF.Max(0f, _shakeRemainingSec - deltaSec);
            if (_shakeRemainingSec <= 0f)
            {
                _shakeIntensityPx = 0f;
                _shakeDurationSec = 0f;
            }
        }

        if (_flashRemainingSec > 0f)
            _flashRemainingSec = MathF.Max(0f, _flashRemainingSec - deltaSec);
    }

    // Render is called *after* DrawMap each frame so highlight tints land
    // on top of the freshly painted world cells; otherwise DrawMap would
    // overwrite the tint the same frame.
    public void Render()
    {
        if (_highlights.Count > 0)
        {
            foreach (var h in _highlights)
            {
                if (!InBounds(_worldSurface, h.Tile)) continue;
                var t = 1f - (h.Age / h.Lifetime);
                var tintFactor = t * HighlightMaxTint;

                var x = h.Tile.X;
                var y = h.Tile.Y;
                var origBg = _worldSurface.GetBackground(x, y);
                var origFg = _worldSurface.GetForeground(x, y);
                var origGlyph = _worldSurface.GetGlyph(x, y);

                var tintedBg = LerpColor(origBg, h.Color, tintFactor);
                _worldSurface.SetGlyph(x, y, origGlyph, origFg, tintedBg);
            }
            _worldSurface.IsDirty = true;
        }

        if (_flashRemainingSec > 0f)
        {
            var t = _flashRemainingSec / _flashDurationSec;
            var alpha = (byte)(FlashPeakAlpha * t);
            var fill = new Color(_flashColor.R, _flashColor.G, _flashColor.B, alpha);
            for (var y = 0; y < _flashSurface.Height; y++)
            for (var x = 0; x < _flashSurface.Width; x++)
                _flashSurface.SetGlyph(x, y, ' ', Color.White, fill);
            _flashSurface.IsDirty = true;
            _flashSurfacePopulated = true;
        }
        else if (_flashSurfacePopulated)
        {
            // One-time clear so the lingering tinted cells from a finished
            // flash don't sit on the overlay forever.
            ClearFlashSurface();
            _flashSurfacePopulated = false;
        }
    }

    // Per-frame shake offset, evaluated freshly each call so Render +
    // UpdateCamera each get a different sample if both invoke it (which
    // they shouldn't — only UpdateCamera calls this).
    public Point GetShakeOffsetPx()
    {
        if (_shakeRemainingSec <= 0f || _shakeDurationSec <= 0f)
            return new Point(0, 0);
        var t = _shakeRemainingSec / _shakeDurationSec;
        var amplitude = _shakeIntensityPx * t;
        var dx = (Random.Shared.NextSingle() * 2f - 1f) * amplitude;
        var dy = (Random.Shared.NextSingle() * 2f - 1f) * amplitude;
        return new Point((int)MathF.Round(dx), (int)MathF.Round(dy));
    }

    public void Clear()
    {
        _highlights.Clear();
        _shakeRemainingSec = 0f;
        _shakeDurationSec = 0f;
        _shakeIntensityPx = 0f;
        _flashRemainingSec = 0f;
        if (_flashSurfacePopulated)
        {
            ClearFlashSurface();
            _flashSurfacePopulated = false;
        }
    }

    private void ClearFlashSurface()
    {
        for (var y = 0; y < _flashSurface.Height; y++)
        for (var x = 0; x < _flashSurface.Width; x++)
            _flashSurface.SetGlyph(x, y, ' ', Color.Transparent, Color.Transparent);
        _flashSurface.IsDirty = true;
    }

    private static bool InBounds(ICellSurface s, Position p) =>
        p.X >= 0 && p.X < s.Width && p.Y >= 0 && p.Y < s.Height;

    private static Color LerpColor(Color a, Color b, float t)
    {
        var clampT = Math.Clamp(t, 0f, 1f);
        return new Color(
            (byte)(a.R + (b.R - a.R) * clampT),
            (byte)(a.G + (b.G - a.G) * clampT),
            (byte)(a.B + (b.B - a.B) * clampT));
    }

    private sealed class Highlight
    {
        public required Position Tile { get; init; }
        public required Color Color { get; init; }
        public required float Lifetime { get; init; }
        public float Age;
    }
}
