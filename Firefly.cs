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
        const string pluginVersion = "1.0.0";

        private readonly Harmony harmony = new Harmony(pluginID);

        internal static readonly ConfigSync configSync = new ConfigSync(pluginID) { DisplayName = pluginName, CurrentVersion = pluginVersion, MinimumRequiredVersion = pluginVersion };

        internal static Firefly instance;

        private static ConfigEntry<bool> configLocked;
        private static ConfigEntry<bool> loggingEnabled;

        private static GameObject rootObject;
        private static GameObject prefabsRoot;

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

            configLocked = config("General", "Lock Configuration", defaultValue: true, "Configuration is locked and can be changed by server admins only.");
            loggingEnabled = config("General", "Logging enabled", defaultValue: false, "Enable logging. [Not Synced with Server]", false);
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
                rootObject = new GameObject("_fireflyRoot");

            if (prefabsRoot == null)
            {
                prefabsRoot = new GameObject("Prefabs");
                prefabsRoot.transform.SetParent(rootObject.transform, false);
                prefabsRoot.SetActive(false);
            }
        }

        public static GameObject InitPrefabClone(GameObject prefabToClone, string prefabName)
        {
            InitRootObject();

            prefabInit = true;
            GameObject clonedPrefab = UnityEngine.Object.Instantiate(prefabToClone, prefabsRoot.transform, false);
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


    }
}
