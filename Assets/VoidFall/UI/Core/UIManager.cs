using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VoidFall.UI
{
    /// <summary>
    /// Which screen the interface should be showing. The runtime owns this state
    /// (it already tracks menu pages, pause, level-up and game-over), so the UI
    /// never runs a second navigation state machine that could disagree.
    /// </summary>
    public enum UIScreen
    {
        None,
        Home,
        Workshop,
        Records,
        Settings,
        LevelUp,
        Pause,
        Revive,
        GameOver,
        Roulette,
        RouteSelect,
        PrizeReveal
    }

    /// <summary>
    /// Everything the interface can ask the game to do. Passed once at startup.
    ///
    /// A field bag rather than a long positional argument list: the previous
    /// fourteen-parameter factory made it impossible to add a callback without
    /// risking a silent mis-ordering of same-typed arguments.
    /// </summary>
    public sealed class UICallbacks
    {
        public Action StartRun;
        public Action RestartRun;
        public Action ResumeRun;
        public Action AbortToMenu;

        public Action OpenWorkshop;
        public Action OpenRecords;
        public Action OpenSettings;
        public Action CloseMenuPage;

        public Action PrevArena;
        public Action NextArena;

        public Action<string> BuyWorkshop;
        public Action<string> PreviewWorkshop;
        public Action RefundWorkshop;

        public Action<float> SetMasterVolume;
        public Action<float> SetEffectsVolume;
        public Action<float> SetMusicVolume;
        public Action<float> SetScreenShake;
        public Action<float> SetTouchSize;
        public Action<string> SetQuality;
        public Action<bool> SetReducedMotion;
        public Action<bool> SetHighContrast;

        /// <summary>VIDEO section. Resolution 0 x 0 means AUTO (native).</summary>
        public Action<int, int> SetResolution;
        public Action<int> SetDisplayMode;
        public Action<float> SetBloom;
        public Action<float> SetChromatic;

        public Action ToggleMute;
        public Func<bool> IsMuted;

        public Action ResetProgress;
        public Action ExportSave;
        public Action ExportTelemetry;

        public Action AcceptRevive;
        public Action DeclineRevive;
        public Action RerollUpgrades;

        /// <summary>Confirmed exit from the home screen's close control.</summary>
        public Action QuitGame;

        /// <summary>
        /// Menu music muffle tracking the quit dialog's visibility. Invoked with
        /// false on every dismissal path, including SetScreen sweeps.
        /// </summary>
        public Action<bool> SetQuitDialogOpen;
    }

    /// <summary>Current audio and display preferences, pushed by the runtime.</summary>
    public struct UISettingsState
    {
        public float MasterVolume;
        public float EffectsVolume;
        public float MusicVolume;
        public float ScreenShake;
        public float TouchSize;
        public string Quality;
        public bool ReducedMotion;
        public bool HighContrast;

        // VIDEO. Resolution 0 x 0 means AUTO (native); negative effect
        // intensities mean "use the shipped defaults" (bloom 1.2, chromatic 0.12).
        public int ResolutionWidth;
        public int ResolutionHeight;
        public int FullscreenMode;
        public float Bloom;
        public float Chromatic;
    }

    /// <summary>Lifetime profile figures shown on the home screen and records.</summary>
    public struct UIProfileState
    {
        public int Parts;
        public int BestScore;
        public int TotalRuns;
        public string ArenaName;
    }

    /// <summary>
    /// Owns the menu canvas and every view, and applies the screen state the
    /// runtime pushes. Created once and kept for the process lifetime.
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        private static UIManager _instance;
        public static UIManager Instance => _instance;

        public HUDView HUD { get; private set; }
        public ToastView Toasts { get; private set; }
        public MainMenuView MainMenu { get; private set; }
        public LevelUpView LevelUp { get; private set; }
        public GameOverView GameOver { get; private set; }
        public PauseView Pause { get; private set; }
        public WorkshopView Workshop { get; private set; }
        public RecordsView Records { get; private set; }
        public SettingsView Settings { get; private set; }
        public EvolutionRevealView Evolution { get; private set; }
        public RevivePromptView Revive { get; private set; }
        public DebugOverlayView DebugOverlay { get; private set; }

        /// <summary>
        /// Boss Roulette ceremony. Opened by the runtime after a boss kill;
        /// not part of the per-frame screen sweep because it owns a modal
        /// session rather than a navigation state.
        /// </summary>
        public RouletteView Roulette { get; private set; }

        /// <summary>
        /// The branching choice after a Void's objective completes; like the
        /// roulette it is a modal session, not a navigation page.
        /// </summary>
        public RouteSelectView RouteSelect { get; private set; }

        /// <summary>The single-card prize reveal after a roulette lands.</summary>
        public PrizeRevealView PrizeReveal { get; private set; }

        /// <summary>
        /// Exit confirmation for the home screen's corner close control. A modal
        /// session rather than a navigation state: it is dismissed by any
        /// SetScreen sweep instead of occupying a UIScreen slot.
        /// </summary>
        public QuitConfirmView QuitConfirm { get; private set; }

        public UICallbacks Callbacks { get; private set; }

        private Canvas _canvas;
        private RectTransform _root;
        private RawImage _backdrop;
        private Image _backdropWash;
        private Image _menuScrim;
        private CanvasGroup _menuLayer;
        private RectTransform _menuLayerRect;
        private RectTransform _overlayLayerRect;
        private RectTransform _chromeLayerRect;
        private Button _muteButton;
        private TMPro.TextMeshProUGUI _muteGlyph;
        private UIScreen _screen = UIScreen.None;

        /// <summary>
        /// Creates the manager and builds the whole interface.
        /// </summary>
        public static UIManager Create(UICallbacks callbacks)
        {
            // A second runtime would otherwise get a fully built interface on a
            // GameObject that Awake has already scheduled for destruction, so
            // hand back the live one instead.
            if (_instance != null) return _instance;

            var go = new GameObject("VoidFall UI");
            DontDestroyOnLoad(go);
            var manager = go.AddComponent<UIManager>();

            // Awake runs during AddComponent and rejects duplicates, so confirm
            // this instance won the singleton before building anything on it.
            if (_instance != manager)
            {
                Destroy(go);
                return _instance;
            }

            manager.Build(callbacks ?? new UICallbacks());
            return manager;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Build(UICallbacks callbacks)
        {
            Callbacks = callbacks;

            // Above the runtime's gameplay HUD canvas, which renders the health,
            // clock and weapon strips and is left in place by this migration.
            _canvas = UIBuilder.CreateCanvas("VoidFall Menu Canvas", 200);
            _canvas.transform.SetParent(transform, false);
            _root = (RectTransform)_canvas.transform;

            BuildBackdrop();

            _menuLayerRect = UIBuilder.Stretch(UIBuilder.CreateRect(_root, "Menu Layer"));
            _menuLayer = UIBuilder.EnsureGroup(_menuLayerRect.gameObject);

            _overlayLayerRect = UIBuilder.Stretch(UIBuilder.CreateRect(_root, "Overlay Layer"));
            var effectLayer = UIBuilder.Stretch(UIBuilder.CreateRect(_root, "Effect Layer"));
            _chromeLayerRect = UIBuilder.Stretch(UIBuilder.CreateRect(_root, "Chrome Layer"));

            MainMenu = CreateView<MainMenuView>(_menuLayerRect, "Main Menu");
            MainMenu.Initialize(this);

            Workshop = CreateView<WorkshopView>(_menuLayerRect, "Workshop");
            Workshop.Initialize(this);

            Records = CreateView<RecordsView>(_menuLayerRect, "Records");
            Records.Initialize(this);

            Settings = CreateView<SettingsView>(_menuLayerRect, "Settings");
            Settings.Initialize(this);

            LevelUp = CreateView<LevelUpView>(_overlayLayerRect, "Level Up");
            LevelUp.Initialize(this);

            Pause = CreateView<PauseView>(_overlayLayerRect, "Pause");
            Pause.Initialize(this);

            Revive = CreateView<RevivePromptView>(_overlayLayerRect, "Revive");
            Revive.Initialize(this);

            GameOver = CreateView<GameOverView>(_overlayLayerRect, "Game Over");
            GameOver.Initialize(this);

            Roulette = CreateView<RouletteView>(_overlayLayerRect, "Boss Roulette");
            Roulette.Initialize(this);

            RouteSelect = CreateView<RouteSelectView>(_overlayLayerRect, "Route Select");
            RouteSelect.Initialize(this);

            PrizeReveal = CreateView<PrizeRevealView>(_overlayLayerRect, "Prize Reveal");
            PrizeReveal.Initialize(this);

            QuitConfirm = CreateView<QuitConfirmView>(_overlayLayerRect, "Quit Confirm");
            QuitConfirm.Initialize(this);

            Evolution = CreateView<EvolutionRevealView>(effectLayer, "Evolution Reveal");
            Evolution.Initialize(this);

            Toasts = CreateView<ToastView>(effectLayer, "Notices");
            Toasts.Initialize(this);

            DebugOverlay = CreateView<DebugOverlayView>(_chromeLayerRect, "Diagnostics");
            DebugOverlay.Initialize(this);

            BuildMuteControl();

            // The HUD stays with the runtime, which authors it directly on its own
            // canvas. This view is kept as an inert seam so the runtime's
            // per-frame HUD calls remain valid.
            HUD = CreateView<HUDView>(_chromeLayerRect, "HUD Seam");
            HUD.Initialize(this);

            SetScreen(UIScreen.None);
        }

        private T CreateView<T>(Transform parent, string name) where T : Component
        {
            var rect = UIBuilder.Stretch(UIBuilder.CreateRect(parent, name));
            return rect.gameObject.AddComponent<T>();
        }

        /// <summary>
        /// The menu background: the baked arena plate the runtime hands over,
        /// then the stylesheet's radial cyan wash and a very light scrim. The
        /// browser build lets the game canvas show through .menu-layer the same
        /// way, so the menus never sit on flat black.
        /// </summary>
        private void BuildBackdrop()
        {
            var backdropRect = UIBuilder.Stretch(UIBuilder.CreateRect(_root, "Backdrop"));
            _backdrop = backdropRect.gameObject.AddComponent<RawImage>();
            _backdrop.color = Color.white;
            _backdrop.raycastTarget = false;
            _backdrop.enabled = false;

            // radial-gradient(circle at 50% 32%, rgba(34,211,238,0.055), transparent 43%)
            var wash = UIBuilder.CreateSurface(_root, "Wash", UISprites.Glow(256));
            wash.type = Image.Type.Simple;
            wash.rectTransform.anchorMin = new Vector2(0.5f, 0.68f);
            wash.rectTransform.anchorMax = new Vector2(0.5f, 0.68f);
            wash.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            wash.rectTransform.sizeDelta = new Vector2(1500f, 1500f);
            wash.color = UITheme.WithAlpha(UITheme.Cyan, 0.055f);
            _backdropWash = wash;

            _menuScrim = UIBuilder.CreateScrim(_root, "Menu Scrim", UITheme.MenuScrim, false);
        }

        private void BuildMuteControl()
        {
            var muted = Callbacks.IsMuted != null && Callbacks.IsMuted();
            _muteButton = UIBuilder.CreateIconButton(
                _chromeLayerRect,
                "Mute",
                muted ? "\u2715" : "\u266B",
                () =>
                {
                    Callbacks.ToggleMute?.Invoke();
                    RefreshMuteGlyph();
                });

            var rect = _muteButton.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-14f, 14f);

            _muteGlyph = _muteButton.transform.Find("Glyph")?.GetComponent<TMPro.TextMeshProUGUI>();
        }

        /// <summary>Keeps the corner control in sync after a keyboard mute.</summary>
        public void RefreshMuteGlyph()
        {
            if (_muteGlyph == null) return;
            var muted = Callbacks?.IsMuted != null && Callbacks.IsMuted();
            _muteGlyph.text = muted ? "\u2715" : "\u266B";
            _muteGlyph.color = muted ? UITheme.TextMetricLabel : UITheme.TextChip;
        }

        /// <summary>
        /// Supplies the baked arena plate used behind the menus. Passing null
        /// hides the layer and leaves the rendered world visible instead.
        /// </summary>
        public void SetBackdrop(Texture texture)
        {
            if (_backdrop == null) return;
            _backdrop.texture = texture;
            _backdrop.enabled = texture != null;
        }

        /// <summary>
        /// Shows exactly one screen. Called by the runtime whenever its own menu
        /// or overlay state changes.
        /// </summary>
        public void SetScreen(UIScreen screen)
        {
            _screen = screen;

            var menuVisible = screen == UIScreen.Home || screen == UIScreen.Workshop ||
                screen == UIScreen.Records || screen == UIScreen.Settings;

            if (_menuScrim != null) _menuScrim.enabled = menuVisible;
            if (_backdropWash != null) _backdropWash.enabled = menuVisible;
            if (_backdrop != null && _backdrop.texture != null) _backdrop.enabled = menuVisible;
            if (_menuLayer != null) _menuLayer.blocksRaycasts = menuVisible;

            MainMenu?.SetVisible(screen == UIScreen.Home);
            Workshop?.SetVisible(screen == UIScreen.Workshop);
            Records?.SetVisible(screen == UIScreen.Records);
            Settings?.SetVisible(screen == UIScreen.Settings);
            LevelUp?.SetVisible(screen == UIScreen.LevelUp);
            Pause?.SetVisible(screen == UIScreen.Pause);
            Revive?.SetVisible(screen == UIScreen.Revive);
            GameOver?.SetVisible(screen == UIScreen.GameOver);

            // Notices must not compete with a decision overlay (level-up, revive,
            // pause, run result), matching .toast-stack.is-obscured.
            //
            // They must still show on the menu screens, though: purchases, save
            // failures, imports and resets all originate there, so hiding the
            // stack for every screen except gameplay silently suppressed the
            // messages on precisely the pages that produce them.
            var decisionOverlay = screen == UIScreen.LevelUp || screen == UIScreen.Revive ||
                screen == UIScreen.Pause || screen == UIScreen.GameOver ||
                screen == UIScreen.Roulette || screen == UIScreen.RouteSelect ||
                screen == UIScreen.PrizeReveal;
            Toasts?.SetObscured(decisionOverlay);
            Roulette?.SetVisible(screen == UIScreen.Roulette);
            RouteSelect?.SetVisible(screen == UIScreen.RouteSelect);
            PrizeReveal?.SetVisible(screen == UIScreen.PrizeReveal);

            // The quit confirmation is modal over the home screen only; any
            // navigation (Tab toggle, starting a run, pause) dismisses it.
            QuitConfirm?.SetVisible(false);
        }

        public UIScreen CurrentScreen => _screen;

        /// <summary>Retained for the runtime's existing call sites.</summary>
        public void SwitchToMainMenu() => SetScreen(UIScreen.Home);

        /// <summary>Retained for the runtime's existing call sites.</summary>
        public void SwitchToGameplay() => SetScreen(UIScreen.None);
    }
}
