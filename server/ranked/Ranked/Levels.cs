using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Demolition.Generation;
using Demolition.Levels;
using Demolition.Ranked;

/// <summary>
/// The campaign as the game ships it: Content/&lt;Theme&gt;/Levels/*.json, each built with its theme's
/// Archetypes/*.json (RankedContentTests keeps these equal to the game's theme assets). A day's level
/// is generated once and kept.
/// </summary>
public sealed class Levels
{
    private readonly Dictionary<string, LevelSpec> _specs = new Dictionary<string, LevelSpec>();
    private readonly Dictionary<string, List<BuildingArchetype>> _archetypes = new Dictionary<string, List<BuildingArchetype>>();
    private readonly ConcurrentDictionary<(string, int), LevelLayout> _layouts = new ConcurrentDictionary<(string, int), LevelLayout>();

    public Levels(string content)
    {
        foreach (string theme in Directory.GetDirectories(content))
        {
            string levels = Path.Combine(theme, "Levels"), archetypes = Path.Combine(theme, "Archetypes");
            if (!Directory.Exists(levels) || !Directory.Exists(archetypes))
                continue;
            var list = new List<BuildingArchetype>();
            foreach (string json in Directory.GetFiles(archetypes, "*.json"))
                list.Add(BuildingArchetype.FromJson(File.ReadAllText(json)));
            foreach (string json in Directory.GetFiles(levels, "*.json"))
            {
                LevelSpec spec = LevelSpec.FromJson(File.ReadAllText(json));
                _specs[spec.id] = spec;
                _archetypes[spec.id] = list;
            }
        }
        Templates = new List<string>(_specs.Keys);
    }

    /// <summary>Every campaign level's id.</summary>
    public IReadOnlyList<string> Templates { get; }

    /// <summary>The level of <paramref name="day"/>, as LevelSelection.SelectRanked builds it; null for an unknown template.</summary>
    public LevelLayout Layout(RankedDay day)
    {
        if (!_specs.TryGetValue(day.Template, out LevelSpec spec))
            return null;
        if (_layouts.Count > 16)
            _layouts.Clear(); // a day's level or two is all anyone submits for
        return _layouts.GetOrAdd((day.Template, day.Seed), key =>
            LevelGrammar.Generate(spec.WithSeed(day.Seed, day.LevelId), _archetypes[day.Template], JumpProfile.Default, out _));
    }

    /// <summary>The level's display name ("TOWN 3"), for the boards.</summary>
    public string Name(string template) => _specs.TryGetValue(template, out LevelSpec spec) ? spec.name : template;
}
