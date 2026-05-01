using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;
using Tarpg.Classes;
using Tarpg.Core;
using Tarpg.Skills;

namespace Tarpg.UI;

// Title screen — pick a walker class before the game starts. Lists every
// playable class (one with a non-empty StartingSlotSkills kit), shows the
// selected class's tagline / description / stats / kit, fires a callback
// with the chosen class id when the player confirms.
//
// Program.cs decides whether to bother showing this screen at all: with
// only one playable class we skip straight to GameScreen so the player
// doesn't see a one-option menu.
public sealed class ClassSelectScreen : SadConsole.Console
{
    private static readonly Color TitleColor = new(220, 200, 160);
    private static readonly Color SelectedRowFg = new(20, 20, 20);
    private static readonly Color SelectedRowBg = new(220, 200, 160);
    private static readonly Color UnselectedFg = new(180, 180, 180);
    private static readonly Color DimFg = new(110, 110, 110);
    private static readonly Color BodyFg = new(200, 200, 200);
    private static readonly Color FooterFg = new(140, 140, 140);

    private readonly IReadOnlyList<WalkerClassDefinition> _classes;
    private readonly Action<string> _onConfirm;
    private int _selectedIndex;

    public ClassSelectScreen(
        int width,
        int height,
        IReadOnlyList<WalkerClassDefinition> playableClasses,
        Action<string> onConfirm,
        string? initialClassId = null)
        : base(width, height)
    {
        if (playableClasses.Count == 0)
            throw new ArgumentException(
                "ClassSelectScreen requires at least one playable class.",
                nameof(playableClasses));

        _classes = playableClasses;
        _onConfirm = onConfirm;

        // Default selection: the configured starting-class id when present,
        // otherwise the first entry. Keeps the menu's initial highlight in
        // sync with the previous "flip RenderSettings.StartingClassId and
        // recompile" workflow without forcing the player to scroll to it.
        if (initialClassId is not null)
        {
            for (var i = 0; i < _classes.Count; i++)
            {
                if (_classes[i].Id == initialClassId)
                {
                    _selectedIndex = i;
                    break;
                }
            }
        }

        UseKeyboard = true;
        UseMouse = false;
        IsFocused = true;
        FocusOnMouseClick = true;

        Render();
    }

    public override bool ProcessKeyboard(Keyboard keyboard)
    {
        if (keyboard.IsKeyPressed(Keys.Up) || keyboard.IsKeyPressed(Keys.W))
        {
            _selectedIndex = (_selectedIndex - 1 + _classes.Count) % _classes.Count;
            Render();
            return true;
        }
        if (keyboard.IsKeyPressed(Keys.Down) || keyboard.IsKeyPressed(Keys.S))
        {
            _selectedIndex = (_selectedIndex + 1) % _classes.Count;
            Render();
            return true;
        }
        if (keyboard.IsKeyPressed(Keys.Enter) || keyboard.IsKeyPressed(Keys.Space))
        {
            _onConfirm(_classes[_selectedIndex].Id);
            return true;
        }
        if (keyboard.IsKeyPressed(Keys.Escape))
        {
            // Quit out of the title — same effect as closing the window.
            // We use System.Environment.Exit instead of Game.Instance.Exit
            // to avoid coupling this screen to MonoGame internals.
            Environment.Exit(0);
            return true;
        }
        return base.ProcessKeyboard(keyboard);
    }

    private void Render()
    {
        Surface.Clear();

        var w = Surface.Width;

        // Title - centered on row 1. Plain ASCII hyphen because the CP437
        // font we ship lacks an em-dash (it'd render as a blank cell).
        const string Title = "TARPG - Choose your walker";
        var titleX = Math.Max(0, (w - Title.Length) / 2);
        Surface.Print(titleX, 1, Title, TitleColor);

        // Class list — centered, each row 4 spaces taller than wide so the
        // arrow + name fits with a comfortable gutter on either side.
        var listStartY = 4;
        for (var i = 0; i < _classes.Count; i++)
        {
            var cls = _classes[i];
            var label = $"  {cls.Name}  ";
            var labelX = Math.Max(0, (w - label.Length) / 2);
            var y = listStartY + i;
            if (i == _selectedIndex)
            {
                // Highlighted row: paint background across the label width
                // so the selection reads as a button rather than a text
                // marker. Two-space pad on either side of the name.
                for (var x = 0; x < label.Length; x++)
                    Surface.SetGlyph(labelX + x, y, ' ', SelectedRowFg, SelectedRowBg);
                Surface.Print(labelX, y, label, SelectedRowFg, SelectedRowBg);
            }
            else
            {
                Surface.Print(labelX, y, label, UnselectedFg);
            }
        }

        // Detail block — starts a few rows below the list. Tagline (italic-
        // feel via quotes), description, then the stat / kit lines.
        var detailY = listStartY + _classes.Count + 2;
        var selected = _classes[_selectedIndex];

        Surface.Print(2, detailY, selected.Name, selected.GlyphColor);
        detailY++;
        Surface.Print(2, detailY, $"\"{selected.Tagline}\"", DimFg);
        detailY += 2;
        Surface.Print(2, detailY, selected.Description, BodyFg);
        detailY += 2;
        Surface.Print(2, detailY,
            $"HP {selected.BaseHealth}    {selected.Resource} {selected.BaseResource}",
            BodyFg);
        detailY += 2;

        Surface.Print(2, detailY, "Kit:", BodyFg);
        detailY++;

        // One row per slot. Reuse GameLoopController's slot index ordering
        // (M2/Q/W/E/R) so the menu names match the in-game keybinds.
        var slotLabels = new[] { "M2", "Q ", "W ", "E ", "R " };
        for (var i = 0; i < selected.StartingSlotSkills.Count && i < slotLabels.Length; i++)
        {
            var skillId = selected.StartingSlotSkills[i];
            if (skillId is null)
            {
                Surface.Print(4, detailY + i, $"{slotLabels[i]}  --", DimFg);
                continue;
            }
            if (!Registries.Skills.TryGet(skillId, out var skill))
            {
                Surface.Print(4, detailY + i, $"{slotLabels[i]}  ?? ({skillId})", DimFg);
                continue;
            }
            // Glyph + name + cost / cooldown summary. Cost suppressed when
            // zero so M2 entries (Heavy Strike, Quick Shot) read as "free."
            var costStr = skill.Cost > 0 ? $"{skill.Cost} {skill.Resource}" : "free";
            var cdStr = skill.CooldownSec > 0 ? $"{skill.CooldownSec:F1}s cd" : "no cd";
            var line = $"{slotLabels[i]}  {skill.Glyph} {skill.Name,-14} {costStr,-12} {cdStr}";
            Surface.Print(4, detailY + i, line, BodyFg);
        }

        // Footer — controls hint, pinned to the bottom row.
        var footer = "  Up / Down: switch    Enter: play    Esc: quit  ";
        var footerX = Math.Max(0, (w - footer.Length) / 2);
        Surface.Print(footerX, Surface.Height - 2, footer, FooterFg);
    }
}
