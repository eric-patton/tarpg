using System.Numerics;
using SadRogue.Primitives;
using SadEntity = SadConsole.Entities.Entity;
using SadEntityManager = SadConsole.Entities.EntityManager;

namespace Tarpg.UI.Effects;

// Brief "you clicked here" pulse: on left-button release, GameScreen calls
// Spawn() with the world tile the click landed on; we render a single
// bright glyph at that tile and fade alpha to zero over LifeSec, then
// despawn the entity. Pure feedback — no movement / combat side effects.
//
// Same SadEntity-on-EntityManager pattern as HitFeedback so the indicator
// pans with the world layer when the camera scrolls.
public sealed class ClickIndicator
{
    private const float LifeSec = 0.25f;
    private const char Glyph = '+';
    private const int ZIndex = 150;
    private static readonly Color StartColor = new(255, 255, 180);

    private readonly SadEntityManager _entityManager;
    private readonly List<Pulse> _pulses = new();

    public ClickIndicator(SadEntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    // tilePosition is in continuous tile-space (e.g. (12.5, 8.5) for the
    // visual center of cell (12, 8)). GameScreen passes the click cell + 0.5.
    public void Spawn(Vector2 tilePosition)
    {
        var visual = new SadEntity(StartColor, Color.Transparent, Glyph, zIndex: ZIndex)
        {
            UsePixelPositioning = true,
        };
        _entityManager.Add(visual);
        _pulses.Add(new Pulse
        {
            Visual = visual,
            Position = tilePosition,
            Age = 0f,
        });
    }

    public void Tick(float deltaSec, Point fontSize)
    {
        for (var i = _pulses.Count - 1; i >= 0; i--)
        {
            var p = _pulses[i];
            p.Age += deltaSec;
            if (p.Age >= LifeSec)
            {
                _entityManager.Remove(p.Visual);
                _pulses.RemoveAt(i);
                continue;
            }

            // Tile-space → surface-pixel top-left (matches the entity
            // top-left convention used by SyncVisual / HitFeedback).
            var pxX = (p.Position.X - 0.5f) * fontSize.X;
            var pxY = (p.Position.Y - 0.5f) * fontSize.Y;
            p.Visual.Position = new Point((int)MathF.Round(pxX), (int)MathF.Round(pxY));

            // Linear alpha fade. Avoids the visual pop a hard cut-off would
            // give at the end of life.
            var t = p.Age / LifeSec;
            var alpha = (byte)(255 * (1f - t));
            var faded = new Color(StartColor.R, StartColor.G, StartColor.B, alpha);
            if (p.Visual.AppearanceSingle is { } appearance &&
                appearance.Appearance.Foreground != faded)
            {
                appearance.Appearance.Foreground = faded;
                p.Visual.IsDirty = true;
            }
        }
    }

    // Tear down all live pulses. Used on floor regen so stale indicators
    // from the old map don't render on the new one.
    public void Clear()
    {
        foreach (var p in _pulses) _entityManager.Remove(p.Visual);
        _pulses.Clear();
    }

    private sealed class Pulse
    {
        public required SadEntity Visual { get; init; }
        public required Vector2 Position { get; init; }
        public float Age;
    }
}
