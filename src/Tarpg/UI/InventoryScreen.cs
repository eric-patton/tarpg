using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;
using Tarpg.Entities;
using Tarpg.Items;

namespace Tarpg.UI;

// Modal inventory overlay. Renders the equipment loadout (left half) +
// bag list (right half) on top of the live game; mouse clicks inside
// either pane equip / unequip items. Toggled visible by GameScreen on
// the 'i' key — while open, the world ticks are paused so the player
// can read / decide without enemies catching up.
//
// Click semantics (no drag-and-drop in v0 — click-to-equip is a clean
// enough fit and keeps the overlay's input pipeline simple):
//   - Click a bag row -> swap that item with whatever's equipped in
//     its matching slot (atomic; never increases bag count).
//   - Click an equipped slot -> unequip back to first empty bag slot
//     (no-op if the bag is full and there's no room).
//
// The selected-item details / tooltip pane the GDD eventually wants
// is deferred until the affix system gives us more to display than
// "Magic Weapon, +6 dmg" — for v0 each row already shows that inline.
public sealed class InventoryScreen : SadConsole.Console
{
    private static readonly Color TitleColor       = new(220, 200, 160);
    private static readonly Color HeaderColor      = new(180, 180, 200);
    private static readonly Color SeparatorColor   = new(80, 80, 80);
    private static readonly Color SlotLabelColor   = new(150, 150, 150);
    private static readonly Color EmptySlotColor   = new(80, 80, 80);
    private static readonly Color FooterColor      = new(140, 140, 140);

    // Tier color palette — lifted to top so per-row rendering stays
    // declarative. Roughly matches Diablo conventions: white normal,
    // blue magic, yellow rare, orange legendary.
    private static Color ColorForTier(ItemTier tier) => tier switch
    {
        ItemTier.Normal    => new Color(220, 220, 220),
        ItemTier.Magic     => new Color(120, 160, 255),
        ItemTier.Rare      => new Color(255, 220, 80),
        ItemTier.Legendary => new Color(220, 140, 60),
        ItemTier.Set       => new Color(80, 220, 120),
        _ => Color.White,
    };

    // Layout regions. EquipmentX..BagX split the viewport in half;
    // the row offsets put both panes on parallel rows so the eye
    // tracks them as paired columns.
    private const int EquipmentX = 2;
    private const int BagX       = 42;
    private const int FirstSlotRow = 6;

    // Equipment slots in display order. ItemSlot.None is excluded —
    // None items are consumables that live in the bottom-bar quick-
    // drink slots, not the loadout. Matches GDD §6's eight equip slots.
    private static readonly ItemSlot[] DisplayedSlots = new[]
    {
        ItemSlot.Weapon,
        ItemSlot.Offhand,
        ItemSlot.Head,
        ItemSlot.Chest,
        ItemSlot.Hands,
        ItemSlot.Feet,
        ItemSlot.Ring,
        ItemSlot.Amulet,
    };

    private readonly Player _player;
    private readonly Action _onClose;

    public InventoryScreen(int width, int height, Player player, Action onClose)
        : base(width, height)
    {
        _player = player;
        _onClose = onClose;

        // Always-on input handlers — we toggle IsVisible / IsFocused
        // from GameScreen, but the screen's own input methods need
        // to be ready when those flip on.
        UseKeyboard = true;
        UseMouse = true;
        FocusOnMouseClick = false; // GameScreen drives focus
        IsVisible = false;
    }

    public void Open()
    {
        IsVisible = true;
        IsFocused = true;
        Render();
    }

    public void Close()
    {
        IsVisible = false;
        _onClose();
    }

    public override bool ProcessKeyboard(Keyboard keyboard)
    {
        if (keyboard.IsKeyPressed(Keys.Escape) || keyboard.IsKeyPressed(Keys.I))
        {
            Close();
            return true;
        }
        return false;
    }

    public override bool ProcessMouse(MouseScreenObjectState state)
    {
        // Standard MouseScreenObjectState routing translates window
        // pixels to cell coords for us; we just react to the press
        // edge and resolve which row was clicked.
        if (!state.IsOnScreenObject) return base.ProcessMouse(state);
        if (!state.Mouse.LeftClicked) return base.ProcessMouse(state);

        var cellX = state.CellPosition.X;
        var cellY = state.CellPosition.Y;
        HandleClick(cellX, cellY);
        return true;
    }

    private void HandleClick(int cellX, int cellY)
    {
        // Equipment pane: rows FirstSlotRow .. FirstSlotRow + 7,
        // anywhere left of the bag column counts as a slot click.
        if (cellX < BagX)
        {
            var slotIndex = cellY - FirstSlotRow;
            if (slotIndex < 0 || slotIndex >= DisplayedSlots.Length) return;
            _player.UnequipToBag(DisplayedSlots[slotIndex]);
            Render();
            return;
        }
        // Bag pane: rows FirstSlotRow .. FirstSlotRow + bag length.
        var bagIndex = cellY - FirstSlotRow;
        if (bagIndex < 0 || bagIndex >= _player.Inventory.BagItems.Count) return;
        _player.EquipFromBag(bagIndex);
        Render();
    }

    private void Render()
    {
        // Fill every cell with a solid-black background BEFORE drawing
        // text — otherwise the world / bottom-HUD layers below us bleed
        // through transparent cells and the modal reads as a confused
        // overlay instead of a focused screen.
        var w = Surface.Width;
        var h = Surface.Height;
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
            Surface.SetGlyph(x, y, ' ', Color.White, Color.Black);

        // Title bar.
        const string Title = "I N V E N T O R Y";
        var titleX = Math.Max(0, (w - Title.Length) / 2);
        Surface.Print(titleX, 1, Title, TitleColor);
        for (var x = 0; x < w; x++)
        {
            Surface.SetGlyph(x, 2, '-', SeparatorColor, Color.Black);
        }

        // Pane headers.
        Surface.Print(EquipmentX, 4, "EQUIPMENT", HeaderColor);
        Surface.Print(BagX, 4, $"BAG ({_player.Inventory.BagCount}/{Tarpg.Inventory.Inventory.MaxBagSlots})", HeaderColor);
        for (var x = EquipmentX; x < EquipmentX + 9; x++) Surface.SetGlyph(x, 5, '-', SeparatorColor, Color.Black);
        for (var x = BagX; x < BagX + 9; x++) Surface.SetGlyph(x, 5, '-', SeparatorColor, Color.Black);

        // Equipment list — one row per slot. Empty slots draw "--" in
        // dim grey so the column stays readable as the player picks
        // up items and the loadout fills in.
        for (var i = 0; i < DisplayedSlots.Length; i++)
        {
            var slot = DisplayedSlots[i];
            var y = FirstSlotRow + i;
            var label = $"{slot,-9}: ";
            Surface.Print(EquipmentX, y, label, SlotLabelColor);

            var equipped = _player.GetEquipped(slot);
            if (equipped is null)
            {
                Surface.Print(EquipmentX + label.Length, y, "--", EmptySlotColor);
                continue;
            }
            // Compact equipment line — same shape as the bag rows so
            // the eye tracks them as paired columns. Tier color carries
            // the rarity; the bonus number sits at the end so quick
            // scans for "weapon damage upgrade?" land in the same place.
            var bonus = equipped.WeaponDamageBonus > 0 ? $" +{equipped.WeaponDamageBonus}" : "";
            var line = $"{equipped.Name}{bonus}";
            Surface.Print(EquipmentX + label.Length, y, line, ColorForTier(equipped.Tier));
        }

        // Bag list — number prefix + glyph + name + bonus. Tier color
        // already conveys rarity (white normal, blue magic, orange
        // legendary), so the row stays compact + readable. Empty
        // slots beyond the current item count don't render — keeps
        // the column scannable instead of a wall of "--" lines.
        var bagItems = _player.Inventory.BagItems;
        for (var i = 0; i < bagItems.Count; i++)
        {
            var item = bagItems[i];
            if (item is null) continue;
            var y = FirstSlotRow + i;
            var bonus = item.WeaponDamageBonus > 0 ? $"+{item.WeaponDamageBonus}" : "";
            var line = $"[{i + 1,2}] {item.Glyph} {item.Name,-22} {bonus}";
            Surface.Print(BagX, y, line, ColorForTier(item.Tier));
        }

        // Footer hints.
        var footer = "  [click bag] equip / swap     [click equipped] unequip     [I/Esc] close  ";
        var footerX = Math.Max(0, (w - footer.Length) / 2);
        Surface.Print(footerX, Surface.Height - 2, footer, FooterColor);
    }

}
