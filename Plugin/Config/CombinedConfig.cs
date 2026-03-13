using DynamicMaps.Common;
using DynamicMaps.Config;

namespace DynamicMaps.UI
{
    internal class CombinedConfig(ModConfig ServerConfig)
    {
        public bool ShowPlayerMarker => ServerConfig.AllowShowPlayerMarker && Settings.ShowPlayerMarker.Value;
        public bool ShowFriendlyPlayerMarkersInRaid => ServerConfig.AllowShowFriendlyPlayerMarkersInRaid && Settings.ShowFriendlyPlayerMarkersInRaid.Value;
        public bool ShowEnemyPlayerMarkersInRaid => ServerConfig.AllowShowEnemyPlayerMarkersInRaid && Settings.ShowEnemyPlayerMarkersInRaid.Value;
        public bool ShowScavMarkersInRaid => ServerConfig.AllowShowScavMarkersInRaid && Settings.ShowScavMarkersInRaid.Value;
        public bool ShowBossMarkersInRaid => ServerConfig.AllowShowBossMarkersInRaid && Settings.ShowBossMarkersInRaid.Value;
        public bool ShowLockedDoorStatus => ServerConfig.AllowShowLockedDoorStatus && Settings.ShowLockedDoorStatus.Value;
        public bool ShowQuestsInRaid => ServerConfig.AllowShowQuestsInRaid && Settings.ShowQuestsInRaid.Value;
        public bool ShowExtractsInRaid => ServerConfig.AllowShowExtractsInRaid && Settings.ShowExtractsInRaid.Value;
        public bool ShowExtractsStatusInRaid => ServerConfig.AllowShowExtractStatusInRaid && Settings.ShowExtractStatusInRaid.Value;
        public bool ShowTransitPointsInRaid => ServerConfig.AllowShowTransitPointsInRaid && Settings.ShowTransitPointsInRaid.Value;
        public bool ShowSecretExtractsInRaid => ServerConfig.AllowShowSecretExtractsInRaid && Settings.ShowSecretPointsInRaid.Value;
        public bool ShowDroppedBackpackInRaid => ServerConfig.AllowShowDroppedBackpackInRaid && Settings.ShowDroppedBackpackInRaid.Value;
        public bool ShowWishlistedItemsInRaid => ServerConfig.AllowShowWishlistedItemsInRaid && Settings.ShowWishListItemsInRaid.Value;
        public bool ShowBTRInRaid => ServerConfig.AllowShowBTRInRaid && Settings.ShowBTRInRaid.Value;
        public bool ShowAirdropsInRaid => ServerConfig.AllowShowAirdropsInRaid && Settings.ShowAirdropsInRaid.Value;
        public bool ShowHiddenStashesInRaid => ServerConfig.AllowShowHiddenStashesInRaid && Settings.ShowHiddenStashesInRaid.Value;
        public bool ShowFriendlyCorpses => ServerConfig.AllowShowFriendlyCorpses && Settings.ShowFriendlyCorpsesInRaid.Value;
        public bool ShowKilledCorpses => ServerConfig.AllowShowKilledCorpses && Settings.ShowKilledCorpsesInRaid.Value;
        public bool ShowFriendlyKilledCorpses => ServerConfig.AllowShowFriendlyKilledCorpses && Settings.ShowFriendlyKilledCorpsesInRaid.Value;
        public bool ShowBossCorpses => ServerConfig.AllowShowBossCorpses && Settings.ShowBossCorpsesInRaid.Value;
        public bool ShowOtherCorpses => ServerConfig.AllowShowOtherCorpses && Settings.ShowOtherCorpsesInRaid.Value;
        public bool ShowHeliCrashSiteInRaid => ServerConfig.AllowShowHeliCrashSiteInRaid && Settings.ShowHeliCrashMarker.Value;
        public bool AllowMiniMap => ServerConfig.AllowMiniMap && Settings.MiniMapEnabled.Value;
        public bool RequireMapInInventory => ServerConfig.RequireMapInInventory || Settings.RequireMapInInventory.Value;
        public int ShowScavIntelLevel => ServerConfig.ShowScavIntelLevel > Settings.ShowScavIntelLevel.Value ? ServerConfig.ShowScavIntelLevel : Settings.ShowScavIntelLevel.Value;
        public int ShowPmcIntelLevel => ServerConfig.ShowPmcIntelLevel > Settings.ShowPmcIntelLevel.Value ? ServerConfig.ShowPmcIntelLevel : Settings.ShowPmcIntelLevel.Value;
        public int ShowBossIntelLevel => ServerConfig.ShowBossIntelLevel > Settings.ShowBossIntelLevel.Value ? ServerConfig.ShowBossIntelLevel : Settings.ShowBossIntelLevel.Value;
        public int ShowFriendlyIntelLevel => ServerConfig.ShowFriendlyIntelLevel > Settings.ShowFriendlyIntelLevel.Value ? ServerConfig.ShowFriendlyIntelLevel : Settings.ShowFriendlyIntelLevel.Value;
        public int ShowAirdropIntelLevel => ServerConfig.ShowAirDropIntelLevel > Settings.ShowAirdropIntelLevel.Value ? ServerConfig.ShowAirDropIntelLevel : Settings.ShowAirdropIntelLevel.Value;
        public int ShowCorpseIntelLevel => ServerConfig.ShowCorpseIntelLevel > Settings.ShowCorpseIntelLevel.Value ? ServerConfig.ShowCorpseIntelLevel : Settings.ShowCorpseIntelLevel.Value;
        public int ShowWishListItemsIntelLevel => ServerConfig.ShowWishListIntelLevel > Settings.ShowWishListItemsIntelLevel.Value ? ServerConfig.ShowWishListIntelLevel : Settings.ShowWishListItemsIntelLevel.Value;
        public int ShowHiddenStashIntelLevel => ServerConfig.ShowHiddenStashIntelLevel > Settings.ShowHiddenStashIntelLevel.Value ? ServerConfig.ShowHiddenStashIntelLevel : Settings.ShowHiddenStashIntelLevel.Value;
    }
}
