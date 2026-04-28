using System.Numerics;
using SadRogue.Primitives;
using Tarpg.Entities;
using SadEntity = SadConsole.Entities.Entity;
using SadEntityManager = SadConsole.Entities.EntityManager;

namespace Tarpg.UI.Effects;

// Owns transient visual feedback for combat hits and deaths:
//   - per-entity flash: brief tint to FlashColor when an entity takes damage
//   - damage numbers: per-digit SadEntities that drift upward and despawn
//   - kill burst: radial particle spray when an enemy dies
//   - hit-stop: global movement-pause timer set on every successful hit
//
// Triggered via Entity.Damaged / Entity.Died events that GameScreen wires up.
// Renders by adding/removing SadEntities to the supplied EntityManager.
//
// Tick(deltaSec, fontSize) is called once per frame from GameScreen.Update.
// The fontSize comes from GameScreen and is needed to convert tile-space
// drift into pixel positions (entities use UsePixelPositioning).
public sealed class HitFeedback
{
    private const float FlashSec = 0.12f;

    private const float DamageNumberLifeSec = 0.6f;
    private const float DamageNumberDriftTilesPerSec = 1.5f;
    private const float DamageNumberJitterTiles = 0.3f;
    private const int DamageNumberZIndex = 200;

    private const float KillBurstLifeSec = 0.4f;
    private const float KillBurstSpeedTilesPerSec = 4.0f;
    private const int KillBurstParticles = 7;
    private const int KillBurstZIndex = 200;

    private const float HitStopSec = 0.08f;

    private static readonly Color FlashColor = new(255, 80, 80);

    private readonly SadEntityManager _entityManager;
    private readonly Dictionary<Entity, float> _flashTimers = new();
    private readonly List<DamageNumber> _damageNumbers = new();
    private readonly List<Particle> _particles = new();

    public float HitStopRemaining { get; private set; }

    public HitFeedback(SadEntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public bool IsFlashing(Entity entity) => _flashTimers.ContainsKey(entity);

    public Color FlashTint => FlashColor;

    public void OnDamaged(Entity entity, int amount, Color numberColor)
    {
        _flashTimers[entity] = FlashSec;
        SpawnDamageNumber(entity.ContinuousPosition, amount, numberColor);
        if (HitStopRemaining < HitStopSec) HitStopRemaining = HitStopSec;
    }

    public void OnDied(Entity entity)
    {
        SpawnKillBurst(entity.ContinuousPosition, entity.Color);
    }

    public void Tick(float deltaSec, Point fontSize)
    {
        TickFlashes(deltaSec);
        TickDamageNumbers(deltaSec, fontSize);
        TickParticles(deltaSec, fontSize);
        if (HitStopRemaining > 0f)
            HitStopRemaining = MathF.Max(0f, HitStopRemaining - deltaSec);
    }

    // Tear down all transient visuals — used on floor regen so stale damage
    // numbers and particles from the old map don't render on the new one.
    public void Clear()
    {
        _flashTimers.Clear();
        foreach (var dn in _damageNumbers)
            foreach (var v in dn.Visuals) _entityManager.Remove(v);
        _damageNumbers.Clear();
        foreach (var p in _particles)
            _entityManager.Remove(p.Visual);
        _particles.Clear();
        HitStopRemaining = 0f;
    }

    private void TickFlashes(float deltaSec)
    {
        if (_flashTimers.Count == 0) return;
        // Snapshot keys since we mutate during iteration.
        var keys = new List<Entity>(_flashTimers.Keys);
        foreach (var key in keys)
        {
            var t = _flashTimers[key] - deltaSec;
            if (t <= 0f) _flashTimers.Remove(key);
            else _flashTimers[key] = t;
        }
    }

    private void TickDamageNumbers(float deltaSec, Point fontSize)
    {
        for (var i = _damageNumbers.Count - 1; i >= 0; i--)
        {
            var dn = _damageNumbers[i];
            dn.Age += deltaSec;
            if (dn.Age >= DamageNumberLifeSec)
            {
                foreach (var v in dn.Visuals) _entityManager.Remove(v);
                _damageNumbers.RemoveAt(i);
                continue;
            }

            dn.Position = new Vector2(
                dn.Position.X,
                dn.Position.Y - DamageNumberDriftTilesPerSec * deltaSec);

            for (var j = 0; j < dn.Visuals.Count; j++)
            {
                var pxX = (dn.Position.X + j - 0.5f) * fontSize.X;
                var pxY = (dn.Position.Y - 0.5f) * fontSize.Y;
                dn.Visuals[j].Position = new Point((int)MathF.Round(pxX), (int)MathF.Round(pxY));
            }
        }
    }

    private void TickParticles(float deltaSec, Point fontSize)
    {
        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Age += deltaSec;
            if (p.Age >= p.Lifetime)
            {
                _entityManager.Remove(p.Visual);
                _particles.RemoveAt(i);
                continue;
            }

            p.Position += p.Velocity * deltaSec;
            var pxX = (p.Position.X - 0.5f) * fontSize.X;
            var pxY = (p.Position.Y - 0.5f) * fontSize.Y;
            p.Visual.Position = new Point((int)MathF.Round(pxX), (int)MathF.Round(pxY));
        }
    }

    private void SpawnDamageNumber(Vector2 entityPos, int amount, Color color)
    {
        var text = amount.ToString();
        var jitterX = (Random.Shared.NextSingle() * 2f - 1f) * DamageNumberJitterTiles;
        var startPos = new Vector2(entityPos.X + jitterX, entityPos.Y - 0.6f);

        var visuals = new List<SadEntity>(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var visual = new SadEntity(color, Color.Transparent, text[i], zIndex: DamageNumberZIndex)
            {
                UsePixelPositioning = true,
            };
            _entityManager.Add(visual);
            visuals.Add(visual);
        }

        _damageNumbers.Add(new DamageNumber
        {
            Position = startPos,
            Visuals = visuals,
            Age = 0f,
        });
    }

    private void SpawnKillBurst(Vector2 center, Color baseColor)
    {
        ReadOnlySpan<char> glyphs = stackalloc[] { '*', '\'', ',', '.', '`' };
        for (var i = 0; i < KillBurstParticles; i++)
        {
            // Even angular distribution + small jitter so the spray doesn't
            // look mechanical.
            var baseAngle = i / (float)KillBurstParticles * MathF.PI * 2f;
            var angle = baseAngle + (Random.Shared.NextSingle() - 0.5f) * 0.4f;
            var velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle))
                           * KillBurstSpeedTilesPerSec;
            var glyph = glyphs[Random.Shared.Next(glyphs.Length)];

            var visual = new SadEntity(baseColor, Color.Transparent, glyph, zIndex: KillBurstZIndex)
            {
                UsePixelPositioning = true,
            };
            _entityManager.Add(visual);

            _particles.Add(new Particle
            {
                Visual = visual,
                Position = center,
                Velocity = velocity,
                Age = 0f,
                Lifetime = KillBurstLifeSec,
            });
        }
    }

    private sealed class DamageNumber
    {
        public required Vector2 Position { get; set; }
        public required List<SadEntity> Visuals { get; init; }
        public float Age;
    }

    private sealed class Particle
    {
        public required SadEntity Visual { get; init; }
        public required Vector2 Position { get; set; }
        public required Vector2 Velocity { get; init; }
        public float Age;
        public required float Lifetime { get; init; }
    }
}
