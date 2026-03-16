using Comfort.Common;
using DynamicMaps.Common;
using DynamicMaps.Data;
using EFT;
using EFT.Interactive;
using EFT.Quests;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        private static readonly Dictionary<string, Task<Sprite>> _asyncTraderIcons = [];
        private static Color _questColor = Color.green;
        private static List<LootItemAbstraction> _questItems;
        private static Vector2 _questPivot = new(0.5f, 0f);

        private static List<TriggerWithIdAbstraction> _triggersWithIds;

        internal static void DiscardQuestData()
        {
            _triggersWithIds?.Clear();
            _triggersWithIds = null;
            _questItems?.Clear();
            _questItems = null;
        }

        internal static void FillQuestDataOutOfRaid(List<ConditionData> data, MapDef def)
        {
            _questItems ??= [.. data.Select(d => new LootItemAbstraction()
                {
                    ItemId = d.ItemId,
                    Position = new Vector3(d.SpawnPoint[0], d.SpawnPoint[1], d.SpawnPoint[2])
                })];

            _triggersWithIds ??= [.. def.TriggersWithId];
        }

        internal static IEnumerable<MapMarkerDef> GetMarkerDefsForPlayer(AbstractQuestControllerClass questController)
        {
            if (_triggersWithIds == null || _questItems == null || questController == null)
            {
                Plugin.Log.LogWarning($"TriggersWithIds null: {_triggersWithIds == null} or QuestItems null: {_questItems == null} or Player null: {questController == null}");
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

        private static IEnumerable<MapMarkerDef> GetMarkerDefsForQuest(AbstractQuestControllerClass questController, QuestDataClass quest)
        {
            var markers = new List<MapMarkerDef>();
            if (!_asyncTraderIcons.TryGetValue(quest.Template.TraderId, out var traderAvatar))
            {
                Singleton<BackendConfigSettingsClass>.Instance
                    .TradersSettings.TryGetValue(quest.Template.TraderId, out var trader);

                _asyncTraderIcons[quest.Template.TraderId] = traderAvatar = trader?.GetAvatar();
            }

            var conditions = GetIncompleteQuestConditions(questController, quest);
            foreach (var condition in conditions)
            {
                var questName = quest.Template.NameLocaleKey.BSGLocalized();
                var conditionDescription = condition.id.BSGLocalized();

                var conditionData = GetConditionData(condition);
                foreach (var data in conditionData)
                {
                    var isDuplicate = false;

                    // check against previously created markers for duplicate position
                    foreach (var marker in markers)
                    {
                        if (marker.Position.ApproxEquals(data.Position))
                        {
                            Plugin.Log.LogInfo($"Duplicate marker for quest {questName} condition {conditionDescription} at position {data.Position}, duplicate of {marker.Text}");
                            isDuplicate = true;
                            break;
                        }
                    }

                    if (isDuplicate)
                    {
                        continue;
                    }

                    markers.Add(CreateQuestMapMarkerDef(data, questName, traderAvatar));
                }
            }

            return markers;
        }

        internal static void TryCaptureQuestData(MapDef def)
        {
            var gameWorld = Singleton<GameWorld>.Instance;

            if (_triggersWithIds == null)
            {
                if (gameWorld is not null)
                {
                    _triggersWithIds = [.. GameObject.FindObjectsOfType<TriggerWithId>().Select(k => {
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
                else
                {
                    _triggersWithIds = [.. def.TriggersWithId];
                }
            }

            _questItems ??= [.. Traverse.Create(gameWorld)
                    .Field("LootItems")
                    .Field("List_0")
                    .GetValue<List<LootItem>>()
                    .Where(i => i.Item.QuestItem).Select(k => new LootItemAbstraction()
                    {
                        ItemId = k.TemplateId,
                        Position = k.transform.position
                    })];
        }

        private static MapMarkerDef CreateQuestMapMarkerDef(ConditionWrapper data, string questName, Task<Sprite> traderAvatar)
        {
            return new MapMarkerDef
            {
                Category = _questCategory,
                Color = _questColor,
                ImagePath = _questImagePath,
                Sprite = data.Icon,
                LayeredSprite = data.LayeredIcon,
                Position = data.Position,
                Pivot = _questPivot,
                Text = questName,
                ZoneTrigger = data.ZoneTrigger,
                LabelSprite = traderAvatar
            };
        }

        private static TriggerWithIdAbstraction GetAssociatedZoneTrigger(ConditionWrapper data)
        {
            return _triggersWithIds.FirstOrDefault(f => f.Id == data.ZoneId
                && (_triggersWithIds.Count(z => z.Id == f.Id) == 1 || MathUtils.ConvertToMapPosition(f.Position).ApproxEquals(data.Position, 1)));
        }

        private static IEnumerable<ConditionWrapper> GetConditionData(Condition condition)
        {
            Sprite icon = null;
            Sprite layeredIcon = null;
            switch (condition)
            {
                case ConditionZone zoneCondition:
                    {
                        var targetItemId = zoneCondition.target.FirstOrDefault();

                        foreach (var position in GetPositionsForZoneId(zoneCondition.zoneId))
                        {
                            if (zoneCondition is ConditionLeaveItemAtLocation)
                            {
                                if (targetItemId == "5b4391a586f7745321235ab2") // wifi camera
                                    icon = TextureUtils.GetOrLoadCachedSprite("Markers/camera.png");
                                else
                                    icon = TextureUtils.GetOrLoadCachedSprite("Markers/deposit.png");
                            }
                            else if (zoneCondition is ConditionPlaceBeacon)
                            {
                                if (targetItemId == "5991b51486f77447b112d44f") // ms2000 marker
                                    icon = TextureUtils.GetOrLoadCachedSprite("Markers/ms20000_marker.png");
                                else if (targetItemId == "63a0b2eabea67a6d93009e52") // radio repeater
                                    icon = TextureUtils.GetOrLoadCachedSprite("Markers/radiorepeater.png"); // todo: make/find icon for this
                                else if (targetItemId == "5447e0e74bdc2d3c308b4567") // signaljammer
                                    icon = TextureUtils.GetOrLoadCachedSprite("Markers/signaljammer.png");
                            }

                            yield return new(condition, position, zoneCondition.zoneId, icon, layeredIcon);
                        }
                        break;
                    }
                case ConditionLaunchFlare flareCondition:
                    {
                        icon = TextureUtils.GetOrLoadCachedSprite("Markers/flare.png");
                        foreach (var position in GetPositionsForZoneId(flareCondition.zoneID))
                        {
                            yield return new(condition, position, flareCondition.zoneID, null, null);
                        }
                        break;
                    }
                case ConditionVisitPlace place:
                    {
                        icon = TextureUtils.GetOrLoadCachedSprite("Markers/visitplace.png");
                        foreach (var position in GetPositionsForZoneId(place.target))
                        {
                            yield return new(condition, position, place.target, icon, layeredIcon);
                        }
                        break;
                    }
                case ConditionInZone zone:
                    {
                        foreach (var zoneId in zone.zoneIds)
                        {
                            foreach (var position in GetPositionsForZoneId(zoneId))
                            {
                                yield return new(condition, position, zoneId, icon, layeredIcon);
                            }
                        }
                        break;
                    }
                case ConditionFindItem findItemCondition:
                    {
                        icon = TextureUtils.GetOrLoadCachedSprite("Markers/pickup.png");
                        foreach (var position in GetPositionsForQuestItems(findItemCondition.target))
                        {
                            yield return new(condition, position, string.Empty, icon, layeredIcon);
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
                                yield return new(condition, MathUtils.ConvertToMapPosition(specifiedExit.transform), string.Empty, icon, layeredIcon);
                            }

                            break;
                        }
                    }
                case ConditionCounterCreator conditionCreator:
                    {
                        // this will recurse back into this method
                        foreach (var position in GetPositionsForConditionCreator(conditionCreator))
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

        private static IEnumerable<ConditionWrapper> GetPositionsForConditionCreator(ConditionCounterCreator conditionCreator)
        {
            Sprite icon = null;
            Sprite layeredIcon = null;

            var counter = conditionCreator.TemplateConditions;
            var killCondition = counter.Conditions.FirstOrDefault(c => c is ConditionKills) as ConditionKills;
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

                icon = TextureUtils.GetOrLoadCachedSprite("Markers/kill_any.png");

                if (string.Equals(killCondition.target, "anypmc", System.StringComparison.OrdinalIgnoreCase))
                    layeredIcon = TextureUtils.GetOrLoadCachedSprite("Markers/kill_pmc.png");
                else if (string.Equals(killCondition.target, "usec", System.StringComparison.OrdinalIgnoreCase))
                    layeredIcon = TextureUtils.GetOrLoadCachedSprite("Markers/kill_usec.png");
                else if (string.Equals(killCondition.target, "bear", System.StringComparison.OrdinalIgnoreCase))
                    layeredIcon = TextureUtils.GetOrLoadCachedSprite("Markers/kill_bear.png");
                else if (string.Equals(killCondition.target, "savage", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (killCondition.savageRole.Length == 0)
                        layeredIcon = TextureUtils.GetOrLoadCachedSprite("Markers/kill_scav.png");
                    else if (killCondition.savageRole.Any(r => r.StartsWith("boss", System.StringComparison.OrdinalIgnoreCase) || r.StartsWith("follower", System.StringComparison.OrdinalIgnoreCase)))
                        layeredIcon = TextureUtils.GetOrLoadCachedSprite("Markers/kill_boss.png");
                    else
                        layeredIcon = TextureUtils.GetOrLoadCachedSprite("Markers/kill_special.png");
                }
            }
            foreach (var condition in counter.Conditions)
            {
                foreach (var position in GetConditionData(condition))
                {
                    position.SetIcons(icon, layeredIcon);
                    yield return position;
                }
            }
        }

        private static IEnumerable<Vector3> GetPositionsForQuestItems(IEnumerable<string> questItemIds)
        {
            foreach (var questItemId in questItemIds)
            {
                var questItems = _questItems?.Where(i => i.ItemId == questItemId);
                foreach (var item in questItems)
                {
                    yield return MathUtils.ConvertToMapPosition(item.Position);
                }
            }
        }

        private static IEnumerable<Vector3> GetPositionsForZoneId(string zoneId)
        {
            var zones = _triggersWithIds?.GetZoneTriggers(zoneId);
            foreach (var zone in zones)
            {
                yield return MathUtils.ConvertToMapPosition(zone.Position);
            }
        }

        private static IEnumerable<TriggerWithIdAbstraction> GetZoneTriggers(this IEnumerable<TriggerWithIdAbstraction> triggerWithIds, string zoneId)
        {
            return triggerWithIds.Where(t => t.Id == zoneId);
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

        internal class LootItemAbstraction
        {
            public string ItemId { get; set; }
            public Vector3 Position { get; set; }
        }

        private class ConditionWrapper
        {
            public ConditionWrapper(Condition condition, Vector3 position, string zoneId, Sprite icon, Sprite layeredIcon)
            {
                InnerCondition = condition;
                Position = position;
                ZoneId = zoneId;
                Icon = icon;
                LayeredIcon = layeredIcon;
                if (!string.IsNullOrEmpty(zoneId))
                    ZoneTrigger = GetAssociatedZoneTrigger(this);
            }

            public Sprite Icon { get; private set; }
            public Condition InnerCondition { get; }
            public Sprite LayeredIcon { get; private set; }
            public Vector3 Position { get; }
            public string ZoneId { get; }
            public TriggerWithIdAbstraction ZoneTrigger { get; }

            public void SetIcons(Sprite icon, Sprite layeredIcon)
            {
                Icon ??= icon;
                LayeredIcon ??= layeredIcon;
            }
        }
    }
}