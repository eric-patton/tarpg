using System.Globalization;
using System.Text;
using Tarpg.Core;
using Tarpg.Sim;

namespace Tarpg.Sim.Cli;

// Headless balance-tuning runner. Sweeps a (floor × seed) grid for one
// (zone, class, pilot) combination and writes a CSV per-run + an aggregate
// summary to stdout. Used to tune enemy / class / item numbers across the
// whole kit by aggregate outcome rather than first-principles math.
//
// Usage:
//   tarpg-sim --zone wolfwood --class reaver \
//             --floors 1-15 --seeds 100 --pilot greedy \
//             --out runs/2026-04-30-baseline.csv
//
// All args have sensible defaults; the bare `tarpg-sim` invocation runs a
// small sanity sweep so you can verify the harness wired up correctly.
public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var opts = ParseArgs(args);
            return RunSweep(opts);
        }
        catch (ArgException ex)
        {
            Console.Error.WriteLine($"tarpg-sim: {ex.Message}");
            Console.Error.WriteLine();
            Console.Error.WriteLine(Usage);
            return 2;
        }
    }

    private static int RunSweep(Options opts)
    {
        // Touch a registry to force ContentInitializer to run (TickRunner
        // also does this internally, but we need definitions resolvable
        // before we start logging totals).
        ContentInitializer.Initialize();

        var allResults = new List<RunRecord>();
        var startedAt = DateTime.UtcNow;

        Console.WriteLine($"# tarpg-sim: zone={opts.ZoneId} class={opts.ClassId} pilot={opts.PilotId} " +
                          $"floors={opts.FloorMin}-{opts.FloorMax} seeds={opts.SeedCount}");

        for (var floor = opts.FloorMin; floor <= opts.FloorMax; floor++)
        {
            for (var seedIdx = 0; seedIdx < opts.SeedCount; seedIdx++)
            {
                var seed = opts.SeedBase + seedIdx;
                var cfg = new SimConfig
                {
                    ZoneId = opts.ZoneId,
                    ClassId = opts.ClassId,
                    Floor = floor,
                    Seed = seed,
                };

                var pilot = MakePilot(opts.PilotId);
                var result = global::Tarpg.Sim.TickRunner.Run(cfg, pilot);

                allResults.Add(new RunRecord(seed, floor, result));
            }
        }

        var elapsedSec = (DateTime.UtcNow - startedAt).TotalSeconds;
        Console.WriteLine($"# {allResults.Count} runs completed in {elapsedSec:F1}s");

        if (opts.OutPath is not null)
            WriteCsv(opts.OutPath, allResults);

        PrintAggregateSummary(allResults);
        return 0;
    }

    private static ISimPilot MakePilot(string id) => id switch
    {
        "greedy" => new GreedySimPilot(),
        _ => throw new ArgException($"Unknown pilot '{id}' (supported: greedy)"),
    };

    private static void WriteCsv(string path, IReadOnlyList<RunRecord> rows)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        // Per-enemy kill columns: union of all enemy ids seen across rows so
        // the CSV schema is consistent regardless of which seeds happened to
        // spawn what. Sorted for deterministic header order.
        var allEnemyIds = new SortedSet<string>();
        foreach (var r in rows)
            foreach (var id in r.Result.KillsByEnemyId.Keys)
                allEnemyIds.Add(id);

        var sb = new StringBuilder();
        sb.Append("seed,floor,outcome,ticks,sim_seconds,initial_enemies,enemies_killed,");
        sb.Append("dmg_dealt,dmg_taken,hp_end,hp_min,skill_uses");
        foreach (var id in allEnemyIds)
        {
            sb.Append(",kills_");
            sb.Append(id);
        }
        sb.AppendLine();

        foreach (var r in rows)
        {
            var x = r.Result;
            sb.Append(r.Seed.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(r.Floor.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(x.Outcome);
            sb.Append(',');
            sb.Append(x.TicksElapsed.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(x.SimSeconds.ToString("F3", CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(x.InitialEnemyCount.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(x.EnemiesKilled.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(x.PlayerDamageDealt.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(x.PlayerDamageTaken.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(x.PlayerHpAtEnd.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(x.PlayerHpMin.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(x.SkillUses.ToString(CultureInfo.InvariantCulture));
            foreach (var id in allEnemyIds)
            {
                sb.Append(',');
                sb.Append((x.KillsByEnemyId.TryGetValue(id, out var k) ? k : 0)
                    .ToString(CultureInfo.InvariantCulture));
            }
            sb.AppendLine();
        }

        File.WriteAllText(path, sb.ToString());
        Console.WriteLine($"# wrote {rows.Count} rows to {path}");
    }

    private static void PrintAggregateSummary(IReadOnlyList<RunRecord> rows)
    {
        Console.WriteLine();
        Console.WriteLine("Floor   N   Cleared%  Died%  TimeOut%  HpEnd(p50)  HpMin(p50)  Kills(avg)  Time(s)(avg)");
        Console.WriteLine("-----  ---  --------  -----  --------  ----------  ----------  ----------  ------------");

        var byFloor = rows.GroupBy(r => r.Floor).OrderBy(g => g.Key);
        foreach (var group in byFloor)
        {
            var n = group.Count();
            var cleared = group.Count(r => r.Result.Outcome == SimOutcome.PlayerCleared);
            var died = group.Count(r => r.Result.Outcome == SimOutcome.PlayerDied);
            var timeout = group.Count(r => r.Result.Outcome == SimOutcome.Timeout);

            var hpEndP50 = Median(group.Select(r => r.Result.PlayerHpAtEnd));
            var hpMinP50 = Median(group.Select(r => r.Result.PlayerHpMin));
            var killsAvg = group.Average(r => (double)r.Result.EnemiesKilled);
            var timeAvg = group.Average(r => (double)r.Result.SimSeconds);

            Console.WriteLine(
                $"{group.Key,5}  {n,3}  {cleared * 100.0 / n,7:F1}%  " +
                $"{died * 100.0 / n,4:F1}%  {timeout * 100.0 / n,7:F1}%  " +
                $"{hpEndP50,10}  {hpMinP50,10}  {killsAvg,10:F1}  {timeAvg,12:F1}");
        }
    }

    private static int Median(IEnumerable<int> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0) return 0;
        return sorted[sorted.Count / 2];
    }

    // Argument parsing. Long-form only (--name value), no short flags. Keeps
    // the parser tiny — we add a real CLI lib if/when sweep configs grow
    // beyond ~6 args.
    private static Options ParseArgs(string[] args)
    {
        var opts = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--zone":
                    opts.ZoneId = RequireValue(args, ref i);
                    break;
                case "--class":
                    opts.ClassId = RequireValue(args, ref i);
                    break;
                case "--floors":
                    ParseFloorRange(RequireValue(args, ref i), out opts.FloorMin, out opts.FloorMax);
                    break;
                case "--seeds":
                    opts.SeedCount = int.Parse(RequireValue(args, ref i), CultureInfo.InvariantCulture);
                    break;
                case "--seed-base":
                    opts.SeedBase = int.Parse(RequireValue(args, ref i), CultureInfo.InvariantCulture);
                    break;
                case "--pilot":
                    opts.PilotId = RequireValue(args, ref i);
                    break;
                case "--out":
                    opts.OutPath = RequireValue(args, ref i);
                    break;
                case "-h":
                case "--help":
                    Console.WriteLine(Usage);
                    Environment.Exit(0);
                    break;
                default:
                    throw new ArgException($"Unknown argument '{args[i]}'");
            }
        }
        return opts;
    }

    private static string RequireValue(string[] args, ref int i)
    {
        if (i + 1 >= args.Length) throw new ArgException($"Missing value for '{args[i]}'");
        return args[++i];
    }

    private static void ParseFloorRange(string value, out int min, out int max)
    {
        var dash = value.IndexOf('-');
        if (dash < 0)
        {
            min = max = int.Parse(value, CultureInfo.InvariantCulture);
            return;
        }
        min = int.Parse(value[..dash], CultureInfo.InvariantCulture);
        max = int.Parse(value[(dash + 1)..], CultureInfo.InvariantCulture);
        if (min > max) throw new ArgException($"--floors {value}: min must be <= max");
    }

    private const string Usage = """
        Usage: tarpg-sim [options]

          --zone <id>          Zone to simulate (default: wolfwood)
          --class <id>         Player class id (default: reaver)
          --floors <range>     Floor range, e.g. "1-15" or "5" (default: 1-10)
          --seeds <n>          Seeds per floor (default: 25)
          --seed-base <n>      First seed; subsequent seeds are seed-base + i (default: 1000)
          --pilot <id>         Pilot strategy: greedy (default: greedy)
          --out <path>         CSV output path (omit to skip CSV; aggregates still print)
          -h, --help           Show this message
        """;

    private sealed class Options
    {
        public string ZoneId = "wolfwood";
        public string ClassId = "reaver";
        public int FloorMin = 1;
        public int FloorMax = 10;
        public int SeedCount = 25;
        public int SeedBase = 1000;
        public string PilotId = "greedy";
        public string? OutPath;
    }

    private sealed class ArgException(string message) : Exception(message);

    private sealed record RunRecord(int Seed, int Floor, SimResult Result);
}
