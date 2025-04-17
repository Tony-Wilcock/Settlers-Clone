using UnityEngine;
using UnityEngine.UI; // Required for Button and Slider
using TMPro; // Optional: If using TextMeshPro for loading text

namespace PunkyFruitBat
{
    public class MainMenuManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject menuPanel; // The main panel holding the menu UI
        [SerializeField] private Button startGameButton;
        [SerializeField] private Slider loadingBarSlider;
        [SerializeField] private TMP_Text loadingText; // Optional: Text like "Loading..." or percentage

        [Header("Dependencies")]
        [SerializeField] private HexGridManager hexGridManager; // Assign in inspector

        private void Awake()
        {
            // Find HexGridManager if not assigned
            if (hexGridManager == null)
            {
                hexGridManager = HexGridManager.Instance;
            }

            if (hexGridManager == null)
            {
                Debug.LogError("MainMenuManager: HexGridManager instance not found!", this);
                enabled = false; // Disable script if manager is missing
                return;
            }

            // Ensure UI elements are assigned
            if (menuPanel == null || startGameButton == null || loadingBarSlider == null)
            {
                Debug.LogError("MainMenuManager: Assign Menu Panel, Start Button, and Loading Bar Slider in the Inspector!", this);
                enabled = false;
                return;
            }

            // Setup button listener
            startGameButton.onClick.AddListener(OnStartGameClicked);

            // Initial UI state
            menuPanel.SetActive(true);
            loadingBarSlider.gameObject.SetActive(false); // Hide loading bar initially
            loadingBarSlider.value = 0;
            if (loadingText != null) loadingText.text = "";
        }

        private void OnEnable()
        {
            // Subscribe to HexGridManager events when the menu becomes active
            if (hexGridManager != null)
            {
                hexGridManager.OnGenerationProgress += UpdateLoadingBar;
                hexGridManager.OnGridComplete += OnGridGenerationFinished;
            }
        }

        private void OnDisable()
        {
            // Unsubscribe when the menu is disabled or destroyed
            if (hexGridManager != null)
            {
                hexGridManager.OnGenerationProgress -= UpdateLoadingBar;
                hexGridManager.OnGridComplete -= OnGridGenerationFinished;
            }

            // Clean up button listener if the object is destroyed
            if (startGameButton != null)
            {
                startGameButton.onClick.RemoveListener(OnStartGameClicked);
            }
        }

        /// <summary>
        /// Called when the Start Game button is clicked.
        /// </summary>
        private void OnStartGameClicked()
        {
            // Disable button, show loading bar
            startGameButton.interactable = false;
            loadingBarSlider.gameObject.SetActive(true);
            if (loadingText != null) loadingText.text = "Generating Grid... 0%";

            // Tell the HexGridManager to start generation
            hexGridManager.StartGridGeneration();
        }

        /// <summary>
        /// Updates the loading bar based on grid generation progress.
        /// </summary>
        /// <param name="progress">Progress value from 0.0 to 1.0.</param>
        private void UpdateLoadingBar(float progress)
        {
            loadingBarSlider.value = progress * 100;
            if (loadingText != null)
            {
                loadingText.text = $"Generating Grid... {progress:P0}"; // Format as percentage
            }
        }

        /// <summary>
        /// Called when the HexGridManager signals that grid generation is complete.
        /// </summary>
        private void OnGridGenerationFinished()
        {
            // Hide loading bar and deactivate the menu panel
            loadingBarSlider.gameObject.SetActive(false);
            if (loadingText != null) loadingText.text = "Complete!"; // Optional final message

            // Optionally add a small delay before hiding the menu
             StartCoroutine(DeactivateMenuAfterDelay());
            //menuPanel.SetActive(false);

            // Optional: Re-enable the start button if the player might return to the menu?
            // startGameButton.interactable = true; // Or destroy the menu entirely
        }

        // Optional: Coroutine for delay
        private System.Collections.IEnumerator DeactivateMenuAfterDelay()
        {
            float delay = 0.5f; // Delay in seconds
            yield return WaitForSecondsFactory.WaitCoroutine(delay);
            menuPanel.SetActive(false);
        }
    }
}