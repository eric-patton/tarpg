using SadConsole;
using SadConsole.Configuration;
using Tarpg.Core;
using Tarpg.UI;

const int ScreenWidth = 80;
const int ScreenHeight = 30;

Settings.WindowTitle = "TARPG";

var builder = new Builder()
    .SetWindowSizeInCells(ScreenWidth, ScreenHeight)
    .OnStart(OnGameStart);

// Choose the default font based on the render-mode toggle. Both modes share
// every other piece of the configuration; only the font asset differs.
//
// Resolve the custom font path against the executable's directory so it works
// regardless of the caller's current working directory (run.bat sets cwd to
// the project root, not the bin folder).
if (RenderSettings.UseSquareCells)
{
    var fontPath = Path.Combine(AppContext.BaseDirectory, RenderSettings.SquareFontPath);
    builder.ConfigureFonts(fontPath);
}
else
{
    builder.ConfigureFonts(true);
}

Game.Create(builder);
Game.Instance.Run();
Game.Instance.Dispose();
return;

static void OnGameStart(object? sender, GameHost host)
{
    ContentInitializer.Initialize();

    // Playable = a class with at least one wired skill slot. Cipher /
    // Speaker stubs have all-null kits, so they're skipped. With only one
    // playable class we'd present a one-option menu — silly — so jump
    // straight to the game in that case.
    var playable = Tarpg.Core.Registries.Classes.All
        .Where(c => c.StartingSlotSkills.Any(s => s != null))
        .OrderBy(c => c.Id, StringComparer.Ordinal)
        .ToList();

    Game.Instance.DestroyDefaultStartingConsole();

    if (playable.Count <= 1)
    {
        var only = playable.Count == 1 ? playable[0].Id : Tarpg.Core.RenderSettings.StartingClassId;
        Game.Instance.Screen = new GameScreen(ScreenWidth, ScreenHeight, classId: only);
        return;
    }

    Game.Instance.Screen = new ClassSelectScreen(
        ScreenWidth, ScreenHeight,
        playable,
        onConfirm: chosenId =>
        {
            Game.Instance.Screen = new GameScreen(ScreenWidth, ScreenHeight, classId: chosenId);
        },
        initialClassId: Tarpg.Core.RenderSettings.StartingClassId);
}
