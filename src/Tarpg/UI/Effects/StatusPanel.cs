using SadConsole;
using SadRogue.Primitives;
using Tarpg.Core;
using Tarpg.Entities;
using Tarpg.Skills;

namespace Tarpg.UI.Effects;

// Bottom-of-viewport status panel. Renders into its own Console child so
// pixel-shifting on the world layer doesn't affect it. Layout (80 × 5):
//
//   ┌───┐                                                          ┌───┐
//   │HP │     [Q ] [W ] [E ] [R ]                                  │RES│
//   │ N │     [ X] [  ] [  ] [  ]                                  │ N │
//   │   │     [rdy] [-] [-] [-]                                    │   │
//   └───┘                                                          └───┘
//
// HP orb on the left, class-resource orb on the right (color picked from
// WalkerClassDefinition.Resource), four skill slots centered between.
// Number-in-the-middle on each orb shows the current value; vertical
// fill (rows from the bottom up) is the visual fraction. Slots show
// keybind / skill glyph / cooldown-or-status, dimmed when unbound.
public sealed class StatusPanel
{
    public const int PanelHeight = 5;

    private const int OrbWidth = 5;
    private const int OrbHeight = 5;

    private const int SlotWidth = 6;
    private const int SlotHeight = PanelHeight; // slots match the orbs vertically
    private const int SlotGap = 4;
    private const int SlotRowOffset = 0;

    // Consumable slot dimensions. Narrower than skill slots so they tuck
    // beside the orbs without crowding the skill row in the middle.
    private const int ConsumableWidth = 4;
    private const int ConsumableHeight = PanelHeight;

    // Within a slot, vertical positions for the keybind (top), glyph
    // (middle, with breathing-room rows above and below), and cooldown text
    // (bottom). Cooldown only renders when actually counting down.
    private const int SlotKeybindRow = 0;
    private const int SlotGlyphRow = 2;
    private const int SlotCooldownRow = 4;

    private static readonly Color HpFilled = new(180, 30, 30);
    private static readonly Color HpEmpty = new(50, 10, 10);

    private static readonly Color SlotBgReady = new(30, 30, 40);
    private static readonly Color SlotBgIdle = new(15, 15, 22);
    private static readonly Color SlotLabelReady = new(220, 220, 230);
    private static readonly Color SlotLabelIdle = new(110, 110, 120);
    private static readonly Color SlotCooldown = new(220, 200, 110);

    private readonly SadConsole.Console _console;

    public StatusPanel(SadConsole.Console console)
    {
        _console = console;
        if (_console.Surface.Height != PanelHeight)
            throw new ArgumentException(
                $"StatusPanel expected {PanelHeight}-row console, got {_console.Surface.Height}.",
                nameof(console));
    }

    public void Render(
        Player player,
        IReadOnlyList<SkillSlot> skillSlots,
        ConsumableSlot? hpPotion,
        ConsumableSlot? resourcePotion)
    {
        var surface = _console.Surface;
        var width = surface.Width;

        ClearSurface(surface);

        // HP orb on the left.
        var hpFraction = player.MaxHealth > 0
            ? (float)player.Health / player.MaxHealth
            : 0f;
        DrawOrb(surface, x: 0, y: 0,
            fraction: hpFraction,
            filled: HpFilled,
            empty: HpEmpty,
            label: player.Health.ToString());

        // HP potion immediately right of the HP orb.
        if (hpPotion is { } hp)
            DrawConsumableSlot(surface, x: OrbWidth, y: 0, hp, HpFilled);

        // Resource orb on the right. Color follows class.
        var resourceColor = ResourceColor(player.WalkerClass.Resource);
        var resourceEmpty = Dim(resourceColor, 0.25f);
        var resourceFraction = player.MaxResource > 0
            ? (float)player.Resource / player.MaxResource
            : 0f;
        DrawOrb(surface, x: width - OrbWidth, y: 0,
            fraction: resourceFraction,
            filled: resourceColor,
            empty: resourceEmpty,
            label: player.Resource.ToString());

        // Resource potion immediately left of the resource orb.
        if (resourcePotion is { } rp)
            DrawConsumableSlot(surface, x: width - OrbWidth - ConsumableWidth, y: 0, rp, resourceColor);

        // Skill slots centered between the consumable slots so adding /
        // removing potions doesn't drift the skill bar visually.
        var skillsAreaLeft = OrbWidth + ConsumableWidth;
        var skillsAreaRight = width - OrbWidth - ConsumableWidth;
        var skillsAreaWidth = skillsAreaRight - skillsAreaLeft;
        var slotsTotalWidth = skillSlots.Count * SlotWidth + (skillSlots.Count - 1) * SlotGap;
        var slotsStartX = skillsAreaLeft + (skillsAreaWidth - slotsTotalWidth) / 2;
        for (var i = 0; i < skillSlots.Count; i++)
        {
            var slotX = slotsStartX + i * (SlotWidth + SlotGap);
            DrawSlot(surface, slotX, SlotRowOffset, skillSlots[i], player);
        }

        surface.IsDirty = true;
    }

    // Tear down the panel — used on floor regen so any in-flight cooldown
    // text from the prior frame can't bleed into the first paint of the new
    // floor's panel state.
    public void Clear()
    {
        ClearSurface(_console.Surface);
        _console.Surface.IsDirty = true;
    }

    private static void ClearSurface(ICellSurface surface)
    {
        for (var y = 0; y < surface.Height; y++)
        for (var x = 0; x < surface.Width; x++)
            surface.SetGlyph(x, y, ' ', Color.White, Color.Black);
    }

    // Vertical-fill orb. Bottom row is always (semi-)filled; rows above are
    // filled progressively as `fraction` rises. The label sits on the
    // middle row, white text against whatever fill color that row ended up
    // with — at low HP that's the empty shade, which gives a useful
    // "darkening" cue at a glance.
    private static void DrawOrb(ICellSurface surface, int x, int y,
        float fraction, Color filled, Color empty, string label)
    {
        var clamped = Math.Clamp(fraction, 0f, 1f);
        var fillRows = (int)MathF.Round(clamped * OrbHeight);

        // Snap fillRows to at least 1 if HP > 0 so the orb still shows a
        // sliver of color even at very low HP. Pure-zero shows fully dim.
        if (fillRows == 0 && fraction > 0f) fillRows = 1;

        var labelRow = OrbHeight / 2;
        var labelStartCol = (OrbWidth - label.Length) / 2;

        for (var dy = 0; dy < OrbHeight; dy++)
        for (var dx = 0; dx < OrbWidth; dx++)
        {
            var rowFromBottom = OrbHeight - 1 - dy;
            var bg = rowFromBottom < fillRows ? filled : empty;

            var glyph = ' ';
            var fg = Color.White;
            if (dy == labelRow)
            {
                var labelIdx = dx - labelStartCol;
                if (labelIdx >= 0 && labelIdx < label.Length)
                    glyph = label[labelIdx];
            }

            surface.SetGlyph(x + dx, y + dy, glyph, fg, bg);
        }
    }

    private static void DrawSlot(ICellSurface surface, int x, int y,
        SkillSlot slot, Player player)
    {
        var def = slot.Skill;
        var hasSkill = def is not null;
        var ready = hasSkill
            && slot.CooldownRemaining <= 0f
            && player.Resource >= def!.Cost;

        var bg = hasSkill && ready ? SlotBgReady : SlotBgIdle;
        var labelFg = hasSkill && ready ? SlotLabelReady : SlotLabelIdle;

        // Wipe the slot to its background color first.
        for (var dy = 0; dy < SlotHeight; dy++)
        for (var dx = 0; dx < SlotWidth; dx++)
            surface.SetGlyph(x + dx, y + dy, ' ', Color.White, bg);

        // Keybind on the top row, glyph in the middle, both centered with
        // the same formula so they share a column. The glyph row is
        // SlotGlyphRow (= 2) to leave a clear empty row between keybind and
        // glyph — the "breathing room" the bar reads better with.
        DrawCentered(surface, x, y + SlotKeybindRow, SlotWidth,
            slot.Keybind, labelFg, bg);

        if (!hasSkill) return;

        var glyphFg = ready
            ? ResourceColor(def!.Resource)
            : Dim(ResourceColor(def!.Resource), 0.45f);
        DrawCentered(surface, x, y + SlotGlyphRow, SlotWidth,
            def!.Glyph.ToString(), glyphFg, bg);

        // Cooldown timer is the only bottom-row message left. Ready state and
        // low-resource state are both signaled by the slot's dim/bright bg
        // alone — explicit labels were just noise on top of the color cue.
        if (slot.CooldownRemaining > 0f)
            DrawCentered(surface, x, y + SlotCooldownRow, SlotWidth,
                $"{slot.CooldownRemaining:0.0}s", SlotCooldown, bg);
    }

    // Same shape as a skill slot but narrower; bottom row carries an `xN`
    // stack count when count > 0 instead of a cooldown timer. Empty (count
    // 0) state shows the dim slot bg + dim glyph as a "you don't have any"
    // cue without dropping the slot from view entirely.
    private static void DrawConsumableSlot(ICellSurface surface, int x, int y,
        ConsumableSlot slot, Color glyphTint)
    {
        var ready = slot.Count > 0;
        var bg = ready ? SlotBgReady : SlotBgIdle;
        var labelFg = ready ? SlotLabelReady : SlotLabelIdle;

        for (var dy = 0; dy < ConsumableHeight; dy++)
        for (var dx = 0; dx < ConsumableWidth; dx++)
            surface.SetGlyph(x + dx, y + dy, ' ', Color.White, bg);

        DrawCentered(surface, x, y + SlotKeybindRow, ConsumableWidth,
            slot.Keybind, labelFg, bg);

        var glyphFg = ready ? glyphTint : Dim(glyphTint, 0.45f);
        DrawCentered(surface, x, y + SlotGlyphRow, ConsumableWidth,
            slot.Glyph.ToString(), glyphFg, bg);

        if (slot.Count > 0)
            DrawCentered(surface, x, y + SlotCooldownRow, ConsumableWidth,
                $"x{slot.Count}", SlotCooldown, bg);
    }

    private static void DrawCentered(ICellSurface surface,
        int x, int y, int width, string text, Color fg, Color bg)
    {
        var startCol = (width - text.Length) / 2;
        for (var i = 0; i < text.Length; i++)
        {
            var col = startCol + i;
            if (col < 0 || col >= width) continue;
            surface.SetGlyph(x + col, y, text[i], fg, bg);
        }
    }

    private static Color ResourceColor(ResourceType resource) => resource switch
    {
        ResourceType.Rage => new Color(220, 90, 40),
        ResourceType.Focus => new Color(220, 200, 80),
        ResourceType.Insight => new Color(80, 160, 240),
        ResourceType.Echo => new Color(180, 100, 220),
        _ => new Color(180, 180, 180),
    };

    private static Color Dim(Color c, float factor) =>
        new((byte)(c.R * factor), (byte)(c.G * factor), (byte)(c.B * factor), c.A);
}

public readonly record struct SkillSlot(
    string Keybind,
    SkillDefinition? Skill,
    float CooldownRemaining);

// Bottom-bar slot for consumable items (potions, scrolls, etc.). Count is
// the stack size; 0 = empty placeholder (slot still draws so the keybind
// stays visible and the player learns the slot's role before they ever
// pick up the first item).
public readonly record struct ConsumableSlot(
    string Keybind,
    char Glyph,
    int Count);
