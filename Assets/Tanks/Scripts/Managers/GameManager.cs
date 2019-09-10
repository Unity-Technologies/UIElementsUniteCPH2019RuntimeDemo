using System.Collections;
using Unity.UIElements.Runtime;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Complete
{
    public class GameManager : MonoBehaviour
    {
        public CameraControl m_CameraControl;       // Reference to the CameraControl script for control during different phases.
        public GameObject m_TankPrefab;             // Reference to the prefab the players will control.
        public Rigidbody m_Shell;                   // Prefab of the shell.
        public int m_ShellRandomRange = 20;
        public int m_ShellForce = 25;
        public int m_ShellWaveCount = 10;
        public float m_ShellDelay = 0.1f;
        public TankManager[] m_Tanks;               // A collection of managers for enabling and disabling different aspects of the tanks.

        public PanelRenderer m_MainMenuScreen;
        public PanelRenderer m_GameScreen;
        public PanelRenderer m_EndScreen;

        public VisualTreeAsset m_PlayerListItem;

        private Label m_SpeedLabel;
        private Label m_KillsLabel;
        private Label m_ShotsLabel;
        private Label m_AccuracyLabel;

        private TankMovement m_Player1Movement;
        private TankShooting m_Player1Shooting;

        private WaitForSeconds m_ShellTime;

        private void OnEnable()
        {
            m_MainMenuScreen.uxmlWasReloaded = BindMainMenuScreen;
            m_GameScreen.uxmlWasReloaded = BindGameScreen;
            m_EndScreen.uxmlWasReloaded = BindEndScreen;

            m_ShellTime = new WaitForSeconds(m_ShellDelay);
        }

        private void Start()
        {
            GoToMainMenu();
        }

        private void Update()
        {
            if (m_SpeedLabel == null || m_Tanks.Length == 0 || m_Player1Movement == null)
                return;

            m_SpeedLabel.text = m_Player1Movement.m_Speed.ToString();

            var kills = m_Tanks.Length;
            foreach (var tank in m_Tanks)
                if (tank.m_Instance.activeSelf)
                    kills--;
            m_KillsLabel.text = kills.ToString();

            var fireCount = m_Player1Shooting.m_FireCount;
            m_ShotsLabel.text = fireCount.ToString();

            var hitCount = m_Player1Shooting.m_HitCount;
            if (fireCount == 0)
                fireCount = 1; // Avoid div by 0.
            var percent = (int)(((float)hitCount / (float)fireCount) * 100);
            m_AccuracyLabel.text = percent.ToString();
        }

        private void BindMainMenuScreen()
        {
            var root = m_MainMenuScreen.visualTree;

            root.Q<Button>("start-button").clickable.clicked += () =>
            {
                StartRound();
            };
            root.Q<Button>("exit-button").clickable.clicked += () =>
            {
                Application.Quit();
            };
        }

        private void BindGameScreen()
        {
            var root = m_GameScreen.visualTree;

            // Stats
            m_SpeedLabel = root.Q<Label>("_speed");
            m_KillsLabel = root.Q<Label>("_kills");
            m_ShotsLabel = root.Q<Label>("_shots");
            m_AccuracyLabel = root.Q<Label>("_accuracy");

            // Buttons
            root.Q<Button>("increase-speed").clickable.clicked += () =>
            {
                m_Player1Movement.m_Speed += 1;
            };
            root.Q<Button>("back-to-menu").clickable.clicked += () =>
            {
                SceneManager.LoadScene(0);
            };
            root.Q<Button>("random-explosion").clickable.clicked += () =>
            {
                StartCoroutine(Firestorm());
            };

            var listView = root.Q<ListView>("player-list");
            if (listView.makeItem == null)
                listView.makeItem = MakeItem;
            if (listView.bindItem == null)
                listView.bindItem = BindItem;

            listView.itemsSource = m_Tanks;
            listView.Refresh();
        }

        private VisualElement MakeItem()
        {
            var element = m_PlayerListItem.CloneTree();

            element.schedule.Execute(() => UpdateHealthBar(element)).Every(500);

            return element;
        }

        private void BindItem(VisualElement element, int index)
        {
            element.Q<Label>("player-name").text = "Player " + m_Tanks[index].m_PlayerNumber;

            var playerColor = m_Tanks[index].color;
            playerColor.a = 0.9f;
            element.Q("icon").style.backgroundColor = playerColor;

            element.userData = m_Tanks[index];
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

        private void BindEndScreen()
        {
            var root = m_EndScreen.visualTree;

            root.Q<Button>("back-to-menu-button").clickable.clicked += () =>
            {
                SceneManager.LoadScene(0);
            };
        }

        private void SpawnAllTanks()
        {
            // For all the tanks...
            for (int i = 0; i < m_Tanks.Length; i++)
            {
                // ... create them, set their player number and references needed for control.
                m_Tanks[i].m_Instance =
                    Instantiate(m_TankPrefab, m_Tanks[i].m_SpawnPoint.position, m_Tanks[i].m_SpawnPoint.rotation) as GameObject;
                m_Tanks[i].m_PlayerNumber = i + 1;
                m_Tanks[i].Setup();
            }

            var instance = m_Tanks[0].m_Instance;
            m_Player1Movement = instance.GetComponent<TankMovement>();
            m_Player1Shooting = instance.GetComponent<TankShooting>();
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
            m_MainMenuScreen.visualTree.style.display = DisplayStyle.Flex;
            m_GameScreen.visualTree.style.display = DisplayStyle.None;
            m_EndScreen.visualTree.style.display = DisplayStyle.None;
        }

        private void StartRound()
        {
            SpawnAllTanks();
            SetCameraTargets();

            // As soon as the round starts reset the tanks and make sure they can't move.
            ResetAllTanks();

            // Snap the camera's zoom and position to something appropriate for the reset tanks.
            m_CameraControl.SetStartPositionAndSize();

            // As soon as the round begins playing let the players control the tanks.
            EnableTankControl();

            m_MainMenuScreen.visualTree.style.display = DisplayStyle.None;
            m_GameScreen.visualTree.style.display = DisplayStyle.Flex;
            m_EndScreen.visualTree.style.display = DisplayStyle.None;
        }

        private void EndRound()
        {
            // Stop tanks from moving.
            DisableTankControl();

            m_MainMenuScreen.visualTree.style.display = DisplayStyle.None;
            m_GameScreen.visualTree.style.display = DisplayStyle.None;
            m_EndScreen.visualTree.style.display = DisplayStyle.Flex;
        }


        // This is used to check if there is one or fewer tanks remaining and thus the round should end.
        private bool OneTankLeft()
        {
            // Start the count of tanks left at zero.
            int numTanksLeft = 0;

            // Go through all the tanks...
            for (int i = 0; i < m_Tanks.Length; i++)
            {
                // ... and if they are active, increment the counter.
                if (m_Tanks[i].m_Instance.activeSelf)
                    numTanksLeft++;
            }

            // If there are one or fewer tanks remaining return true, otherwise return false.
            return numTanksLeft <= 1;
        }
        
        
        // This function is to find out if there is a winner of the round.
        // This function is called with the assumption that 1 or fewer tanks are currently active.
        private TankManager GetRoundWinner()
        {
            // Go through all the tanks...
            for (int i = 0; i < m_Tanks.Length; i++)
            {
                // ... and if one of them is active, it is the winner so return it.
                if (m_Tanks[i].m_Instance.activeSelf)
                    return m_Tanks[i];
            }

            // If none of the tanks are active it is a draw so return null.
            return null;
        }

        // This function is used to turn all the tanks back on and reset their positions and properties.
        private void ResetAllTanks()
        {
            for (int i = 0; i < m_Tanks.Length; i++)
            {
                m_Tanks[i].Reset();
            }
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