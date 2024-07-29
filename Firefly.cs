using HarmonyLib;
using BepInEx;
using ServerSync;
using BepInEx.Configuration;
using UnityEngine;
using System.IO;
using System.Reflection;
using System.Linq;

namespace Firefly
{
    [BepInPlugin(pluginID, pluginName, pluginVersion)]
    public class Firefly : BaseUnityPlugin
    {
        const string pluginID = "shudnal.Firefly";
        const string pluginName = "Firefly";
        const string pluginVersion = "1.0.7";

        private readonly Harmony harmony = new Harmony(pluginID);

        internal static readonly ConfigSync configSync = new ConfigSync(pluginID) { DisplayName = pluginName, CurrentVersion = pluginVersion, MinimumRequiredVersion = pluginVersion };

        internal static Firefly instance;

        private static ConfigEntry<bool> configLocked;
        private static ConfigEntry<bool> loggingEnabled;

        public static ConfigEntry<int> itemStackSize;
        public static ConfigEntry<string> itemCraftingStation;
        public static ConfigEntry<int> itemMinStationLevel;
        public static ConfigEntry<int> statusEffectDuration;
        public static ConfigEntry<string> itemRecipe;
        public static ConfigEntry<float> itemWeight;

        public static ConfigEntry<Color> lightColor;

        public static ConfigEntry<float> lightIntensityOutdoors;
        public static ConfigEntry<float> lightRangeOutdoors;
        public static ConfigEntry<float> lightShadowsOutdoors;

        public static ConfigEntry<float> lightIntensityIndoors;
        public static ConfigEntry<float> lightRangeIndoors;
        public static ConfigEntry<float> lightShadowsIndoors;

        public static ConfigEntry<bool> showLightSource;
        public static ConfigEntry<bool> showLightFlare;

        private const string c_rootObjectName = "_shudnalRoot";
        private const string c_rootPrefabsName = "Prefabs";

        private static GameObject rootObject;
        private static GameObject rootPrefabs;

        public static GameObject fireflyPrefab;
        public static Sprite itemIcon;
        public static GameObject ballPrefab;

        public static bool prefabInit = false;

        private void Awake()
        {
            harmony.PatchAll();

            instance = this;

            ConfigInit();
            _ = configSync.AddLockingConfigEntry(configLocked);

            Game.isModded = true;

            LoadIcons();
        }

        private void OnDestroy()
        {
            Config.Save();
            instance = null;
            harmony?.UnpatchSelf();
        }

        public static void LogInfo(object data)
        {
            if (loggingEnabled.Value)
                instance.Logger.LogInfo(data);
        }

        public void ConfigInit()
        {
            config("General", "NexusID", 2741, "Nexus mod ID for updates", false);

            configLocked = config("General", "Lock Configuration", defaultValue: true, "Configuration is locked and can be changed by server admins only");
            loggingEnabled = config("General", "Logging enabled", defaultValue: false, "Enable logging. [Not Synced with Server]", false);

            itemStackSize = config("Item", "Stack size", defaultValue: 20, "How many items in stack");
            itemCraftingStation = config("Item", "Crafting station", defaultValue: "$piece_workbench", "Station to craft item. Leave empty to craft with hands");
            itemMinStationLevel = config("Item", "Crafting station level", defaultValue: 3, "Minimum level of station required to craft");
            statusEffectDuration = config("Item", "Duration", defaultValue: 300, "Duration of status effect");
            itemRecipe = config("Item", "Recipe", defaultValue: "Dandelion:1,GreydwarfEye:1,Resin:2", "Item recipe");
            itemWeight = config("Item", "Weight", defaultValue: 0.5f, "Item weight");

            itemCraftingStation.SettingChanged += (sender, args) => FireflyItem.SetFireFlyRecipe();
            itemMinStationLevel.SettingChanged += (sender, args) => FireflyItem.SetFireFlyRecipe();

            itemWeight.SettingChanged += (sender, args) => FireflyItem.PatchLanternItemOnConfigChange();
            itemStackSize.SettingChanged += (sender, args) => FireflyItem.PatchLanternItemOnConfigChange();

            lightColor = config("Light", "Color", defaultValue: new Color(1f, 0.62f, 0.48f), "Color of firefly light");

            lightIntensityOutdoors = config("Light - Outdoors", "Intensity", defaultValue: 1f, "Intensity of light");
            lightRangeOutdoors = config("Light - Outdoors", "Range", defaultValue: 30f, "Range of light");
            lightShadowsOutdoors = config("Light - Outdoors", "Shadows strength", defaultValue: 0.8f, "Strength of shadows");
            
            lightIntensityIndoors = config("Light - Indoors", "Intensity", defaultValue: 0.8f, "Intensity of light");
            lightRangeIndoors = config("Light - Indoors", "Range", defaultValue: 20f, "Range of light");
            lightShadowsIndoors = config("Light - Indoors", "Shadows strength", defaultValue: 0.9f, "Strength of shadows");

            showLightSource = config("Misc", "Show point of light source", defaultValue: false, "Show floating point of light. Restart to see changes.");
            showLightFlare = config("Misc", "Show flare around light source", defaultValue: false, "Show flare surrounding point of light. Restart to see changes.");
        }

        ConfigEntry<T> config<T>(string group, string name, T defaultValue, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, defaultValue, description);

            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        ConfigEntry<T> config<T>(string group, string name, T defaultValue, string description, bool synchronizedSetting = true) => config(group, name, defaultValue, new ConfigDescription(description), synchronizedSetting);

        private void LoadIcons()
        {
            LoadIcon("firefly.png", ref itemIcon);
        }

        private void LoadIcon(string filename, ref Sprite icon)
        {
            Texture2D tex = new Texture2D(2, 2);
            if (LoadTexture(filename, ref tex))
                icon = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero);
        }

        private bool LoadTexture(string filename, ref Texture2D tex)
        {
            string fileInPluginFolder = Path.Combine(Paths.PluginPath, filename);
            if (File.Exists(fileInPluginFolder))
            {
                LogInfo($"Loaded image: {fileInPluginFolder}");
                return tex.LoadImage(File.ReadAllBytes(fileInPluginFolder));
            }

            Assembly executingAssembly = Assembly.GetExecutingAssembly();

            string name = executingAssembly.GetManifestResourceNames().Single(str => str.EndsWith(filename));

            Stream resourceStream = executingAssembly.GetManifestResourceStream(name);

            byte[] data = new byte[resourceStream.Length];
            resourceStream.Read(data, 0, data.Length);

            return tex.LoadImage(data, true);
        }

        private static void InitRootObject()
        {
            if (rootObject == null)
                rootObject = GameObject.Find(c_rootObjectName) ?? new GameObject(c_rootObjectName);
            
            DontDestroyOnLoad(rootObject);

            if (rootPrefabs == null)
            {
                rootPrefabs = rootObject.transform.Find(c_rootPrefabsName)?.gameObject;

                if (rootPrefabs == null)
                {
                    rootPrefabs = new GameObject(c_rootPrefabsName);
                    rootPrefabs.transform.SetParent(rootObject.transform, false);
                    rootPrefabs.SetActive(false);
                }
            }
        }

        public static GameObject InitPrefabClone(GameObject prefabToClone, string prefabName)
        {
            InitRootObject();

            if (rootPrefabs.transform.Find(prefabName) != null)
                return rootPrefabs.transform.Find(prefabName).gameObject;

            prefabInit = true;
            GameObject clonedPrefab = Instantiate(prefabToClone, rootPrefabs.transform, false);
            prefabInit = false;
            clonedPrefab.name = prefabName;

            return clonedPrefab;
        }

        [HarmonyPatch(typeof(ZNetView), nameof(ZNetView.Awake))]
        public static class ZNetView_Awake_AddPrefab
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix() => !prefabInit;
        }

        [HarmonyPatch(typeof(ZSyncTransform), nameof(ZSyncTransform.Awake))]
        public static class ZSyncTransform_Awake_AddPrefab
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix() => !prefabInit;
        }
        
        [HarmonyPatch(typeof(ZSyncTransform), nameof(ZSyncTransform.OnEnable))]
        public static class ZSyncTransform_OnEnable_AddPrefab
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix() => !prefabInit;
        }
        
        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Awake))]
        public static class ItemDrop_Awake_AddPrefab
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix() => !prefabInit;
        }

        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Start))]
        public static class ItemDrop_Start_AddPrefab
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix() => !prefabInit;
        }
    }

}
