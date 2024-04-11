using HarmonyLib;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static Firefly.Firefly;

namespace Firefly
{
    public class SE_Firefly : SE_Demister
    {
        public const string statusEffectName = "Firefly";
        public static int statusEffectHash = statusEffectName.GetStableHashCode();

        public LightFlicker m_lightFlicker;
        public LightLod m_lightLod;
        public Light m_light;
        public bool m_indoors;

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            if (!m_ballInstance)
                return;

            if (!m_light || !m_lightFlicker || !m_lightLod)
            {
                m_lightFlicker = m_ballInstance.GetComponentInChildren<LightFlicker>();
                m_light = m_ballInstance.GetComponentInChildren<Light>();
                m_lightLod = m_ballInstance.GetComponentInChildren<LightLod>();
            }

            if (m_ttl != 0 && (m_ttl - GetDuration()) <= m_ttl * 0.1f)
            {
                SetLightIntensity(0.4f + 0.6f * (m_ttl - GetDuration()) / (m_ttl * 0.1f));
            }
            else if (m_character.InInterior() && !m_indoors)
            {
                m_indoors = true;
                SetLightIntensity(0.8f);
                m_light.range = 20;
                m_light.shadowStrength = 0.9f;
                m_lightLod.m_lightDistance = m_light.range;
                m_lightLod.m_baseRange = m_light.range;
                m_lightLod.m_baseShadowStrength = m_light.shadowStrength;
            }
            else if (!m_character.InInterior() && m_indoors)
            {
                m_indoors = true;
                SetLightIntensity(1f);
                m_light.range = 30;
                m_light.shadowStrength = 0.75f;
                m_lightLod.m_lightDistance = m_light.range;
                m_lightLod.m_baseRange = m_light.range;
                m_lightLod.m_baseShadowStrength = m_light.shadowStrength;
            }
        }

        private void SetLightIntensity(float intensity)
        {
            m_light.intensity = intensity;
            m_lightFlicker.m_baseIntensity = intensity;
        }

        public static void AddCustomStatusEffect(ObjectDB odb)
        {
            if (odb.m_StatusEffects.Count > 0)
            {
                SE_Demister demister = odb.m_StatusEffects.Find(se => se.name == "Demister") as SE_Demister;
                if (demister != null && !odb.m_StatusEffects.Any(se => se.name == statusEffectName))
                {
                    SE_Firefly firefly = ScriptableObject.CreateInstance<SE_Firefly>();

                    foreach (FieldInfo property in demister.GetType().GetFields())
                    {
                        FieldInfo field = firefly.GetType().GetField(property.Name);
                        if (field == null)
                            continue;

                        field.SetValue(firefly, property.GetValue(demister));
                    }

                    firefly.name = statusEffectName;
                    firefly.m_nameHash = statusEffectHash;
                    firefly.m_icon = itemIcon;
                    firefly.m_name = FireflyItem.itemDropName;
                    firefly.m_tooltip = FireflyItem.itemDropDescription;
                    firefly.m_ttl = 60;
                    firefly.m_startMessageType = MessageHud.MessageType.TopLeft;

                    if (ballPrefab == null)
                        CloneBallPrefab(demister.m_ballPrefab);

                    if (ballPrefab != null)
                        firefly.m_ballPrefab = ballPrefab;

                    odb.m_StatusEffects.Add(firefly);
                }
            }
        }

        public static void CloneBallPrefab(GameObject demisterBall)
        {
            if (demisterBall == null)
                return;

            ballPrefab = InitPrefabClone(demisterBall, "firefly_ball");

            UnityEngine.Object.DestroyImmediate(ballPrefab.transform.Find("demister_ball").gameObject);
            UnityEngine.Object.DestroyImmediate(ballPrefab.transform.Find("effects/Particle System Force Field").gameObject);

            AudioSource sfxStart = ballPrefab.transform.Find("effects/SFX Start").GetComponent<AudioSource>();
            sfxStart.volume = 0.2f;
            sfxStart.pitch = 0.8f;

            AudioSource sfx = ballPrefab.transform.Find("effects/SFX").GetComponent<AudioSource>();
            sfx.volume = 0.6f;
            sfx.pitch = 0.75f;

            Light ballLight = ballPrefab.transform.Find("effects/Point light").GetComponent<Light>();
            ballLight.intensity = 1;
            ballLight.color = new Color(1f, 0.62f, 0.48f);
            ballLight.range = 30;
            ballLight.shadows = LightShadows.Soft;
            ballLight.shadowStrength = 0.75f;

            Transform flameTransform = ballPrefab.transform.Find("effects/flame");

            for (int i = flameTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = flameTransform.GetChild(i);
                if (child.name != "sparcs_front")
                {
                    UnityEngine.Object.DestroyImmediate(flameTransform.GetChild(i).gameObject);
                    continue;
                }

                ParticleSystemRenderer psRenderer = child.GetComponent<ParticleSystemRenderer>();
                psRenderer.sharedMaterial = new Material(psRenderer.sharedMaterial)
                {
                    name = "firefly_sparcs"
                };
                psRenderer.sharedMaterial.SetColor("_EmissionColor", new Color(1f, 0.62f, 0.48f));
            }
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        public static class ObjectDB_Awake_AddStatusEffects
        {
            [HarmonyPriority(Priority.HigherThanNormal)]
            private static void Postfix(ObjectDB __instance)
            {
                AddCustomStatusEffect(__instance);
            }
        }
    }
}

