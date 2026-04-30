using SadRogue.Primitives;
using Tarpg.Core;

namespace Tarpg.Skills;

// Visual-effects hooks a skill behavior can fire from inside Execute. Kept
// in Tarpg.Skills (not UI.Effects) so skill authors don't pull a UI
// dependency for what's logically "side effects of the ability." The
// concrete UI-side renderer (UI/Effects/SkillVfx.cs) implements this
// interface and is plugged into SkillContext by GameScreen.
//
// All three primitives are *fire-and-forget*: the call returns immediately
// and the renderer ticks the resulting state on its own. Stacking rules
// are renderer-defined (current impl: shakes take max intensity / duration;
// flashes overwrite; highlights accumulate per tile).
public interface ISkillVfx
{
    // Tint the listed tiles with `color` for `lifeSec`, alpha-fading toward
    // the original cell bg. Use for "area struck" footprints (Cleave's
    // 8-cell ring, Whirlwind's chebyshev-2 square, etc.).
    void PlayAreaHighlight(IEnumerable<Position> tiles, Color color, float lifeSec);

    // Jitter the world camera by up to `intensityPx` pixels per axis,
    // tapering linearly to zero over `durationSec`. Used for impact-feel
    // on heavy hits.
    void PlayScreenShake(float intensityPx, float durationSec);

    // Tint the entire viewport with `color` (semi-transparent) and fade to
    // clear over `durationSec`. Used for shouted abilities (War Cry's
    // green flash) and other "global state changed" moments.
    void PlayScreenFlash(Color color, float durationSec);
}
