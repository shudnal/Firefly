﻿using HarmonyLib;
using UnityEngine;
using static Firefly.Firefly;

namespace Firefly
{
    internal class FireflyItem
    {
        public const string itemName = "Firefly";
        public static int itemHash = itemName.GetStableHashCode();
        public const string itemDropName = "$item_firefly";
        public const string itemDropDescription = "$item_firefly_description";

        private static void CreateFireflyPrefab()
        {
            if ((bool)fireflyPrefab)
                return;

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
            fireflyItem.m_itemData.m_shared.m_description = "$npc_dvergrrogue_random_goodbye5";//itemDropDescription;
            fireflyItem.m_itemData.m_shared.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
            fireflyItem.m_itemData.m_shared.m_consumeStatusEffect = ObjectDB.instance.GetStatusEffect(SE_Firefly.statusEffectHash);
            fireflyItem.m_itemData.m_shared.m_maxStackSize = 20;

            LogInfo($"Created prefab {fireflyPrefab.name}");
        }

        private static void RegisterFireflyPrefab()
        {
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
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        public static class ObjectDB_Awake_AddPrefab
        {
            private static void Postfix(ObjectDB __instance)
            {
                RegisterFireflyPrefab();
            }
        }

        [HarmonyPatch(typeof(Localization), nameof(Localization.SetupLanguage))]
        public static class Localization_SetupLanguage_AddLocalizedWords
        {
            private static void Postfix(Localization __instance, string language)
            {
                __instance.AddWord("item_firefly", GetItemName(language));
                __instance.AddWord("item_firefly_description", GetItemDescription(language));
            }

            private static string GetItemName(string language)
            {
                return language switch
                {
                    "English" => "Firefly",
                    "Russian" => "Светлячок",
                    _ => "Firefly"
                };
            }
            private static string GetItemDescription(string language)
            {
                return language switch
                {
                    "English" => "A bound firefly to guide you through the darkest of nights.",
                    "Russian" => "Светлячок, который проведет вас через самые темные ночи",
                    _ => "A bound firefly to guide you through the darkest of nights."
                };
            }
        }

    }
}
