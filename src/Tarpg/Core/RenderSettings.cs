namespace Tarpg.Core;

// Single source of truth for render-mode toggles. Both Program.cs (which
// loads the default font) and GameScreen.cs (which sets cell sizes / aspect
// ratios) read from here so they always agree about which mode is active.
public static class RenderSettings
{
    // Vertical-movement experiment toggle:
    //   false = Option A — render the embedded IBM 8x16 font; movement
    //           applies aspect correction so visual pixel-speed is uniform.
    //   true  = Option B — render the bundled Adam Milazzo 12x12 square
    //           CP437 font; cells are square so movement is symmetric in
    //           both pixels AND tile-units, no correction needed.
    // static readonly (not const) so flipping the toggle doesn't generate
    // unreachable-code warnings in the consumer if/else branches.
    public static readonly bool UseSquareCells = true;

    // Path to the bundled square font, copied to bin/.../Content/ at build.
    // Only used when UseSquareCells is true.
    public const string SquareFontPath = "Content/font_12x12.font";

    // FOV master switch. When false, GameScreen renders every tile full-color
    // and every entity visible — the pre-FOV behavior. Useful for bisecting
    // render bugs vs. visibility bugs.
    public static readonly bool EnableFov = true;

    // RGB multiplier applied to explored-but-not-currently-visible tiles.
    // 0.3 was the original spec but rendered floor tiles (foreground 80,80,80)
    // at (24,24,24) — basically invisible against the black background. 0.5
    // brings floor to (40,40,40) and walls to (80,70,55), which reads as a
    // distinct dimmed-but-visible memory layer.
    public static readonly float UnseenDimFactor = 0.5f;

    // Class id the player starts with on a fresh game. Resolved against
    // Registries.Classes by GameScreen at startup. Live class-select UI
    // is deferred — for now flip this constant and recompile to play a
    // different class. Sim runners override via the --class CLI arg.
    public static readonly string StartingClassId = "reaver";
}
