using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    const string HIGH_SCORE_KEY = "GC_HISCORE";

    [Header("Player")]
    [SerializeField] PlayerDeath playerDeath;

    [Header("Camera")]
    [SerializeField] SideScrollCamera2D sideScrollCamera;

    [Header("Respawn")]
    [SerializeField] Transform defaultCheckpoint;

    [Header("Gameplay Stats")]
    [Min(1)] [SerializeField] int startingLives = 3;
    [Min(1)] [SerializeField] int fruitsPerExtraLife = 100;

    [Header("Events")]
    public UnityEvent onGameOver;

    Transform currentCheckpoint;
    bool cameraLocked;

    int currentLives;
    int score;
    int fruitsCollectedTotal;
    int highScore;

    public int Lives => currentLives;
    public int Score => score;
    public int FruitsCollected => fruitsCollectedTotal;
    public int FruitsTowardsExtraLife => fruitsPerExtraLife > 0 ? fruitsCollectedTotal % fruitsPerExtraLife : 0;
    public int HighScore => highScore;
    public int FruitsPerExtraLife => fruitsPerExtraLife;

    public event Action StatsChanged;

    void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        highScore = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
        InitializeSession();
        EnsureReferences();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureReferences();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureReferences();
    }

    void EnsureReferences()
    {
        if (!playerDeath)
            playerDeath = FindObjectOfType<PlayerDeath>();

        if (!sideScrollCamera)
            sideScrollCamera = FindObjectOfType<SideScrollCamera2D>();

        if (!currentCheckpoint)
            currentCheckpoint = defaultCheckpoint;
    }

    void InitializeSession()
    {
        currentLives = Mathf.Max(1, startingLives);
        score = 0;
        fruitsCollectedTotal = 0;
        currentCheckpoint = defaultCheckpoint;
        NotifyStatsChanged();
    }

    public void ResetSession()
    {
        InitializeSession();
    }

    public void HandlePlayerKilled(PlayerDeath pd)
    {
        if (pd) playerDeath = pd;
        EnsureReferences();
        RegisterPlayerDeath();
        LockCameraForDeath();
    }

    public void RegisterPlayerDeath()
    {
        currentLives = Mathf.Max(0, currentLives - 1);
        NotifyStatsChanged();

        if (currentLives <= 0)
            TriggerGameOver();
    }

    void TriggerGameOver()
    {
        onGameOver?.Invoke();
        UnlockCameraIfLocked();
        ResetSession();
    }

    public void RegisterFruitCollect(int scoreValue, int fruitAmount)
    {
        int previousMilestone = fruitsPerExtraLife > 0 ? fruitsCollectedTotal / fruitsPerExtraLife : 0;

        AddScoreInternal(scoreValue);
        fruitsCollectedTotal = Mathf.Max(0, fruitsCollectedTotal + Mathf.Max(0, fruitAmount));

        if (fruitsPerExtraLife > 0)
        {
            int newMilestone = fruitsCollectedTotal / fruitsPerExtraLife;
            int extraLives = Mathf.Max(0, newMilestone - previousMilestone);
            if (extraLives > 0)
                currentLives += extraLives;
        }

        NotifyStatsChanged();
    }

    public void RegisterEnemyDefeated(int scoreValue)
    {
        AddScoreInternal(scoreValue);
        NotifyStatsChanged();
    }

    public void RegisterScore(int scoreValue)
    {
        AddScoreInternal(scoreValue);
        NotifyStatsChanged();
    }

    void AddScoreInternal(int amount)
    {
        if (amount == 0) return;
        score = Mathf.Max(0, score + amount);
        if (score > highScore)
        {
            highScore = score;
            PlayerPrefs.SetInt(HIGH_SCORE_KEY, highScore);
            PlayerPrefs.Save();
        }
    }

    public void RequestRespawn(PlayerDeath pd, Transform checkpointOverride = null)
    {
        if (pd) playerDeath = pd;
        if (!pd)
        {
            UnlockCameraIfLocked();
            return;
        }

        Transform spawn = checkpointOverride ? checkpointOverride : currentCheckpoint;
        if (!spawn) spawn = defaultCheckpoint;

        if (!spawn)
        {
            Debug.LogWarning("Respawn requested but no checkpoint is assigned.");
            UnlockCameraIfLocked();
            return;
        }

        pd.RespawnAt(spawn);
        UnlockCameraIfLocked();
    }

    public void SetCheckpoint(Transform checkpoint)
    {
        if (checkpoint) currentCheckpoint = checkpoint;
    }

    void UnlockCameraIfLocked()
    {
        if (cameraLocked && sideScrollCamera)
        {
            sideScrollCamera.SetLockVertical(false);
            cameraLocked = false;
        }
    }

    public void LockCameraForDeath()
    {
        if (!sideScrollCamera)
        {
            EnsureReferences();
            if (!sideScrollCamera)
            {
                Camera mainCam = Camera.main;
                if (mainCam) sideScrollCamera = mainCam.GetComponent<SideScrollCamera2D>();
                if (!sideScrollCamera) sideScrollCamera = FindObjectOfType<SideScrollCamera2D>();
            }
        }

        if (sideScrollCamera)
        {
            sideScrollCamera.SetLockVertical(true, true);
            cameraLocked = true;
        }
    }

    void NotifyStatsChanged() => StatsChanged?.Invoke();
}
