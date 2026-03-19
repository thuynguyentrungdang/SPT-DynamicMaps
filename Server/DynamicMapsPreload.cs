using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Services;

namespace _dynamicMapsServer;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 0)]
public class DynamicMapsPreload(DatabaseService databaseService) : IOnLoad
{
    public Dictionary<string, List<Spawnpoint>> SpawnPointsDict { get; private set; }

    public Task OnLoad()
    {
        SpawnPointsDict = PopulateSpawnPoints(databaseService);
        return Task.CompletedTask;
    }

    private static Dictionary<string, List<Spawnpoint>> PopulateSpawnPoints(DatabaseService database)
    {
        var locations = database.GetLocations();
        return new Dictionary<string, List<Spawnpoint>>()
        {
            ["bigmap"] = [.. locations.Bigmap.LooseLoot!.Value!.SpawnpointsForced!.ToList()],
            ["interchange"] = [.. locations.Interchange.LooseLoot!.Value!.SpawnpointsForced!.ToList()],
            ["laboratory"] = [.. locations.Laboratory.LooseLoot!.Value!.SpawnpointsForced!.ToList()],
            ["lighthouse"] = [.. locations.Lighthouse.LooseLoot!.Value!.SpawnpointsForced!.ToList()],
            ["rezervbase"] = [.. locations.RezervBase.LooseLoot!.Value!.SpawnpointsForced!.ToList()],
            ["shoreline"] = [.. locations.Shoreline.LooseLoot!.Value!.SpawnpointsForced!.ToList()],
            ["tarkovstreets"] = [.. locations.TarkovStreets.LooseLoot!.Value!.SpawnpointsForced!.ToList()],
            ["labyrinth"] = [.. locations.Labyrinth.LooseLoot!.Value!.SpawnpointsForced!.ToList()],
            ["woods"] = [.. locations.Woods.LooseLoot!.Value!.SpawnpointsForced!.ToList()],
            ["factory4_day"] = [.. locations.Factory4Day.LooseLoot!.Value!.SpawnpointsForced!.ToList()],
            ["factory4_night"] = [.. locations.Factory4Night.LooseLoot!.Value!.SpawnpointsForced!.ToList()],
            ["sandbox"] = [.. locations.Sandbox.LooseLoot!.Value!.SpawnpointsForced!.ToList()],
            ["sandbox_high"] = [.. locations.SandboxHigh.LooseLoot!.Value!.SpawnpointsForced!.ToList()]
        };
    }
}
