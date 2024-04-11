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

                SetLightIntensity(lightIntensityIndoors.Value);
                m_light.range = lightRangeIndoors.Value;
                m_light.shadowStrength = lightShadowsIndoors.Value;

                m_lightLod.m_lightDistance = m_light.range;
                m_lightLod.m_baseRange = m_light.range;
                m_lightLod.m_baseShadowStrength = m_light.shadowStrength;

                m_light.shadows = m_light.shadowStrength > 0 ? LightShadows.Soft : LightShadows.None;
            }
            else if (!m_character.InInterior() && m_indoors)
            {
                m_indoors = true;

                SetLightIntensity(lightIntensityOutdoors.Value);
                m_light.range = lightRangeOutdoors.Value;
                m_light.shadowStrength = lightShadowsOutdoors.Value;

                m_lightLod.m_lightDistance = m_light.range;
                m_lightLod.m_baseRange = m_light.range;
                m_lightLod.m_baseShadowStrength = m_light.shadowStrength;
                
                m_light.shadows = m_light.shadowStrength > 0 ? LightShadows.Soft : LightShadows.None;
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
                    firefly.m_tooltip = FireflyItem.statusEffectDescription;
                    firefly.m_ttl = statusEffectDuration.Value;
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

            UnityEngine.Object.DestroyImmediate(ballPrefab.transform.Find("effects/Particle System Force Field").gameObject);

            GameObject ball = ballPrefab.transform.Find("demister_ball").gameObject;
            if (showLightSource.Value)
            {
                ball.name = "firefly_ball";
                ball.transform.localScale = Vector3.one * 0.03f;

                MeshRenderer ballRenderer = ball.GetComponent<MeshRenderer>();
                ballRenderer.sharedMaterial = new Material(ballRenderer.sharedMaterial)
                {
                    name = "firefly_ball"
                };
                ballRenderer.sharedMaterial.SetColor("_EmissionColor", lightColor.Value);
            }
            else
                UnityEngine.Object.DestroyImmediate(ball);

            AudioSource sfxStart = ballPrefab.transform.Find("effects/SFX Start").GetComponent<AudioSource>();
            sfxStart.volume = 0.2f;
            sfxStart.pitch = 0.8f;

            AudioSource sfx = ballPrefab.transform.Find("effects/SFX").GetComponent<AudioSource>();
            sfx.volume = 0.6f;
            sfx.pitch = 0.75f;

            Light ballLight = ballPrefab.transform.Find("effects/Point light").GetComponent<Light>();
            ballLight.intensity = lightIntensityOutdoors.Value;
            ballLight.color = lightColor.Value;
            ballLight.range = lightRangeOutdoors.Value;
            ballLight.shadowStrength = lightShadowsOutdoors.Value;
            ballLight.shadows = ballLight.shadowStrength > 0 ? LightShadows.Soft : LightShadows.None;

            Transform flameTransform = ballPrefab.transform.Find("effects/flame");

            for (int i = flameTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = flameTransform.GetChild(i);
                if (child.name == "sparcs_front")
                {
                    ParticleSystemRenderer psRenderer = child.GetComponent<ParticleSystemRenderer>();
                    psRenderer.sharedMaterial = new Material(psRenderer.sharedMaterial)
                    {
                        name = "firefly_sparcs"
                    };
                    psRenderer.sharedMaterial.SetColor("_EmissionColor", lightColor.Value);
                }
                else if (showLightFlare.Value && child.name == "flare")
                {
                    child.localScale = Vector3.one * 0.5f;
                    ParticleSystemRenderer psRenderer = child.GetComponent<ParticleSystemRenderer>();
                    psRenderer.sharedMaterial = new Material(psRenderer.sharedMaterial)
                    {
                        name = "firefly_flare"
                    };
                    psRenderer.sharedMaterial.SetColor("_Color", lightColor.Value);

                    ParticleSystem.MainModule mainModule = child.GetComponent<ParticleSystem>().main;
                    mainModule.startColor = new Color(lightColor.Value.r, lightColor.Value.g, lightColor.Value.b, 0.04f);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(flameTransform.GetChild(i).gameObject);
                    continue;
                }
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

