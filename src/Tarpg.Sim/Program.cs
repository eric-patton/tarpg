using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Tarpg.Core;
using Tarpg.Sim;
using Tarpg.World;

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
            if (opts.DumpFloor) return DumpFloor(opts);
            if (opts.Interactive) opts = PromptInteractive(opts);
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

    // Generation diagnostic: build a single floor (zone, seed, floor) and
    // print a full ASCII map with entry / threshold / enemy spawns marked,
    // plus reachability from entry to each spawn. No sim runs. Useful for
    // debugging "why does this seed time out" without parsing CSV outputs.
    private static int DumpFloor(Options opts)
    {
        ContentInitializer.Initialize();
        var zone = Registries.Zones.Get(opts.ZoneId);

        // Mirror TickRunner.Run's seed derivation so the dumped layout
        // matches what a sim run on the same seed actually generated.
        var rng = new Random(opts.SeedBase);
        var floorSeed = rng.Next();
        var floor = opts.FloorMin;

        var generated = zone.Generator.Generate(160, 60, floorSeed, floor);

        // Determine spawn types using the same RNG flow as TickRunner.Run
        // (one PickEnemyForZone call per spawn point, in order).
        var spawnDefs = new List<Tarpg.Enemies.EnemyDefinition>();
        foreach (var _ in generated.EnemySpawnPoints)
            spawnDefs.Add(PickEnemyForZoneDiag(zone, rng));

        Console.WriteLine($"# dump-floor: zone={opts.ZoneId} seed={opts.SeedBase} floor={floor}");
        Console.WriteLine($"# entry=({generated.Entry.X},{generated.Entry.Y}) threshold=({generated.BossAnchor.X},{generated.BossAnchor.Y})");
        Console.WriteLine($"# spawn points ({generated.EnemySpawnPoints.Count}):");
        for (var i = 0; i < generated.EnemySpawnPoints.Count; i++)
        {
            var p = generated.EnemySpawnPoints[i];
            var def = spawnDefs[i];
            var path = generated.Map.FindPath(generated.Entry, p);
            var reach = path is null ? "UNREACHABLE" : $"reachable in {path.Count} steps";
            Console.WriteLine($"#   [{i}] {def.Id,-12} at ({p.X,3},{p.Y,2}) — {reach}");
        }

        // Cell legend: # wall, . floor, > threshold, E entry, digit = spawn index (0-9, then letters).
        Console.WriteLine();
        var w = generated.Map.Width;
        var h = generated.Map.Height;
        var spawnByPos = new Dictionary<Position, int>();
        for (var i = 0; i < generated.EnemySpawnPoints.Count; i++)
            spawnByPos[generated.EnemySpawnPoints[i]] = i;

        for (var y = 0; y < h; y++)
        {
            var row = new System.Text.StringBuilder(w);
            for (var x = 0; x < w; x++)
            {
                var p = new Position(x, y);
                if (p == generated.Entry) { row.Append('E'); continue; }
                if (p == generated.BossAnchor) { row.Append('>'); continue; }
                if (spawnByPos.TryGetValue(p, out var idx))
                {
                    row.Append(idx < 10 ? (char)('0' + idx) : (char)('a' + (idx - 10)));
                    continue;
                }
                row.Append(generated.Map.IsWalkable(p) ? '.' : '#');
            }
            Console.WriteLine(row.ToString());
        }
        return 0;
    }

    // Lifted out of TickRunner so the diagnostic can derive enemy types
    // using the same RNG sequence without coupling to the runner's loop.
    private static Tarpg.Enemies.EnemyDefinition PickEnemyForZoneDiag(ZoneDefinition zone, Random rng)
    {
        var totalWeight = 0;
        foreach (var def in Registries.Enemies.All)
        {
            if (!def.ZoneIds.Contains(zone.Id)) continue;
            if (def.RarityWeight <= 0) continue;
            totalWeight += def.RarityWeight;
        }
        var pick = rng.Next(totalWeight);
        foreach (var def in Registries.Enemies.All)
        {
            if (!def.ZoneIds.Contains(zone.Id)) continue;
            if (def.RarityWeight <= 0) continue;
            pick -= def.RarityWeight;
            if (pick < 0) return def;
        }
        throw new InvalidOperationException("Weighted enemy roll fell through.");
    }

    // Interactive prompt: walks through each option in order, showing the
    // current default in brackets. Empty input keeps the default; values
    // that fail to parse re-prompt the same field. Combine with explicit
    // CLI args (e.g. `--interactive --seeds 100`) to pre-seed defaults.
    private static Options PromptInteractive(Options seed)
    {
        Console.WriteLine("TARPG sim — interactive");
        Console.WriteLine("(blank = keep default in brackets)");
        Console.WriteLine();

        var opts = new Options
        {
            Interactive = false,
            ZoneId = PromptString("Zone", seed.ZoneId),
            ClassId = PromptString("Class", seed.ClassId),
        };
        var floorRange = PromptString("Floors range (e.g. 1-15 or 5)", $"{seed.FloorMin}-{seed.FloorMax}");
        ParseFloorRange(floorRange, out opts.FloorMin, out opts.FloorMax);
        opts.SeedCount = PromptInt("Seeds per floor", seed.SeedCount);
        opts.SeedBase = PromptInt("Seed base", seed.SeedBase);
        opts.PilotId = PromptString("Pilot (greedy / kiting)", seed.PilotId);
        opts.Parallel = PromptInt("Parallel workers (1 = serial)", seed.Parallel ?? Environment.ProcessorCount);
        var outPath = PromptString("CSV output path (blank = skip)", seed.OutPath ?? "");
        opts.OutPath = string.IsNullOrWhiteSpace(outPath) ? null : outPath;

        Console.WriteLine();
        return opts;
    }

    private static string PromptString(string label, string @default)
    {
        while (true)
        {
            Console.Write($"  {label} [{@default}]: ");
            var line = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) return @default;
            return line.Trim();
        }
    }

    private static int PromptInt(string label, int @default)
    {
        while (true)
        {
            Console.Write($"  {label} [{@default}]: ");
            var line = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) return @default;
            if (int.TryParse(line.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                return n;
            Console.WriteLine($"  (not an integer — try again)");
        }
    }

    private static int RunSweep(Options opts)
    {
        // Touch a registry to force ContentInitializer to run (TickRunner
        // also does this internally, but we need definitions resolvable
        // before we start logging totals AND before parallel workers start
        // racing into Initialize concurrently — the inner lock is fine,
        // but doing it once up front keeps the hot path lock-free).
        ContentInitializer.Initialize();

        var classDef = Registries.Classes.Get(opts.ClassId);
        var playerMaxHp = classDef.BaseHealth;

        var totalRuns = (opts.FloorMax - opts.FloorMin + 1) * opts.SeedCount;
        var degree = Math.Max(1, opts.Parallel ?? Environment.ProcessorCount);
        var startedAt = DateTime.UtcNow;

        Console.WriteLine($"# tarpg-sim: zone={opts.ZoneId} class={opts.ClassId} pilot={opts.PilotId} " +
                          $"floors={opts.FloorMin}-{opts.FloorMax} seeds={opts.SeedCount}");
        Console.WriteLine($"# total runs: {totalRuns} (parallel: {degree})");

        // Build the full (floor, seed) work list up front so Parallel.ForEach
        // can dispatch them in any order without coupling to nested loops.
        var workItems = new List<WorkItem>(totalRuns);
        for (var floor = opts.FloorMin; floor <= opts.FloorMax; floor++)
            for (var seedIdx = 0; seedIdx < opts.SeedCount; seedIdx++)
                workItems.Add(new WorkItem(floor, opts.SeedBase + seedIdx));

        // Per-floor accumulators. Each worker, after finishing its run, calls
        // Add() on the right floor's state — when the last run for that floor
        // lands, Add() returns true and we emit the floor banner. Without
        // this we'd have no per-floor progress output at all (Parallel finishes
        // floors in random order; the old "print after the inner loop ends"
        // pattern doesn't apply anymore).
        var floorState = new ConcurrentDictionary<int, FloorAggregator>();
        for (var f = opts.FloorMin; f <= opts.FloorMax; f++)
            floorState[f] = new FloorAggregator(opts.SeedCount, DateTime.UtcNow);

        var results = new ConcurrentBag<RunRecord>();
        var completed = 0;

        // Console.WriteLine is line-atomic in .NET, but pairing the run line
        // with the (sometimes-following) floor summary needs a single critical
        // section so they don't get interleaved with another thread's run line.
        var consoleLock = new object();

        Parallel.ForEach(workItems,
            new ParallelOptions { MaxDegreeOfParallelism = degree },
            item =>
            {
                var cfg = new SimConfig
                {
                    ZoneId = opts.ZoneId,
                    ClassId = opts.ClassId,
                    Floor = item.Floor,
                    Seed = item.Seed,
                };

                var pilot = MakePilot(opts.PilotId);
                var result = global::Tarpg.Sim.TickRunner.Run(cfg, pilot);

                results.Add(new RunRecord(item.Seed, item.Floor, result));
                var indexForLine = Interlocked.Increment(ref completed) - 1;

                var floorAgg = floorState[item.Floor];
                var floorDone = floorAgg.Add(result);

                lock (consoleLock)
                {
                    Console.WriteLine(SimProgress.FormatRunLine(
                        index: indexForLine,
                        total: totalRuns,
                        floor: item.Floor, seed: item.Seed,
                        result: result, playerMaxHp: playerMaxHp));

                    if (floorDone)
                    {
                        var elapsed = (DateTime.UtcNow - floorAgg.StartedAt).TotalSeconds;
                        Console.WriteLine(SimProgress.FormatFloorSummary(
                            item.Floor, floorAgg.Count,
                            floorAgg.Cleared, floorAgg.Died, floorAgg.Timeout,
                            floorAgg.KillsAvg, elapsed));
                    }
                }
            });

        var elapsedSec = (DateTime.UtcNow - startedAt).TotalSeconds;
        Console.WriteLine($"# {results.Count} runs completed in {SimProgress.FormatWallTime(elapsedSec)}");

        // Sort once for deterministic CSV ordering and a stable aggregate
        // table regardless of the order workers finished in.
        var ordered = results.OrderBy(r => r.Floor).ThenBy(r => r.Seed).ToList();

        if (opts.OutPath is not null)
            WriteCsv(opts.OutPath, ordered);

        PrintAggregateSummary(ordered);
        return 0;
    }

    private readonly record struct WorkItem(int Floor, int Seed);

    // Thread-safe per-floor accumulator. All mutations are under `_lock`
    // because Add reads + writes multiple fields atomically and the floor
    // banner needs a consistent snapshot.
    private sealed class FloorAggregator
    {
        private readonly int _expected;
        private readonly object _lock = new();
        private int _cleared, _died, _timeout, _killsTotal, _count;

        public DateTime StartedAt { get; }

        public FloorAggregator(int expected, DateTime startedAt)
        {
            _expected = expected;
            StartedAt = startedAt;
        }

        public int Count => _count;
        public int Cleared => _cleared;
        public int Died => _died;
        public int Timeout => _timeout;
        public double KillsAvg => _count == 0 ? 0 : (double)_killsTotal / _count;

        // Returns true iff this call landed the final expected run for
        // the floor, signalling the caller to emit the floor banner.
        public bool Add(SimResult result)
        {
            lock (_lock)
            {
                _count++;
                _killsTotal += result.EnemiesKilled;
                switch (result.Outcome)
                {
                    case SimOutcome.PlayerCleared: _cleared++; break;
                    case SimOutcome.PlayerDied:    _died++;    break;
                    case SimOutcome.Timeout:       _timeout++; break;
                }
                return _count == _expected;
            }
        }
    }

    private static ISimPilot MakePilot(string id) => id switch
    {
        "greedy" => new GreedySimPilot(),
        "kiting" => new KitingSimPilot(),
        _ => throw new ArgException($"Unknown pilot '{id}' (supported: greedy, kiting)"),
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
                case "-p":
                case "--parallel":
                    opts.Parallel = int.Parse(RequireValue(args, ref i), CultureInfo.InvariantCulture);
                    break;
                case "-i":
                case "--interactive":
                    opts.Interactive = true;
                    break;
                case "--dump-floor":
                    opts.DumpFloor = true;
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
          --pilot <id>         Pilot strategy: greedy | kiting (default: greedy)
          --out <path>         CSV output path (omit to skip CSV; aggregates still print)
          -p, --parallel <n>   Worker threads (default: ProcessorCount; set to 1 for serial)
          -i, --interactive    Prompt for each option (defaults shown in brackets); combine
                               with explicit args to pre-seed answers (e.g. -i --seeds 100)
          --dump-floor         Print a single floor's ASCII map + entry / threshold / spawn
                               positions + reachability check, then exit. Uses --seed-base
                               and the first --floors value. No sim runs.
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
        public bool Interactive;
        public int? Parallel; // null = ProcessorCount default; 1 = serial
        public bool DumpFloor;
    }

    private sealed class ArgException(string message) : Exception(message);

    private sealed record RunRecord(int Seed, int Floor, SimResult Result);
}
