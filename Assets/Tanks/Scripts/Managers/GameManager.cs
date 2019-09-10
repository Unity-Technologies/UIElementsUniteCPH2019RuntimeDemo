using System.Collections;
using Unity.UIElements.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Complete
{
    public class GameManager : MonoBehaviour
    {
        public CameraControl m_CameraControl;       // Reference to the CameraControl script for control during different phases.
        public GameObject m_TankPrefab;             // Reference to the prefab the players will control.
        public TankManager[] m_Tanks;               // A collection of managers for enabling and disabling different aspects of the tanks.

        public PanelRenderer m_MainMenuScreen;
        public PanelRenderer m_GameScreen;
        public PanelRenderer m_EndScreen;

        public VisualTreeAsset m_PlayerListItem;

        private void OnEnable()
        {
            m_MainMenuScreen.uxmlWasReloaded = BindMainMenuScreen;
            m_GameScreen.uxmlWasReloaded = BindGameScreen;
            m_EndScreen.uxmlWasReloaded = BindEndScreen;
        }

        private void Start()
        {
            GoToMainMenu();
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

            root.Q<Button>("back-to-menu").clickable.clicked += () =>
            {
                SceneManager.LoadScene(0);
            };
            root.Q<Button>("random-explosion").clickable.clicked += () =>
            {
                EndRound();
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

            var playerColor = m_Tanks[index].m_PlayerColor;
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
        }

        private void SetCameraTargets()
        {
            // Create a collection of transforms the same size as the number of tanks.
            Transform[] targets = new Transform[m_Tanks.Length];

            // For each of these transforms...
            for (int i = 0; i < targets.Length; i++)
            {
                // ... set it to the appropriate tank transform.
                targets[i] = m_Tanks[i].m_Instance.transform;
            }

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