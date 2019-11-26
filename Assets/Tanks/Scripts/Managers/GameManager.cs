using System;
using System.Collections;
using System.Collections.Generic;
using Unity.UIElements.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Complete
{
    public class GameManager : MonoBehaviour
    {
        // Public Properties
        public CameraControl m_CameraControl;       // Reference to the CameraControl script for control during different phases.
        public GameObject m_TankPrefab;             // Reference to the prefab the players will control.
        public Rigidbody m_Shell;                   // Prefab of the shell.
        public int m_ShellRandomRange = 20;
        public int m_ShellForce = 25;
        public int m_ShellWaveCount = 10;
        public float m_ShellDelay = 0.1f;
        public TankManager[] m_Tanks;               // A collection of managers for enabling and disabling different aspects of the tanks.

        // UI Panel Renderers
        // Note: normally you can just use one Panel Rendere and just hide or swap in/out
        // (using ve.style.display) elements at runtime. It's a lot more efficient
        // to use a single PanelRenderer.
        public PanelRenderer m_MainMenuScreen;
        public PanelRenderer m_GameScreen;
        public PanelRenderer m_EndScreen;

        // Pre-loaded UI assets (ie. UXML/USS).
        public VisualTreeAsset m_PlayerListItem;
        public StyleSheet m_PlayerListItemStyles;

        // The Panel Renderer can optionally track assets to enable live
        // updates to any changes made in the UI Builder for specific UI
        // assets (ie. UXML/USS).
        private List<Object> m_TrackedAssetsForLiveUpdates;

        // We need to update the values of some UI elements so here are
        // their remembered references after being queried from the cloned
        // UXML.
        private Label m_SpeedLabel;
        private Label m_KillsLabel;
        private Label m_ShotsLabel;
        private Label m_AccuracyLabel;

        // Game-related variables.
        private TankMovement m_Player1Movement;
        private TankShooting m_Player1Shooting;
        private TankHealth m_Player1Health;

        // UIElements has minimal support for animation at this time so we
        // implement a custom solution here to animate the game logo. We
        // just use the built-in UIElements scheduler to repeatedly change
        // the background image of an element with the next frame in the
        // animation.
        int m_CurrentTitleLogoFrame = 0;
        public List<Texture2D> m_TitleLogoFrames = new List<Texture2D>();
        int m_CurrentEndScreenFrame = 0;
        public List<Texture2D> m_EndScreenFrames = new List<Texture2D>();

        // Internal variables.
        WaitForSeconds m_ShellTime;
        int m_MaxFirestormCount = 5;

        ///////////////////////////////////////////////////////////////////////////////////////////////////
        // MonoBehaviour States

        // OnEnable
        // Register our postUxmlReload callbacks to be notified if and when
        // the UXML or USS assets being user are changed (by the UI Builder).
        // In these callbacks, we just rebind UI VisualElements to data or
        // to click events.
        private void OnEnable()
        {
            m_MainMenuScreen.postUxmlReload = BindMainMenuScreen;
            m_GameScreen.postUxmlReload = BindGameScreen;
            m_EndScreen.postUxmlReload = BindEndScreen;

            m_TrackedAssetsForLiveUpdates = new List<Object>();

            m_ShellTime = new WaitForSeconds(m_ShellDelay);
        }

        // Start
        // Just go to main menu.
        private void Start()
        {
#if !UNITY_EDITOR
            if (Screen.fullScreen)
                Screen.fullScreen = false;
#endif

            GoToMainMenu();
        }

        // Update
        // Update UI Labels with data from the game. (also run some minimal game logic)
        private void Update()
        {
            // If any of our UI Labels have not been bound, do nothing.
            if (m_SpeedLabel == null || m_Tanks.Length == 0 || m_Player1Movement == null || m_Player1Health == null)
                return;

            // Player is dead..
            if (m_Player1Health.m_Dead)
                EndRound();

            // Update UI label text.
            m_SpeedLabel.text = m_Player1Movement.m_Speed.ToString();

            // Update UI label text.
            var kills = m_Tanks.Length;
            foreach (var tank in m_Tanks)
                if (tank.m_Instance.activeSelf)
                    kills--;
            m_KillsLabel.text = kills.ToString();

            // Update UI label text.
            var fireCount = m_Player1Shooting.m_FireCount;
            m_ShotsLabel.text = fireCount.ToString();

            // Update UI label text.
            var hitCount = m_Player1Shooting.m_HitCount;
            if (fireCount == 0)
                fireCount = 1; // Avoid div by 0.
            var percent = (int)(((float)hitCount / (float)fireCount) * 100);
            m_AccuracyLabel.text = percent.ToString();
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////////
        // Bind UI to logic

        // Try to find specific elements by name in the main menu screen and
        // bind them to callbacks and data. Not finding an element is a valid
        // state.
        private IEnumerable<Object> BindMainMenuScreen()
        {
            var root = m_MainMenuScreen.visualTree;

            var startButton = root.Q<Button>("start-button");
            if (startButton != null)
            {
                startButton.clickable.clicked += () =>
                {
                    StartRound();
                };
            }

            var exitButton = root.Q<Button>("exit-button");
            if (exitButton != null)
            {
                exitButton.clickable.clicked += () =>
                {
                    Application.Quit();
                };
            }

            // Animate title logo.
            var titleLogo = root.Q("menu-title-image");
            titleLogo?.schedule.Execute(() =>
            {
                if (m_TitleLogoFrames.Count == 0)
                    return;

                m_CurrentTitleLogoFrame = (m_CurrentTitleLogoFrame + 1) % m_TitleLogoFrames.Count;
                var frame = m_TitleLogoFrames[m_CurrentTitleLogoFrame];
                titleLogo.style.backgroundImage = frame;
            }).Every(200);

            return null;
        }

        // Try to find specific elements by name in the game screen and
        // bind them to callbacks and data. Not finding an element is a valid
        // state.
        private IEnumerable<Object> BindGameScreen()
        {
            var root = m_GameScreen.visualTree;

            // Stats
            m_SpeedLabel = root.Q<Label>("_speed");
            m_KillsLabel = root.Q<Label>("_kills");
            m_ShotsLabel = root.Q<Label>("_shots");
            m_AccuracyLabel = root.Q<Label>("_accuracy");

            // Buttons
            var increaseSpeedButton = root.Q<Button>("increase-speed");
            if (increaseSpeedButton != null)
            {
                increaseSpeedButton.clickable.clicked += () =>
                {
                    m_Player1Movement.m_Speed += 1;
                };
            }
            var backToMenuButton = root.Q<Button>("back-to-menu");
            if (backToMenuButton != null)
            {
                backToMenuButton.clickable.clicked += () =>
                {
                    SceneManager.LoadScene(0);
                };
            }
            var randomExplosionButton = root.Q<Button>("random-explosion");
            if (randomExplosionButton != null)
            {
                randomExplosionButton.clickable.clicked += () =>
                {
                    m_MaxFirestormCount--;
                    if (m_MaxFirestormCount < 0)
                        EndRound();
                    StartCoroutine(Firestorm());
                };
            }

            var listView = root.Q<ListView>("player-list");
            m_TrackedAssetsForLiveUpdates.Clear();
            if (listView != null)
            {
                listView.selectionType = SelectionType.None;

                if (listView.makeItem == null)
                    listView.makeItem = MakeItem;
                if (listView.bindItem == null)
                    listView.bindItem = BindItem;

                listView.itemsSource = m_Tanks;
                listView.Refresh();

                m_TrackedAssetsForLiveUpdates.Add(m_PlayerListItem);
                m_TrackedAssetsForLiveUpdates.Add(m_PlayerListItemStyles);
            }

            return m_TrackedAssetsForLiveUpdates;
        }

        // Try to find specific elements by name in the end screen and
        // bind them to callbacks and data. Not finding an element is a valid
        // state.
        private IEnumerable<Object> BindEndScreen()
        {
            var root = m_EndScreen.visualTree;

            root.Q<Button>("back-to-menu-button").clickable.clicked += () =>
            {
                SceneManager.LoadScene(0);
            };

            // Animate end skull.
            var titleLogo = root.Q("menu-title-image");
            titleLogo?.schedule.Execute(() =>
            {
                if (m_EndScreenFrames.Count == 0)
                    return;

                m_CurrentEndScreenFrame = (m_CurrentEndScreenFrame + 1) % m_EndScreenFrames.Count;
                var frame = m_EndScreenFrames[m_CurrentEndScreenFrame];
                titleLogo.style.backgroundImage = frame;
            }).Every(100);

            return null;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////////
        // In-Game Virtualized ListView Implementation

        private VisualElement MakeItem()
        {
            var element = m_PlayerListItem.CloneTree();

            element.schedule.Execute(() => UpdateHealthBar(element)).Every(200);

            return element;
        }

        private void BindItem(VisualElement element, int index)
        {
            element.Q<Label>("player-name").text = "Player " + m_Tanks[index].m_PlayerNumber;

            var playerColor = m_Tanks[index].color;
            playerColor.a = 0.9f;
            element.Q("icon").style.unityBackgroundImageTintColor = playerColor;

            element.userData = m_Tanks[index];

            UpdateHealthBar(element);
        }

        private void UpdateHealthBar(VisualElement element)
        {
            var tank = element.userData as TankManager;
            if (tank == null)
                return;

            var healthBar = element.Q("health-bar");
            var healthBarFill = element.Q("health-bar-fill");

            var totalWidth = healthBar.resolvedStyle.width;

            var healthComponent = tank.m_Instance.GetComponent<TankHealth>();
            var currentHealth = healthComponent.m_CurrentHealth;
            var startingHealth = healthComponent.m_StartingHealth;
            var percentHealth = currentHealth / startingHealth;

            healthBarFill.style.width = totalWidth * percentHealth;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////////
        // Screen Transition Logic

        void SetScreenEnableState(PanelRenderer screen, bool state)
        {
            if (state)
            {
                screen.visualTree.style.display = DisplayStyle.Flex;
                screen.enabled = true;
                screen.gameObject.GetComponent<UIElementsEventSystem>().enabled = true;
            }
            else
            {
                screen.visualTree.style.display = DisplayStyle.None;
                screen.enabled = false;
                screen.gameObject.GetComponent<UIElementsEventSystem>().enabled = false;
            }
        }

        IEnumerator TransitionScreens(PanelRenderer from, PanelRenderer to)
        {
            from.visualTree.style.display = DisplayStyle.None;
            from.gameObject.GetComponent<UIElementsEventSystem>().enabled = false;

            to.enabled = true;

            yield return null;
            yield return null;
            yield return null;

            to.visualTree.style.display = DisplayStyle.Flex;
            to.visualTree.style.visibility = Visibility.Hidden;
            to.gameObject.GetComponent<UIElementsEventSystem>().enabled = true;

            yield return null;
            yield return null;
            yield return null;

            to.visualTree.style.visibility = Visibility.Visible;

            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;

            from.enabled = false;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////////
        // Game Logic (unrelated to UI)

        private IEnumerator Firestorm()
        {
            var shellsLeft = m_ShellWaveCount;

            while (shellsLeft > 0)
            {
                var x = Random.Range(-m_ShellRandomRange, m_ShellRandomRange);
                var z = Random.Range(-m_ShellRandomRange, m_ShellRandomRange);
                var position = new Vector3(x, 20, z);
                var rotation = Quaternion.FromToRotation(position, new Vector3(x, 0f, z));

                Rigidbody shellInstance =
                    Instantiate(m_Shell, position, rotation) as Rigidbody;

                shellInstance.gameObject.GetComponent<ShellExplosion>().m_TankMask = -1;

                // Set the shell's velocity to the launch force in the fire position's forward direction.
                shellInstance.velocity = 30.0f * Vector3.down;

                shellsLeft--;

                yield return m_ShellTime;
            }
        }

        private void SpawnAllTanks()
        {
            // For all the tanks...
            for (int i = 0; i < m_Tanks.Length; i++)
            {
                var ran = Random.Range(0, 180);
                var rot = Quaternion.Euler(0, ran, 0);

                // ... create them, set their player number and references needed for control.
                m_Tanks[i].m_Instance =
                    Instantiate(m_TankPrefab, m_Tanks[i].m_SpawnPoint.position, rot) as GameObject;
                m_Tanks[i].m_Instance.transform.localRotation = rot;
                m_Tanks[i].m_PlayerNumber = i + 1;
                m_Tanks[i].Setup();
            }

            var instance = m_Tanks[0].m_Instance;
            m_Player1Movement = instance.GetComponent<TankMovement>();
            m_Player1Shooting = instance.GetComponent<TankShooting>();
            m_Player1Health = instance.GetComponent<TankHealth>();
        }

        private void SetCameraTargets()
        {
            // Create a collection of transforms the same size as the number of tanks.
            Transform[] targets = new Transform[1];

            // Just add the first tank to the transform.
            targets[0] = m_Tanks[0].m_Instance.transform;

            // These are the targets the camera should follow.
            m_CameraControl.m_Targets = targets;
        }

        private void GoToMainMenu()
        {
            SetScreenEnableState(m_MainMenuScreen, true);
            SetScreenEnableState(m_GameScreen, false);
            SetScreenEnableState(m_EndScreen, false);
        }

        private void StartRound()
        {
            SpawnAllTanks();
            SetCameraTargets();

            // Snap the camera's zoom and position to something appropriate for the reset tanks.
            m_CameraControl.SetStartPositionAndSize();

            // As soon as the round begins playing let the players control the tanks.
            EnableTankControl();

            StartCoroutine(TransitionScreens(m_MainMenuScreen, m_GameScreen));
        }

        private void EndRound()
        {
            // Stop tanks from moving.
            DisableTankControl();

            SetScreenEnableState(m_MainMenuScreen, false);
            SetScreenEnableState(m_GameScreen, false);
            SetScreenEnableState(m_EndScreen, true);
        }

        private void EnableTankControl()
        {
            for (int i = 0; i < m_Tanks.Length; i++)
            {
                m_Tanks[i].EnableControl();
            }
        }


        private void DisableTankControl()
        {
            for (int i = 0; i < m_Tanks.Length; i++)
            {
                m_Tanks[i].DisableControl();
            }
        }
    }
}