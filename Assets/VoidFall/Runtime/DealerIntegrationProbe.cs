using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using VoidFall.Core;
using VoidFall.Persistence;

namespace VoidFall.Runtime
{
    /// <summary>Opt-in rendered player verification; isolated before the first profile load.</summary>
    public sealed class DealerIntegrationProbe : MonoBehaviour
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private VoidFallGameRuntime _runtime;
        private string _folder;
        private LegendaryWeaponId _pose;
        public static string OutputFolder
        {
            get { foreach (var arg in Environment.GetCommandLineArgs()) if (arg.StartsWith("-vfdealer-check=", StringComparison.OrdinalIgnoreCase)) return Path.GetFullPath(arg.Substring(16).Trim('"')); return null; }
        }
        public static string ProfilePath => OutputFolder == null ? null : Path.Combine(OutputFolder, "profile.json");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            var folder = OutputFolder; if (folder == null) return;
            Directory.CreateDirectory(folder); Application.runInBackground = true;
            var root = new GameObject("Dealer Integration Check"); DontDestroyOnLoad(root);
            root.AddComponent<DealerIntegrationProbe>()._folder = folder;
        }
        private void Update()
        {
            if (_runtime == null) return;
            Set("_applicationInactive", false);
            Set("_paused", true);
        }
        private void LateUpdate()
        {
            if (_runtime != null && Get("_ui") is VoidFall.UI.UIManager ui)
            {
                ui.Pause?.SetVisible(false);
                if (_runtime.JourneyStatus == "Junction")
                {
                    Set("_paused", false); Call("RenderJunction"); Set("_paused", true);
                }
            }
            if (_runtime == null || _pose == LegendaryWeaponId.None) return;
            var state = (LegendaryState)Get("_legendaryState"); state.Angle = .12;
            if (_pose == LegendaryWeaponId.ChargedRifle)
            {
                state.Charge = 2.5;
                Set("_legendaryBeamLife", .23f); Set("_legendaryBeamWidth", 64f);
                Set("_legendaryBeamStart", new Vector2(69, 8)); Set("_legendaryBeamEnd", new Vector2(950, 120));
            }
            Call("RenderLegendaries");
        }
        private IEnumerator Start()
        {
            var routine = Run();
            while (true)
            {
                bool more; object current = null;
                try { more = routine.MoveNext(); if (more) current = routine.Current; }
                catch (Exception error)
                {
                    File.WriteAllText(Path.Combine(_folder, "failure.txt"), error.ToString()); Debug.LogError(error); Application.Quit(1); yield break;
                }
                if (!more) yield break; yield return current;
            }
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.55f); yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_folder, name + ".png"));
            yield return new WaitForSecondsRealtime(.3f);
        }
        private IEnumerator Run()
        {
            yield return null; yield return null;
            _runtime = FindAnyObjectByType<VoidFallGameRuntime>(); if (_runtime == null) throw new InvalidOperationException("Runtime missing.");
            var profile = SaveStore.CreateDefault(); profile.directorOnboardingSeen = true;
            profile.settings.fullscreenMode = 3; profile.settings.resolutionWidth = 1600; profile.settings.resolutionHeight = 900;
            var store = new SaveStore(ProfilePath); store.Save(profile); Set("_saveData", profile); Set("_saveStore", store);
            Set("_diagnosticRunSeedOverride", 2848592627u); Set("_applicationInactive", false);
            Call("StartRunInternal", true, true); Set("_paused", true);
            _runtime.ApplySettings();
            Call("OnVoidObjectiveCompleted"); Call("BeginPortalJunction"); Set("_paused", true);
            if (((Vector2)Get("_dealerPosition")).y <= 0) throw new InvalidOperationException("Dealer must appear above the platform.");
            Set("_dealerVariation", 0);
            yield return Capture("room-top");
            Set("_dealerVariation", 3);
            yield return Capture("room-top-variation");
            // Browse from the legal platform edge, rather than teleporting outside it to the dealer anchor.
            SetPlayer("Position", new Vector2(0, 40)); yield return Capture("room-browse");
            Set("_partsEarned", 100); Set("_paused", false); Call("OpenDealer");
            if (!(bool)Get("_dealerOpen")) throw new InvalidOperationException("Dealer did not open.");
            yield return Capture("shop");
            var session = (DealerSession)Get("_dealerSession"); var index = Array.FindIndex(session.Offers, o => o.Kind == DealerOfferKind.Fragment);
            Call("BuyDealerOffer", index);
            if ((int)Get("_partsEarned") != 0 || store.Load().soundBladeFragments != 1) throw new InvalidOperationException("First fragment transaction failed.");
            yield return Capture("shop-purchased");
            Call("CloseDealer"); ((SaveData)Get("_saveData")).soundBladeFragments = 3;
            Set("_dealerSession", new DealerSession(new[] { DealerRules.Fragment(LegendaryWeaponId.SoundBlade, 2) }));
            Set("_partsEarned", 100); Set("_paused", false); Call("OpenDealer");
            yield return new WaitForSecondsRealtime(.2f); Call("BuyDealerOffer", 0);
            if ((LegendaryWeaponId)Get("_legendaryWeapon") != LegendaryWeaponId.SoundBlade || store.Load().soundBladeFragments != 7)
                throw new InvalidOperationException("Assembly did not save and equip.");
            yield return Capture("shop-assembled");
            Call("CloseDealer"); Call("HideJunction");
            var field = typeof(VoidFallGameRuntime).GetField("_journeyStage", Flags); field.SetValue(_runtime, Enum.Parse(field.FieldType, "Combat"));
            SetPlayer("Position", Vector2.zero); Set("_paused", true);
            _pose = LegendaryWeaponId.SoundBlade; yield return Capture("sound-blade");
            Call("EquipLegendary", LegendaryWeaponId.ChargedRifle, 1); _pose = LegendaryWeaponId.ChargedRifle;
            yield return Capture("charged-rifle"); _pose = LegendaryWeaponId.None;
            Set("_runSaved", true); Call("EnterMainMenu"); yield return Capture("main-menu");
            File.WriteAllText(Path.Combine(_folder, "success.txt"), "Native crossing top/bottom, shop transaction, saved assembly/equip, and both weapon visuals completed.\nProfile: " + ProfilePath);
            Application.Quit(0);
        }
        private object Get(string name) => typeof(VoidFallGameRuntime).GetField(name, Flags).GetValue(_runtime);
        private void Set(string name, object value) => typeof(VoidFallGameRuntime).GetField(name, Flags).SetValue(_runtime, value);
        private void SetPlayer(string name, object value)
        {
            var sim = Get("_gameSim"); var field = sim.GetType().GetField("Player", Flags); var player = field.GetValue(sim);
            player.GetType().GetField(name, Flags).SetValue(player, value); field.SetValue(sim, player);
        }
        private object Call(string name, params object[] args)
        {
            foreach (var method in typeof(VoidFallGameRuntime).GetMethods(Flags)) if (method.Name == name && method.GetParameters().Length == args.Length)
                try { return method.Invoke(_runtime, args); } catch (TargetInvocationException e) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException ?? e).Throw(); throw; }
            throw new MissingMethodException(name);
        }
    }
}
