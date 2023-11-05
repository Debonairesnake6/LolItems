using System.Threading;
using System.Globalization;
using System.Collections.Generic;
using BepInEx;
using R2API;
using R2API.Utils;
using RoR2.Orbs;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System;
using BepInEx.Configuration;

namespace LoLItems
{
    internal class InfinityEdge
    {
        public static ItemDef myItemDef;

        public static ConfigEntry<float> bonusCritChance { get; set; }
        public static ConfigEntry<float> bonusCritDamage { get; set; }
        public static ConfigEntry<bool> enabled { get; set; }
        public static ConfigEntry<string> rarity { get; set; }
        public static ConfigEntry<string> voidItems { get; set; }
        public static Dictionary<UnityEngine.Networking.NetworkInstanceId, float> bonusDamageDealt = new Dictionary<UnityEngine.Networking.NetworkInstanceId, float>();
        public static Dictionary<RoR2.UI.ItemInventoryDisplay, CharacterMaster> DisplayToMasterRef = new Dictionary<RoR2.UI.ItemInventoryDisplay, CharacterMaster>();
        public static Dictionary<RoR2.UI.ItemIcon, CharacterMaster> IconToMasterRef = new Dictionary<RoR2.UI.ItemIcon, CharacterMaster>();

        internal static void Init()
        {
            LoadConfig();
            if (!enabled.Value)
            {
                return;
            }

            CreateItem();
            AddTokens();
            var displayRules = new ItemDisplayRuleDict(null);
            ItemAPI.Add(new CustomItem(myItemDef, displayRules));
            hooks();
            Utilities.SetupReadOnlyHooks(DisplayToMasterRef, IconToMasterRef, myItemDef, GetDisplayInformation, rarity, voidItems, "InfinityEdge");
        }

        private static void LoadConfig()
        {
            enabled = LoLItems.MyConfig.Bind<bool>(
                "InfinityEdge",
                "Enabled",
                true,
                "Determines if the item should be loaded by the game."
            );

            rarity = LoLItems.MyConfig.Bind<string>(
                "InfinityEdge",
                "Rarity",
                "Tier2Def",
                "Set the rarity of the item. Valid values: Tier1Def, Tier2Def, Tier3Def, VoidTier1Def, VoidTier2Def, and VoidTier3Def."
            );

            voidItems = LoLItems.MyConfig.Bind<string>(
                "InfinityEdge",
                "Void Items",
                "",
                "Set regular items to convert into this void item (Only if the rarity is set as a void tier). Items should be separated by a comma, no spaces. The item should be the in game item ID, which may differ from the item name."
            );

            bonusCritChance = LoLItems.MyConfig.Bind<float>(
                "InfinityEdge",
                "Crit Chance",
                5f,
                "Amount of crit chance each item will grant."

            );

            bonusCritDamage = LoLItems.MyConfig.Bind<float>(
                "InfinityEdge",
                "Crit Damage",
                15f,
                "Amount of crit damage each item will grant."

            );
        }

        private static void CreateItem()
        {
            myItemDef = ScriptableObject.CreateInstance<ItemDef>();
            myItemDef.name = "InfinityEdge";
            myItemDef.nameToken = "InfinityEdge";
            myItemDef.pickupToken = "InfinityEdgeItem";
            myItemDef.descriptionToken = "InfinityEdgeDesc";
            myItemDef.loreToken = "InfinityEdgeLore";
#pragma warning disable Publicizer001 // Accessing a member that was not originally public. Here we ignore this warning because with how this example is setup we are forced to do this
            myItemDef._itemTierDef = Addressables.LoadAssetAsync<ItemTierDef>(Utilities.GetRarityFromString(rarity.Value)).WaitForCompletion();
#pragma warning restore Publicizer001
            myItemDef.pickupIconSprite = Assets.icons.LoadAsset<Sprite>("InfinityEdgeIcon");
            myItemDef.pickupModelPrefab = Assets.prefabs.LoadAsset<GameObject>("InfinityEdgePrefab");
            myItemDef.canRemove = true;
            myItemDef.hidden = false;
        }

        private static void hooks()
        {            
            // Modify character values
            On.RoR2.CharacterBody.RecalculateStats += (orig, self) =>
            {
                orig(self);
                if (self?.inventory && self.inventory.GetItemCount(myItemDef.itemIndex) > 0)
                {
                    float itemCount = self.inventory.GetItemCount(myItemDef.itemIndex);
                    self.critMultiplier += itemCount * bonusCritDamage.Value * 0.01f;
                    if (self.inventory.GetItemCount(DLC1Content.Items.ConvertCritChanceToCritDamage) == 0)
                    {
                        self.crit += itemCount * bonusCritChance.Value;
                    }
                    else
                    {
                        self.critMultiplier += itemCount * bonusCritChance.Value * 0.01f;
                    }
                }
            };

            // When something takes damage
            On.RoR2.HealthComponent.TakeDamage += (orig, self, damageInfo) =>
            {
                orig(self, damageInfo);
                if (damageInfo.attacker && damageInfo.crit)
                {
                    CharacterBody attackerCharacterBody = damageInfo.attacker.GetComponent<CharacterBody>();
                    
                    if (attackerCharacterBody?.inventory)
                    {
                        int inventoryCount = attackerCharacterBody.inventory.GetItemCount(myItemDef.itemIndex);
                        if (inventoryCount > 0)
                        {
                            float damageDealt = damageInfo.damage * attackerCharacterBody.critMultiplier * (inventoryCount * bonusCritDamage.Value * 0.01f / attackerCharacterBody.critMultiplier);
                            Utilities.AddValueInDictionary(ref bonusDamageDealt, attackerCharacterBody.master, damageDealt);
                        }
                    }
                }
            };
        }

        private static (string, string) GetDisplayInformation(CharacterMaster masterRef)
        {
            if (masterRef == null)
                return (Language.GetString(myItemDef.descriptionToken), "");
            
            string customDescription = "";
            int itemCount = masterRef.inventory.GetItemCount(myItemDef.itemIndex);
            if (masterRef.inventory.GetItemCount(DLC1Content.Items.ConvertCritChanceToCritDamage) == 0){
                customDescription += "<br><br>Bonus crit chance: " + String.Format("{0:#}", itemCount * bonusCritChance.Value) + "%"
                + "<br>Bonus crit damage: " + String.Format("{0:#}", itemCount * bonusCritDamage.Value);
            }
            else
            {
                customDescription += "<br><br>Bonus crit chance: 0%"
                + "<br>Bonus crit damage: " + String.Format("{0:#}", itemCount * bonusCritDamage.Value + itemCount * bonusCritChance.Value);
            }
            

            if (bonusDamageDealt.TryGetValue(masterRef.netId, out float damageDealt))
                customDescription += "<br>Bonus damage dealt: " + String.Format("{0:#}", damageDealt);
            else
                customDescription += "<br>Bonus damage dealt: 0";

            return (Language.GetString(myItemDef.descriptionToken), customDescription);
        }

        //This function adds the tokens from the item using LanguageAPI, the comments in here are a style guide, but is very opiniated. Make your own judgements!
        private static void AddTokens()
        {
            // Name of the item
            LanguageAPI.Add("InfinityEdge", "InfinityEdge");

            // Short description
            LanguageAPI.Add("InfinityEdgeItem", "Gain crit chance and crit damage");

            // Long description
            LanguageAPI.Add("InfinityEdgeDesc", "Gain <style=cIsUtility>" + bonusCritChance.Value + "%</style> <style=cStack>(+" + bonusCritChance.Value + ")</style> crit chance and <style=cIsDamage>" + bonusCritDamage.Value + "%</style> <style=cStack>(+" + bonusCritDamage.Value + ")</style> crit damage");

            // Lore
            LanguageAPI.Add("InfinityEdgeLore", "For when enemies need to die");
        }
    }
}