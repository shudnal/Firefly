﻿using HarmonyLib;
using UnityEngine;
using System.Linq;
using static Firefly.Firefly;

namespace Firefly
{
    internal class FireflyItem
    {
        public const string itemName = "Firefly";
        public static int itemHash = itemName.GetStableHashCode();
        public const string itemDropName = "$item_firefly";
        public const string itemDropDescription = "$item_firefly_description";
        public const string statusEffectDescription = "$se_firefly_description";

        private static void CreateFireflyPrefab()
        {
            GameObject wispPrefab = ObjectDB.instance.GetItemPrefab("Wisp");
            if (wispPrefab == null)
                return;

            fireflyPrefab = InitPrefabClone(wispPrefab, itemName);

            Transform firefly_ball = fireflyPrefab.transform.Find("demister_ball");
            firefly_ball.name = "firefly_ball";
            firefly_ball.localScale *= 0.75f;

            MeshRenderer ballRenderer = firefly_ball.GetComponent<MeshRenderer>();
            ballRenderer.sharedMaterial = new Material(ballRenderer.sharedMaterial)
            {
                name = "firefly_ball"
            };
            ballRenderer.sharedMaterial.SetColor("_EmissionColor", new Color(1f, 0.62f, 0.48f));

            ParticleSystem.MainModule mainModule = fireflyPrefab.transform.Find("flare").GetComponent<ParticleSystem>().main;
            mainModule.startColor = new Color(1f, 0.5f, 0f, 0.5f);

            ParticleSystemRenderer ballSparcs = fireflyPrefab.transform.Find("sparcs_front").GetComponent<ParticleSystemRenderer>();
            
            ballSparcs.sharedMaterial = new Material(ballSparcs.sharedMaterial);
            ballSparcs.sharedMaterial.name = "firefly_sparcs";
            ballSparcs.sharedMaterial.SetColor("_EmissionColor", new Color(1f, 0.62f, 0.48f));

            fireflyPrefab.transform.Find("Point light").GetComponent<Light>().color = new Color(1f, 0.5f, 0f, 1f);

            ItemDrop fireflyItem = fireflyPrefab.GetComponent<ItemDrop>();
            fireflyItem.m_itemData.m_dropPrefab = fireflyPrefab;

            fireflyItem.m_itemData.m_shared.m_icons[0] = itemIcon;
            fireflyItem.m_itemData.m_shared.m_name = itemDropName;
            fireflyItem.m_itemData.m_shared.m_description = itemDropDescription;
            fireflyItem.m_itemData.m_shared.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
            fireflyItem.m_itemData.m_shared.m_consumeStatusEffect = ObjectDB.instance.GetStatusEffect(SE_Firefly.statusEffectHash);
            fireflyItem.m_itemData.m_shared.m_maxStackSize = itemStackSize.Value;
            fireflyItem.m_itemData.m_shared.m_maxQuality = 1;

            LogInfo($"Created prefab {fireflyPrefab.name}");
        }

        private static void RegisterFireflyPrefab()
        {
            ClearPrefabReferences();

            CreateFireflyPrefab();
            if (!(bool)fireflyPrefab)
                return;

            if (ObjectDB.instance && !ObjectDB.instance.m_itemByHash.ContainsKey(itemHash))
            {
                ObjectDB.instance.m_items.Add(fireflyPrefab);
                ObjectDB.instance.m_itemByHash.Add(itemHash, fireflyPrefab);
            }

            if (ZNetScene.instance && !ZNetScene.instance.m_namedPrefabs.ContainsKey(itemHash))
            {
                ZNetScene.instance.m_prefabs.Add(fireflyPrefab);
                ZNetScene.instance.m_namedPrefabs.Add(itemHash, fireflyPrefab);
            }

            if (ObjectDB.instance)
            {
                if (ObjectDB.instance.m_recipes.RemoveAll(x => x.name == itemName) > 0)
                    LogInfo($"Removed recipe {itemName}");

                CraftingStation station = string.IsNullOrWhiteSpace(itemCraftingStation.Value) ? null : ObjectDB.instance.m_recipes.FirstOrDefault(rec => rec.m_craftingStation?.m_name == itemCraftingStation.Value)?.m_craftingStation;

                ItemDrop item = fireflyPrefab.GetComponent<ItemDrop>();

                Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
                recipe.name = itemName;
                recipe.m_amount = 1;
                recipe.m_minStationLevel = itemMinStationLevel.Value;
                recipe.m_item = item;
                recipe.m_enabled = true;

                if (station != null)
                    recipe.m_craftingStation = station;

                recipe.m_resources = new Piece.Requirement[3] 
                {
                    new Piece.Requirement()
                    {
                        m_amount = 1,
                        m_resItem = ObjectDB.instance.GetItemPrefab("Dandelion").GetComponent<ItemDrop>(),
                    },
                    new Piece.Requirement()
                    {
                        m_amount = 1,
                        m_resItem = ObjectDB.instance.GetItemPrefab("GreydwarfEye").GetComponent<ItemDrop>(),
                    },
                    new Piece.Requirement()
                    {
                        m_amount = 2,
                        m_resItem = ObjectDB.instance.GetItemPrefab("Resin").GetComponent<ItemDrop>(),
                    },
                };

                ObjectDB.instance.m_recipes.Add(recipe);
            }
        }

        private static void ClearPrefabReferences()
        {
            if (ObjectDB.instance && ObjectDB.instance.m_itemByHash.ContainsKey(itemHash))
            {
                ObjectDB.instance.m_items.Remove(ObjectDB.instance.m_itemByHash[itemHash]);
                ObjectDB.instance.m_itemByHash.Remove(itemHash);
            }

            if (ZNetScene.instance && ZNetScene.instance.m_namedPrefabs.ContainsKey(itemHash))
            {
                ZNetScene.instance.m_prefabs.Remove(ZNetScene.instance.m_namedPrefabs[itemHash]);
                ZNetScene.instance.m_namedPrefabs.Remove(itemHash);
            }
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        public static class ObjectDB_Awake_AddPrefab
        {
            private static void Postfix()
            {
                RegisterFireflyPrefab();
            }
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        public static class ObjectDB_CopyOtherDB_AddPrefab
        {
            private static void Postfix()
            {
                RegisterFireflyPrefab();
            }
        }

        [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.OnDestroy))]
        public static class FejdStartup_OnDestroy_AddPrefab
        {
            private static void Prefix()
            {
                ClearPrefabReferences();
            }
        }

        [HarmonyPatch(typeof(Localization), nameof(Localization.SetupLanguage))]
        public static class Localization_SetupLanguage_AddLocalizedWords
        {
            private static void Postfix(Localization __instance, string language, bool __result)
            {
                if (!__result)
                    return;

                __instance.AddWord("item_firefly", GetItemName(language));
                __instance.AddWord("item_firefly_description", GetItemDescription(language));
                __instance.AddWord("se_firefly_description", GetStatusEffectTooltip(language));
            }

            private static string GetItemName(string language)
            {
                return language switch
                {
                    "Russian" => "Светлячок",
                    "Chinese" => "萤火虫",
                    "Chinese_Trad" => "螢火蟲",
                    "French" => "Luciole",
                    "German" => "Glühwürmchen",
                    "Polish" => "Świetlik",
                    "Korean" => "반딧불이",
                    "Spanish" => "Luciérnaga",
                    "Turkish" => "Ateşböceği",
                    "Dutch" => "Glimworm",
                    "Portuguese_Brazilian" => "Vaga-lume",
                    "Japanese" => "ホタル",
                    "Ukrainian" => "Світлячок",
                    _ => "Firefly"
                };
            }

            private static string GetItemDescription(string language)
            {
                return language switch
                {
                    "Russian" => "Светлячок, который проведет вас через самые темные ночи",
                    "Chinese" => "一只被束缚的萤火虫，引导你度过最黑暗的夜晚",
                    "Chinese_Trad" => "一隻被束縛的螢火蟲，引導你度過最黑暗的夜晚",
                    "French" => "Une luciole liée pour vous guider à travers les nuits les plus sombres",
                    "German" => "Ein gebundenes Glühwürmchen, das Sie durch die dunkelste Nacht führt",
                    "Polish" => "Związany świetlik, który poprowadzi Cię przez najciemniejsze noce",
                    "Korean" => "가장 어두운 밤을 안내할 묶인 반딧불이",
                    "Spanish" => "Una luciérnaga atada que te guiará a través de las noches más oscuras.",
                    "Turkish" => "En karanlık gecelerde size rehberlik edecek bağlı bir ateş böceği",
                    "Dutch" => "Een gebonden vuurvliegje om je door de donkerste nachten te leiden",
                    "Portuguese_Brazilian" => "Um vaga-lume preso para guiá-lo nas noites mais escuras",
                    "Japanese" => "縛られたホタルがあなたを最も暗い夜へと導きます",
                    "Ukrainian" => "Прив’язаний світлячок проведе вас у найтемніші ночі",
                    _ => "A bound firefly to guide you through the darkest of nights"
                };
            }

            private static string GetStatusEffectTooltip(string language)
            {
                return language switch
                {
                    "Russian" => "Выпустите светлячка, который будет сопровождать вас некоторое время",
                    "Chinese" => "释放一只会陪伴你一段时间的萤火虫",
                    "Chinese_Trad" => "釋放一隻會陪伴你一段時間的螢火蟲",
                    "French" => "Libérez une luciole qui vous accompagnera pendant quelques temps",
                    "German" => "Lassen Sie ein Glühwürmchen los, das Sie einige Zeit begleiten wird",
                    "Polish" => "Wypuść świetlika, a będzie Ci towarzyszył przez jakiś czas",
                    "Korean" => "한동안 동행할 반딧불을 풀어주세요",
                    "Spanish" => "Suelta una luciérnaga que te acompañará durante un tiempo",
                    "Turkish" => "Bir süre size eşlik edecek bir ateş böceğini serbest bırakın",
                    "Dutch" => "Laat een vuurvlieg los die je een tijdje zal vergezellen",
                    "Portuguese_Brazilian" => "Solte um vaga-lume que irá te acompanhar por algum tempo",
                    "Japanese" => "しばらくあなたに同行するホタルを放ちます",
                    "Ukrainian" => "Випустіть світлячка, який буде супроводжувати вас деякий час",
                    _ => "Release a firefly that will accompany you for some time"
                };
            }

        }

    }
}
