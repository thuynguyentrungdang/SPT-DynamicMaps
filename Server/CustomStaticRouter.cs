using DynamicMaps.Common;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using System.Text.Json;

namespace _dynamicMapsServer;

[Injectable]
public class CustomStaticRouter : StaticRouter
{
    private static ModConfig? _modConfig;

    public CustomStaticRouter(
        JsonUtil jsonUtil,
        DatabaseService database,
        SaveServer saveServer,
        ISptLogger<CustomStaticRouter> logger) : base(
        jsonUtil,
        GetCustomRoutes(database, saveServer, logger))
    {
    }

    public void PassConfig(ModConfig config, DatabaseService database, DynamicMapsPreload dynamicMapsPreload)
    {
        _modConfig = config;
        _spawnPoints = dynamicMapsPreload.SpawnPointsDict;
    }

    private static List<RouteAction> GetCustomRoutes(DatabaseService database, SaveServer saveServer, ISptLogger<CustomStaticRouter> logger)
    {
        return
        [
            new RouteAction<EmptyRequestData>(
                Routes.LoadConfigRoute,
                async (
                    url,
                    info,
                    sessionId,
                    output
                ) => await HandleConfigRequestRoute()
            ),
            new RouteAction<EmptyRequestData>(
                Routes.GetQuestItemsForMap,
                    async(
                        url,
                        info,
                        session,
                        output
                        ) => await HandleMapData(session, database, saveServer, logger)
                )
        ];
    }


    private static ValueTask<string> HandleMapData(MongoId session, DatabaseService database, SaveServer saveServer, ISptLogger<CustomStaticRouter> logger)
    {
        try
        {
            logger.Debug($"Call from {session}");
            var profile = saveServer.GetProfile(session);
            var openQuests = profile.CharacterData?.PmcData?.Quests?
                .Where(q => q.Status == SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Started).ToList()
                ?? [];
            if (!openQuests.Any())
                return new ValueTask<string>(JsonSerializer.Serialize(new List<ConditionData>()));

            logger.Debug($"Found {openQuests.Count} open quests");

            List<ConditionData> conditionData = [];
            var questData = database.GetQuests().Where(k => openQuests.Select(q => q.QId).Contains(k.Key)).ToList();
            foreach (var quest in questData)
            {
                foreach (var condition in quest.Value.Conditions?.AvailableForFinish ?? [])
                {
                    if (condition.ConditionType == "FindItem" && !(openQuests.Any(q => (q.CompletedConditions ?? []).Contains(condition.Id))))
                    {
                        conditionData.Add(new ConditionData(quest.Key, condition.Id, condition.Target!.IsList ? condition.Target.List!.First() : condition.Target.Item!));
                    }
                }
            }

            logger.Debug($"Total {conditionData.Count} items to be found");

            var items = database.GetItems().ToList().Where(k => k.Value.IsQuestItem() && conditionData.Any(c => c.ItemId == k.Value.Id.ToString())).Select(k => k.Value).ToList();
            conditionData.RemoveAll(c => !items.Select(k => k.Id).Contains(c.ItemId));

            logger.Debug($"Reduced to {conditionData.Count} with quest items from db");

            List<string> mapNames = new()
            {
                "bigmap", // customs
                "interchange", // interchange
                "laboratory", // lab
                "lighthouse", // lighthouse
                "rezervbase", // reserve 
                "shoreline", // shoreline
                "tarkovstreets", // streets of tarkov
                "labyrinth", // labyrinth
                "woods", // woods
                "factory4_day", // factory day
                "factory4_night", // factory night
                "sandbox", // ground zero low level
                "sandbox_high", // ground zero high level
            };

            foreach (var map in mapNames)
            {
                CreateMapItemData(map, conditionData, logger);
            }

            foreach (var item in conditionData)
            {
                logger.Debug($"Quest {item.QuestId}: Cond {item.ConditionId}, Item {item.ItemId} on {item.MapName} at Pos {item.SpawnPoint}");
            }

            return new ValueTask<string>(JsonSerializer.Serialize(conditionData));
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
            return new ValueTask<string>(JsonSerializer.Serialize(new List<ConditionData>()));
        }
    }

    private static Dictionary<string, List<Spawnpoint>> _spawnPoints = null!;

    private static void CreateMapItemData(string mapName, List<ConditionData> items, ISptLogger<CustomStaticRouter> logger)
    {
        List<Spawnpoint> spawnPoints = _spawnPoints[mapName];
        foreach (var point in spawnPoints)
        {
            foreach (var cond in items.Where(i => !i.IsSet))
            {
                if (cond.ItemId == (point.Template?.Items?.FirstOrDefault()?.Template ?? string.Empty))
                    cond.SetSpawnPoint(mapName, [(float)point.Template!.Position!.X!.Value, (float)point.Template!.Position!.Y!.Value, (float)point.Template!.Position!.Z!.Value]);
            }

            if (items.All(i => i.IsSet))
                return;
        }
    }

    private static ValueTask<string> HandleConfigRequestRoute()
    {
        return new ValueTask<string>(JsonSerializer.Serialize(_modConfig));
    }
}