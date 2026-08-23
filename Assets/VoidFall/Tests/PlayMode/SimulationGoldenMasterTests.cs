using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Runtime;

namespace VoidFall.Tests.PlayMode
{
    /// <summary>
    /// Golden-master guard for the fixed-step simulation. Boots the real
    /// runtime, applies the productionMax stress scenario with a fixed seed,
    /// steps the private Simulate(double) entry point a fixed number of times,
    /// and hashes every gameplay state array bit-exactly.
    ///
    /// Any refactor that changes simulation behavior - extraction, reordering,
    /// float drift - changes this hash and fails here. When a change is
    /// intentionally behavioral, regenerate the constant, say so in the commit,
    /// and land it separately from mechanical work.
    /// </summary>
    public sealed class SimulationGoldenMasterTests
    {
        private const uint Seed = 0x5f1dc0deu;
        private const string ScenarioId = "productionMax";
        private const int Ticks = 600;
        private const double FixedDt = 1.0 / 60.0;

        /// <summary>Public fields on the runtime's GameSim instance.</summary>
        private static readonly string[] GameStateArrayFields =
        {
            "Enemies",
            "Bullets",
            "HostileShots",
            "Pickups",
            "Bosses",
            "Meteors",
        };

        /// <summary>Public fields on the runtime's FxSim instance.</summary>
        private static readonly string[] FxStateArrayFields =
        {
            "MeteorShards",
            "SourceParticles",
        };

        private static readonly string[] ScalarFields =
        {
            "_time",
            "_xp",
            "_xpNeed",
            "_level",
            "_kills",
            // Player vitals migrated into GameSim.Player; dotted paths resolve
            // through the runtime's _gameSim field. Order and values are
            // unchanged from the original scalar list, so the mix is identical.
            "_gameSim.Player.Health",
            "_gameSim.Player.MaxHealth",
            "_gameSim.Player.Position",
            "_gameSim.Player.Velocity",
            "_runSeed",
        };

        [UnityTest]
        public IEnumerator Fixed_step_simulation_matches_the_golden_master_hash()
        {
            var runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(runtime, Is.Not.Null, "The parity probe did not create the game runtime.");
            yield return null;
            yield return null;

            // Hermeticity: cosmetic budgets (particle/shard counts) read the
            // player's saved quality preset and reduced-motion flag, and those
            // budgets are hashed. Pin them so local machine state cannot drift
            // the golden master.
            PinHermeticPresentationState(runtime);

            var apply = typeof(VoidFallGameRuntime).GetMethod(
                "ApplyStressScenario",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(apply, Is.Not.Null, "ApplyStressScenario disappeared; update this test.");
            Assert.That(
                (bool)apply.Invoke(runtime, new object[] { ScenarioId, Seed }),
                Is.True,
                "The stress scenario could not be applied.");

            var simulate = typeof(VoidFallGameRuntime).GetMethod(
                "Simulate",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(simulate, Is.Not.Null, "Simulate(double) disappeared; update this test.");
            for (var tick = 0; tick < Ticks; tick++)
                simulate.Invoke(runtime, new object[] { FixedDt });

            var hash = HashRuntimeState(runtime);
            Assert.That(
                hash,
                Is.EqualTo(GoldenMasterHash),
                $"Simulation state hash drifted. If this refactor is intentionally behavioral, " +
                $"replace GoldenMasterHash with {hash} in a separate, clearly described commit.");
            yield break;
        }

        /// <summary>Computed once and pinned. See class comment before changing.</summary>
        internal const ulong GoldenMasterHash = 7472786138139307308;

        internal static ulong HashRuntimeState(object runtime)
        {
            var type = runtime.GetType();
            ulong hash = 0xcbf29ce484222325ul;

            // Order matters: the hash is a sequence mix. Keep this layout
            // identical to the original field order even as state migrates.
            var game = GetField(runtime, type, "_gameSim");
            foreach (var name in GameStateArrayFields)
                HashArray(ref hash, GetField(game, game.GetType(), name));
            var fx = GetField(runtime, type, "_fxSim");
            foreach (var name in FxStateArrayFields)
                HashArray(ref hash, GetField(fx, fx.GetType(), name));
            foreach (var name in ScalarFields)
                HashValue(ref hash, ResolvePath(runtime, name));

            var combatRng = GetField(game, game.GetType(), "Rng");
            HashValue(ref hash, GetField(combatRng, combatRng.GetType(), "_state"));
            HashValue(ref hash, GetMember(combatRng, combatRng.GetType(), "Draws"));
            var fxRng = GetField(fx, fx.GetType(), "FxRng");
            HashValue(ref hash, GetField(fxRng, fxRng.GetType(), "_state"));
            HashValue(ref hash, GetMember(fxRng, fxRng.GetType(), "Draws"));

            return hash;
        }

        private static object GetField(object target, Type type, string name)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, $"Missing state field '{name}'; update SimulationGoldenMasterTests.");
            return field.GetValue(target);
        }

        /// <summary>
        /// Forces a deterministic presentation profile: reduced motion off,
        /// high contrast off, default touch size, and the High quality preset.
        /// Uses reflection so this assembly does not need a Persistence
        /// reference; fails loudly if the shape changes.
        /// </summary>
        internal static void PinHermeticPresentationState(object runtime)
        {
            var type = runtime.GetType();
            var save = GetField(runtime, type, "_saveData");
            Assert.That(save, Is.Not.Null, "Runtime has no save profile to pin.");

            var settingsField = save.GetType().GetField("settings");
            Assert.That(settingsField, Is.Not.Null, "Save profile has no settings field.");
            var settings = settingsField.GetValue(save);
            if (settings == null)
            {
                settings = System.Activator.CreateInstance(settingsField.FieldType);
                settingsField.SetValue(save, settings);
            }
            var st = settings.GetType();
            st.GetField("reducedMotion").SetValue(settings, false);
            st.GetField("highContrast").SetValue(settings, false);
            st.GetField("touchSize").SetValue(settings, 1f);
            st.GetField("quality").SetValue(settings, "high");

            var rulesType = typeof(VoidFall.Core.QualityRules);
            var presetIdType = rulesType.Assembly.GetType("VoidFall.Core.QualityPresetId");
            Assert.That(presetIdType, Is.Not.Null, "QualityPresetId moved; update SimulationGoldenMasterTests.");
            var highId = System.Enum.Parse(presetIdType, "High");
            var preset = rulesType.GetMethod("Preset").Invoke(null, new[] { highId });

            var applyPreset = type.GetMethod(
                "ApplyQualityPreset",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { preset.GetType() },
                null);
            Assert.That(applyPreset, Is.Not.Null, "ApplyQualityPreset(QualityPreset) moved; update this test.");
            applyPreset.Invoke(runtime, new[] { preset });
        }

        /// <summary>
        /// Resolves a dotted path like "_gameSim.Player.Health" from the root
        /// object. Each segment is a field on the previous segment's type.
        /// </summary>
        private static object ResolvePath(object root, string path)
        {
            var target = root;
            var type = root.GetType();
            foreach (var part in path.Split('.'))
            {
                target = GetField(target, type, part);
                type = target.GetType();
            }
            return target;
        }

        private static object GetMember(object target, Type type, string name)
        {
            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (property != null) return property.GetValue(target);
            return GetField(target, type, name);
        }

        private static void HashArray(ref ulong hash, object arrayObj)
        {
            if (arrayObj is Array array)
            {
                Mix(ref hash, (ulong)array.Length);
                for (var index = 0; index < array.Length; index++)
                    HashValue(ref hash, array.GetValue(index));
                return;
            }
            Mix(ref hash, 0x9e3779b97f4a7c15ul);
        }

        private static void HashValue(ref ulong hash, object value)
        {
            if (value == null)
            {
                Mix(ref hash, 0x517cc1b727220a95ul);
                return;
            }

            var type = value.GetType();
            unchecked
            {
                switch (value)
                {
                    case bool b: Mix(ref hash, b ? 1ul : 0ul); return;
                    case byte v: Mix(ref hash, v); return;
                    case sbyte v: Mix(ref hash, (ulong)v); return;
                    case char v: Mix(ref hash, v); return;
                    case short v: Mix(ref hash, (ulong)v); return;
                    case ushort v: Mix(ref hash, v); return;
                    case int v: Mix(ref hash, (ulong)(long)v); return;
                    case uint v: Mix(ref hash, v); return;
                    case long v: Mix(ref hash, (ulong)v); return;
                    case ulong v: Mix(ref hash, v); return;
                    case float v: Mix(ref hash, BitConverter.ToUInt32(BitConverter.GetBytes(v), 0)); return;
                    case double v: Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(v)); return;
                    case decimal v: Mix(ref hash, (ulong)v.GetHashCode()); return;
                    case string s:
                        Mix(ref hash, (ulong)s.Length);
                        for (var index = 0; index < s.Length; index++)
                            Mix(ref hash, s[index]);
                        return;
                    case Enum e: HashValue(ref hash, Convert.ChangeType(e, e.GetTypeCode())); return;
                    default:
                        if (type.IsPrimitive)
                        {
                            Mix(ref hash, (ulong)value.GetHashCode());
                            return;
                        }
                        foreach (var field in type.GetFields(
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public |
                            BindingFlags.DeclaredOnly))
                        {
                            HashValue(ref hash, field.GetValue(value));
                        }
                        return;
                }
            }
        }

        private static void Mix(ref ulong hash, ulong value)
        {
            unchecked
            {
                hash ^= value + 0x9e3779b97f4a7c15ul + (hash << 6) + (hash >> 2);
                hash *= 0xff51afd7ed558ccdul;
                hash ^= hash >> 29;
            }
        }
    }
}
