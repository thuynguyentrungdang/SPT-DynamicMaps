using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using DynamicMaps.Common;
using DynamicMaps.Data;
using EFT;
using EFT.Interactive;
using EFT.Quests;
using HarmonyLib;
using UnityEngine;

namespace DynamicMaps.Utils
{
    // NOTE: Most of this is adapted from work done for Prop's GTFO mod (https://github.com/dvize/GTFO)
    // this likely does not count as a "substantial portion" of the software
    // under MIT license (https://github.com/dvize/GTFO/blob/master/LICENSE.txt)
    public static class QuestUtils
    {

        // TODO: move to config
        private const string _questCategory = "Quest";
        private const string _questImagePath = "Markers/quest.png";
        private static Vector2 _questPivot = new Vector2(0.5f, 0f);
        private static Color _questColor = Color.green;
        //

        private static List<TriggerWithIdAbstraction> TriggersWithIds;
        private static List<LootItemAbstraction> QuestItems;

        internal class LootItemAbstraction
        {
            public string ItemId { get; set; }
            public Vector3 Position { get; set; }
        }

        internal static void TryCaptureQuestData(MapDef def)
        {
            var gameWorld = Singleton<GameWorld>.Instance;

            if (TriggersWithIds == null)
            {
                if (def.TriggersWithId.Any())
                {
                    TriggersWithIds = [.. def.TriggersWithId];
                }
                else
                {
                    TriggersWithIds = [.. GameObject.FindObjectsOfType<TriggerWithId>().Select(k => {
                        var box = k.GetComponent<BoxCollider>();
                        var worldCenter = box.transform.TransformPoint(box.center);
                        var worldSize = Vector3.Scale(box.size, box.transform.lossyScale.Abs());
                        var yaw = box.transform.eulerAngles.y;
                        return new TriggerWithIdAbstraction
                        {
                            Id = k.Id,
                            Position = worldCenter,
                            Size = worldSize,
                            YawDegrees = yaw
                        };
                    })];
                }
            }

            QuestItems ??= [.. Traverse.Create(gameWorld)
                    .Field("LootItems")
                    .Field("List_0")
                    .GetValue<List<LootItem>>()
                    .Where(i => i.Item.QuestItem).Select(k => new LootItemAbstraction()
                    {
                        ItemId = k.TemplateId,
                        Position = k.transform.position
                    })];
        }

        internal static void FillQuestDataOutOfRaid(List<ConditionData> data, MapDef def)
        {
            QuestItems ??= [.. data.Select(d => new LootItemAbstraction()
                {
                    ItemId = d.ItemId,
                    Position = new Vector3(d.SpawnPoint[0], d.SpawnPoint[1], d.SpawnPoint[2])
                })];

            TriggersWithIds ??= [.. def.TriggersWithId];
        }

        internal static void DiscardQuestData()
        {
            if (TriggersWithIds != null)
            {
                TriggersWithIds.Clear();
                TriggersWithIds = null;
            }

            if (QuestItems != null)
            {
                QuestItems.Clear();
                QuestItems = null;
            }
        }

        internal static IEnumerable<MapMarkerDef> GetMarkerDefsForPlayer(AbstractQuestControllerClass questController)
        {
            if (TriggersWithIds == null || QuestItems == null || questController == null)
            {
                Plugin.Log.LogWarning($"TriggersWithIds null: {TriggersWithIds == null} or QuestItems null: {QuestItems == null} or Player null: {questController == null}");
                return null;
            }

            var markers = new List<MapMarkerDef>();

            var quests = GetIncompleteQuests(questController);
            foreach (var quest in quests)
            {
                markers.AddRange(GetMarkerDefsForQuest(questController, quest));
            }

            return markers;
        }

        internal static IEnumerable<MapMarkerDef> GetMarkerDefsForQuest(AbstractQuestControllerClass questController, QuestDataClass quest)
        {
            var markers = new List<MapMarkerDef>();

            var conditions = GetIncompleteQuestConditions(questController, quest);
            foreach (var condition in conditions)
            {
                var questName = quest.Template.NameLocaleKey.BSGLocalized();
                var conditionDescription = condition.id.BSGLocalized();

                var conditionData = GetConditionData(condition, questName, conditionDescription);
                foreach (var data in conditionData)
                {
                    var isDuplicate = false;

                    // check against previously created markers for duplicate position
                    foreach (var marker in markers)
                    {
                        if (marker.Position.ApproxEquals(data.Position))
                        {
                            isDuplicate = true;
                            break;
                        }
                    }

                    if (isDuplicate)
                    {
                        continue;
                    }

                    TriggerWithIdAbstraction triggerWithIdAbstraction = null;
                    if (data.Condition.InnerCondition is ConditionInZone or ConditionZone or ConditionVisitPlace or ConditionLaunchFlare)
                    {
                        Plugin.Log.LogInfo($"Trying to find trigger for condition {conditionDescription} at position {data.Position} in zone {data.ZoneId}");
                        triggerWithIdAbstraction = TriggersWithIds.FirstOrDefault(f => f.Id == data.ZoneId
                            && (TriggersWithIds.Count(z => z.Id == f.Id) == 1 || MathUtils.ConvertToMapPosition(f.Position).ApproxEquals(data.Position, 1)));
                        if (triggerWithIdAbstraction != null)
                            Plugin.Log.LogInfo($"Found trigger {triggerWithIdAbstraction?.Id} at position {triggerWithIdAbstraction?.Position} for condition {conditionDescription}");
                    }


                    var mapDef = CreateQuestMapMarkerDef(data.Position, questName, conditionDescription, triggerWithIdAbstraction);
                    markers.Add(mapDef);

                    if (data.Condition.InnerCondition is ConditionZone zoneCondition)
                    {
                        var targetItemId = zoneCondition.target.FirstOrDefault();
                        if (zoneCondition is ConditionLeaveItemAtLocation)
                        {
                            if (targetItemId == "5b4391a586f7745321235ab2") // wifi camera
                                mapDef.Sprite = TextureUtils.GetOrLoadCachedSprite("Markers/camera.png");
                            else
                                mapDef.Sprite = TextureUtils.GetOrLoadCachedSprite("Markers/deposit.png");
                        }

                        if (zoneCondition is ConditionPlaceBeacon)
                        {
                            if (targetItemId == "5991b51486f77447b112d44f") // ms2000 marker
                                mapDef.Sprite = TextureUtils.GetOrLoadCachedSprite("Markers/ms20000_marker.png");
                            else if (targetItemId == "63a0b2eabea67a6d93009e52") // radio repeater
                                mapDef.Sprite = TextureUtils.GetOrLoadCachedSprite("Markers/radiorepeater.png");
                            else if (targetItemId == "5447e0e74bdc2d3c308b4567") // signaljammer
                                mapDef.Sprite = TextureUtils.GetOrLoadCachedSprite("Markers/signaljammer.png");
                        }
                    }
                    else if (data.Condition.InnerCondition is ConditionVisitPlace)
                    {
                        mapDef.Sprite = TextureUtils.GetOrLoadCachedSprite("Markers/visitplace.png");
                    }
                    else if (data.Condition.InnerCondition is ConditionFindItem)
                    {
                        mapDef.Sprite = TextureUtils.GetOrLoadCachedSprite("Markers/pickup.png");
                    }

                    if (data.Condition.Parent is not null)
                    {
                        var killCondition = data.Condition.Parent.TemplateConditions.Conditions.FirstOrDefault(c => c is ConditionKills) as ConditionKills;
                        if (killCondition is not null)
                        {
                            // target Any == any == kill icon
                            // target AnyPmc == pmc == kill pmc icon
                            // target Usec == usec == kill usec icon
                            // Target Bear == bear == kill bear icon
                            // target savage + savageRole boss|follower == boss icon
                            // target savage + savageRole empty == scav icon
                            // target savage + savageRole exUsec == rogues
                            // target savage + savageRole marksman == snipers
                            // target savage + savageRole arenaFighterEvent == bloodhounds // raiders?
                            // target savage + savageRole pmcBot == raiders?
                            // target savage + savageRole sectant == cultists

                            mapDef.Sprite = TextureUtils.GetOrLoadCachedSprite("Markers/kill_any.png");

                            if (string.Equals(killCondition.target, "anypmc", System.StringComparison.OrdinalIgnoreCase))
                                mapDef.LayeredSprite = TextureUtils.GetOrLoadCachedSprite("Markers/kill_pmc.png");
                            else if (string.Equals(killCondition.target, "usec", System.StringComparison.OrdinalIgnoreCase))
                                mapDef.LayeredSprite = TextureUtils.GetOrLoadCachedSprite("Markers/kill_usec.png");
                            else if (string.Equals(killCondition.target, "bear", System.StringComparison.OrdinalIgnoreCase))
                                mapDef.LayeredSprite = TextureUtils.GetOrLoadCachedSprite("Markers/kill_bear.png");
                            else if (string.Equals(killCondition.target, "savage", System.StringComparison.OrdinalIgnoreCase))
                            {
                                if (killCondition.savageRole.Length == 0)
                                    mapDef.LayeredSprite = TextureUtils.GetOrLoadCachedSprite("Markers/kill_scav.png");
                                else if (killCondition.savageRole.Any(r => r.StartsWith("boss", System.StringComparison.OrdinalIgnoreCase) || r.StartsWith("follower", System.StringComparison.OrdinalIgnoreCase)))
                                    mapDef.LayeredSprite = TextureUtils.GetOrLoadCachedSprite("Markers/kill_boss.png");
                                else
                                    mapDef.LayeredSprite = TextureUtils.GetOrLoadCachedSprite("Markers/kill_special.png");
                            }
                        }
                    }
                }
            }

            return markers;
        }

        private static IEnumerable<(ConditionWrapper Condition, Vector3 Position, string ZoneId)> GetConditionData(Condition condition, string questName,
                                                                    string conditionDescription)
        {
            switch (condition)
            {
                case ConditionZone zoneCondition:
                    {
                        foreach (var position in GetPositionsForZoneId(zoneCondition.zoneId, questName, conditionDescription))
                        {
                            yield return (new(condition), position, zoneCondition.zoneId);
                        }
                        break;
                    }
                case ConditionLaunchFlare flareCondition:
                    {
                        foreach (var position in GetPositionsForZoneId(flareCondition.zoneID, questName, conditionDescription))
                        {
                            yield return (new(condition), position, flareCondition.zoneID);
                        }
                        break;
                    }
                case ConditionVisitPlace place:
                    {
                        foreach (var position in GetPositionsForZoneId(place.target, questName, conditionDescription))
                        {
                            yield return (new(condition), position, place.target);
                        }
                        break;
                    }
                case ConditionInZone zone:
                    {
                        foreach (var zoneId in zone.zoneIds)
                        {
                            foreach (var position in GetPositionsForZoneId(zoneId, questName, conditionDescription))
                            {
                                yield return (new(condition), position, zoneId);
                            }
                        }
                        break;
                    }
                case ConditionFindItem findItemCondition:
                    {
                        foreach (var position in GetPositionsForQuestItems(findItemCondition.target, questName, conditionDescription))
                        {
                            yield return (new(condition), position, null);
                        }
                        break;
                    }
                case ConditionExitName exitCondition:
                    {
                        var gameWorld = Singleton<GameWorld>.Instance;
                        if (gameWorld == null || gameWorld?.ExfiltrationController == null)
                        {
                            break;
                        }
                        else
                        {
                            var exfils = gameWorld.ExfiltrationController?.ExfiltrationPoints ?? [];
                            var specifiedExit = exfils.FirstOrDefault(e => e.Settings.Name == exitCondition.exitName);

                            if (specifiedExit != null)
                            {
                                yield return (new(condition), MathUtils.ConvertToMapPosition(specifiedExit.transform), null);
                            }

                            break;
                        }
                    }
                case ConditionCounterCreator conditionCreator:
                    {
                        // this will recurse back into this method
                        foreach (var position in GetPositionsForConditionCreator(conditionCreator, questName, conditionDescription))
                        {
                            yield return position;
                        }
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }

        private static IEnumerable<(ConditionWrapper, Vector3, string)> GetPositionsForConditionCreator(ConditionCounterCreator conditionCreator,
                                                                            string questName, string conditionDescription)
        {
            var counter = conditionCreator.TemplateConditions;
            foreach (var condition in counter.Conditions)
            {
                foreach (var position in GetConditionData(condition, questName, conditionDescription))
                {
                    position.Condition.Parent = conditionCreator;
                    yield return position;
                }
            }
        }

        private class ConditionWrapper
        {
            public Condition InnerCondition { get; }
            public ConditionCounterCreator Parent { get; set; }
            public ConditionWrapper(Condition condition)
            {
                InnerCondition = condition;
            }
        }

        private static IEnumerable<Vector3> GetPositionsForZoneId(string zoneId, string questName,
                                                                  string conditionDescription)
        {
            var zones = TriggersWithIds?.GetZoneTriggers(zoneId);
            foreach (var zone in zones)
            {
                yield return MathUtils.ConvertToMapPosition(zone.Position);
            }
        }

        private static IEnumerable<Vector3> GetPositionsForQuestItems(IEnumerable<string> questItemIds, string questName,
                                                                      string conditionDescription)
        {
            foreach (var questItemId in questItemIds)
            {
                var questItems = QuestItems?.Where(i => i.ItemId == questItemId);
                foreach (var item in questItems)
                {
                    yield return MathUtils.ConvertToMapPosition(item.Position);
                }
            }
        }

        private static IEnumerable<Condition> GetIncompleteQuestConditions(AbstractQuestControllerClass player, QuestDataClass quest)
        {
            // TODO: Template.Conditions is a GClass reference
            if (quest?.Template?.Conditions == null)
            {
                Plugin.Log.LogError($"GetIncompleteQuestConditions: quest.Template.Conditions is null, skipping quest");
                yield break;
            }

            // TODO: conditions is a GClass reference
            if (!quest.Template.Conditions.TryGetValue(EQuestStatus.AvailableForFinish, out var conditions) || conditions == null)
            {
                Plugin.Log.LogError($"Quest {quest.Template.NameLocaleKey.BSGLocalized()} doesn't have conditions marked AvailableForFinish, skipping it");
                yield break;
            }

            foreach (var condition in conditions)
            {
                if (condition == null)
                {
                    Plugin.Log.LogWarning($"Quest {quest.Template.NameLocaleKey.BSGLocalized()} has null condition, skipping it");
                    continue;
                }

                // filter out completed conditions
                if (IsConditionCompleted(player, quest, condition))
                {
                    continue;
                }

                yield return condition;
            }
        }

        private static IEnumerable<QuestDataClass> GetIncompleteQuests(AbstractQuestControllerClass questController)
        {
            var quests = questController.Quests;
            if (quests == null)
            {
                Plugin.Log.LogError($"Not able to get quests for player, quests is null");
                yield break;
            }

            var questsList = quests.List_1;
            if (questsList == null)
            {
                Plugin.Log.LogError($"Not able to get quests for player, questsList is null");
                yield break;
            }

            foreach (var quest in questsList)
            {
                if (quest?.Template?.Conditions == null)
                {
                    continue;
                }

                if (quest.Status != EQuestStatus.Started)
                {
                    continue;
                }

                yield return quest;
            }
        }

        private static bool IsConditionCompleted(AbstractQuestControllerClass questController, QuestDataClass questData, Condition condition)
        {
            // CompletedConditions is inaccurate (it doesn't reset when some quests do on death)
            // and also does not contain optional objectives, need to recheck if something is in there
            if (condition.IsNecessary && !questData.CompletedConditions.Contains(condition.id))
            {
                return false;
            }

            var quests = questController.Quests;
            if (quests == null)
            {
                return false;
            }

            var quest = quests.GetConditional(questData.Id);
            if (quest == null)
            {
                return false;
            }

            return quest.IsConditionDone(condition);
        }

        private static IEnumerable<TriggerWithIdAbstraction> GetZoneTriggers(this IEnumerable<TriggerWithIdAbstraction> triggerWithIds, string zoneId)
        {
            return triggerWithIds.Where(t => t.Id == zoneId);
        }

        private static MapMarkerDef CreateQuestMapMarkerDef(Vector3 position, string questName, string conditionDescription, TriggerWithIdAbstraction triggerWithIdAbstraction)
        {
            return new MapMarkerDef
            {
                Category = _questCategory,
                Color = _questColor,
                ImagePath = _questImagePath,
                Position = position,
                Pivot = _questPivot,
                Text = questName,
                ZoneTrigger = triggerWithIdAbstraction
            };
        }
    }
}
