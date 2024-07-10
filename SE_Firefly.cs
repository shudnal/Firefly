using HarmonyLib;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static Firefly.Firefly;

namespace Firefly
{
    public class SE_Firefly : StatusEffect
    {
        public const string statusEffectName = "Firefly";
        public static int statusEffectHash = statusEffectName.GetStableHashCode();

        public LightFlicker m_lightFlicker;
        public LightLod m_lightLod;
        public Light m_light;
        public bool m_indoors;

        [Header("SE_Firefly")]
        public GameObject m_ballPrefab;

        public Vector3 m_offset = new Vector3(0.1f, 3.5f, 0.1f);

        public Vector3 m_offsetInterior = new Vector3(0.5f, 2.1f, 0f);

        public float m_maxDistance = 7f;

        public float m_ballAcceleration = 6f;

        public float m_ballMaxSpeed = 60f;

        public float m_ballFriction = 0.02f;

        public float m_noiseDistance = 1.35f;

        public float m_noiseDistanceInterior = 0.2f;

        public float m_noiseDistanceYScale = 0.35f;

        public float m_noiseSpeed = 0.4f;

        public float m_characterVelocityFactor = 3f;

        public float m_rotationSpeed = 8f;

        public int m_coverRayMask;

        public GameObject m_ballInstance;

        public Vector3 m_ballVel = new Vector3(0f, 0f, 0f);

        public override void Setup(Character character)
        {
            base.Setup(character);
            if (m_coverRayMask == 0)
            {
                m_coverRayMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
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

        public bool IsUnderRoof()
        {
            return Physics.Raycast(m_character.GetCenterPoint(), Vector3.up, out _, 4f, m_coverRayMask);
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            if (!m_ballInstance)
            {
                Vector3 position = m_character.GetCenterPoint() + m_character.transform.forward * 0.5f;
                m_ballInstance = Object.Instantiate(m_ballPrefab, position, Quaternion.identity);
                return;
            }

            _ = m_character;
            bool num = IsUnderRoof();
            Vector3 position2 = m_character.transform.position;
            Vector3 vector = m_ballInstance.transform.position;
            Vector3 vector2 = (num ? m_offsetInterior : m_offset);
            float num2 = (num ? m_noiseDistanceInterior : m_noiseDistance);
            Vector3 vector3 = position2 + m_character.transform.TransformVector(vector2);
            float num3 = Time.time * m_noiseSpeed;
            vector3 += new Vector3(Mathf.Sin(num3 * 4f), Mathf.Sin(num3 * 2f) * m_noiseDistanceYScale, Mathf.Cos(num3 * 5f)) * num2;
            float num4 = Vector3.Distance(vector3, vector);
            if (num4 > m_maxDistance * 2f)
            {
                vector = vector3;
            }
            else if (num4 > m_maxDistance)
            {
                Vector3 normalized = (vector - vector3).normalized;
                vector = vector3 + normalized * m_maxDistance;
            }

            Vector3 normalized2 = (vector3 - vector).normalized;
            m_ballVel += normalized2 * m_ballAcceleration * dt;
            if (m_ballVel.magnitude > m_ballMaxSpeed)
            {
                m_ballVel = m_ballVel.normalized * m_ballMaxSpeed;
            }

            if (!num)
            {
                Vector3 velocity = m_character.GetVelocity();
                m_ballVel += velocity * m_characterVelocityFactor * dt;
            }

            m_ballVel -= m_ballVel * m_ballFriction;
            Vector3 position3 = vector + m_ballVel * dt;
            m_ballInstance.transform.position = position3;
            Quaternion rotation = m_ballInstance.transform.rotation;
            rotation *= Quaternion.Euler(m_rotationSpeed, 0f, m_rotationSpeed * 0.5321f);
            m_ballInstance.transform.rotation = rotation;

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

        public void RemoveEffects()
        {
            if (m_ballInstance != null)
            {
                ZNetView component = m_ballInstance.GetComponent<ZNetView>();
                if (component.IsValid())
                {
                    component.ClaimOwnership();
                    component.Destroy();
                }
            }
        }

        public override void OnApplicationQuit()
        {
            base.OnApplicationQuit();
            m_ballInstance = null;
        }

        public override void Stop()
        {
            base.Stop();
            RemoveEffects();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            RemoveEffects();
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

