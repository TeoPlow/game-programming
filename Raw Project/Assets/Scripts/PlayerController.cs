using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    [Header("Движение и прыжки")]
    public float forwardSpeed = 20f;
    public float nitroBoostSpeed = 35f;
    public float switchSpeed = 300f;
    public float switchSpeedFactor = 0.45f;
    public float switchArcToCenter = 0.75f;
    public float switchPitchAngle = 30f;

    [Header("Столкновения")]
    public float slowdownAmount = 10f;
    public float speedRecoveryRate = 5f;
    public float boostAccelerationRate = 20f;

    [Header("Поворот (Езда по дороге)")]
    public Transform carModel;
    public float steerSpeed = 15f;
    public float maxSteerAngle = 25f;
    public float maxLaneOffset = 3.5f;

    [Header("Взрыв")]
    public GameObject explosionPrefab;
    public float explosionLifetime = 1.5f;
    public float gameOverFreezeDelay = 0.8f;

    [Header("Звук")]
    public AudioClip engineClip;
    public AudioClip gameMusicClip;
    public AudioClip loseMusicClip;
    [Range(0f, 1f)] public float engineVolume = 0.35f;
    [Range(0f, 1f)] public float musicVolume = 0.4f;
    [Range(0f, 1f)] public float loseMusicVolume = 0.6f;
    public float minEnginePitch = 0.9f;
    public float maxEnginePitch = 1.6f;
    public float loseMusicFadeDuration = 0.6f;

    private float currentSpeed;
    private float targetZRotation = 0f;

    private float currentHorizontalPosition = 0f;
    private float currentSteerAngle = 0f;
    private float initialModelYRotation = 0f;
    private float baseModelY = 0f;
    private float baseModelZ = 0f;
    private float switchStartZRotation = 0f;
    private float switchTotalAngle = 90f;

    private bool isSwitching = false;
    private float lastHitTime = -10f;
    private float hitCooldown = 1.5f;
    private bool isGameOver = false;
    private bool isExploded = false;
    private int score = 0;
    private int bestScore = 0;
    private float scoreTimer = 0f;
    private float scorePopTimer = 0f;
    private int lastScoreForAnim = 0;

    private const string BestScoreKey = "BestScore";

    private GameObject gameOverCanvasObject;
    private Text gameOverText;
    private AudioSource engineSource;
    private AudioSource musicSource;
    private AudioSource loseMusicSource;

    void Start()
    {
        Time.timeScale = 1f;

        GameObject existingCanvas = GameObject.Find("GameOverCanvas");
        if (existingCanvas != null)
            Destroy(existingCanvas);

        gameOverCanvasObject = null;
        gameOverText = null;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        SetupAudio();
        rb.isKinematic = true;
        rb.useGravity = false;

        currentSpeed = forwardSpeed;
        targetZRotation = transform.eulerAngles.z;
        switchStartZRotation = targetZRotation;
        score = 0;
        bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
        scoreTimer = 0f;
        scorePopTimer = 0f;
        lastScoreForAnim = 0;

        if (carModel != null)
        {
            initialModelYRotation = carModel.localEulerAngles.y;
            currentHorizontalPosition = carModel.localPosition.x;
            baseModelY = carModel.localPosition.y;
            baseModelZ = carModel.localPosition.z;
        }
    }

    void Update()
    {
        if (isGameOver) return;

        bool isBoosting = Keyboard.current != null &&
                          (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed);

        scoreTimer += Time.deltaTime;
        while (scoreTimer >= 1f)
        {
            score += 1;
            scoreTimer -= 1f;
            scorePopTimer = 0.2f;
        }

        if (scorePopTimer > 0f)
            scorePopTimer -= Time.deltaTime;

        float targetSpeed = isBoosting ? nitroBoostSpeed : forwardSpeed;
        float speedChangeRate = isBoosting ? boostAccelerationRate : speedRecoveryRate;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, speedChangeRate * Time.deltaTime);
        UpdateEngineAudio(isBoosting);

        transform.Translate(Vector3.forward * currentSpeed * Time.deltaTime);

        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
                BeginSwitch(targetZRotation - 90f);
            else if (Mouse.current.rightButton.wasPressedThisFrame)
                BeginSwitch(targetZRotation + 90f);
            else if (Mouse.current.middleButton.wasPressedThisFrame)
                BeginSwitch(targetZRotation + 180f);
        }

        Vector3 currentAngles = transform.eulerAngles;
        float effectiveSwitchSpeed = switchSpeed * switchSpeedFactor;
        float newZ = Mathf.MoveTowardsAngle(currentAngles.z, targetZRotation, effectiveSwitchSpeed * Time.deltaTime);
        transform.eulerAngles = new Vector3(currentAngles.x, currentAngles.y, newZ);

        float angleDiff = Mathf.Abs(Mathf.DeltaAngle(newZ, targetZRotation));
        isSwitching = angleDiff > 1f;
        float switchProgress = GetSwitchProgress(newZ);
        float arc = Mathf.Sin(switchProgress * Mathf.PI);

        float horizontalInput = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                horizontalInput = -1f;
            else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                horizontalInput = 1f;
        }

        if (carModel != null)
        {
            currentHorizontalPosition += horizontalInput * steerSpeed * Time.deltaTime;
            currentHorizontalPosition = Mathf.Clamp(currentHorizontalPosition, -maxLaneOffset, maxLaneOffset);
            float switchY = Mathf.Lerp(baseModelY, baseModelY * (1f - switchArcToCenter), arc);
            carModel.localPosition = new Vector3(currentHorizontalPosition, switchY, baseModelZ);

            float targetAngle = horizontalInput * maxSteerAngle;
            currentSteerAngle = Mathf.LerpAngle(currentSteerAngle, targetAngle, Time.deltaTime * 8f);
            float switchPitch = -switchPitchAngle * arc;
            carModel.localRotation = Quaternion.Euler(switchPitch, initialModelYRotation + currentSteerAngle, 0f);
        }

        CheckCollisions();
    }

    private void CheckCollisions()
    {
        if (carModel == null) return;

        Collider[] hits = Physics.OverlapSphere(carModel.position, 2.0f);
        foreach (Collider hit in hits)
        {
            if (hit.transform == carModel || hit.transform.root == transform) continue;

            if (hit.CompareTag("Obstacle"))
            {
                ExplodePlayer();
                TriggerGameOver("Врезались в препятствие! ИГРА ОКОНЧЕНА!");
                continue;
            }

            EnemyController enemy = hit.transform.root.GetComponent<EnemyController>();
            if (enemy == null && !hit.CompareTag("Enemy")) continue;

            if (enemy != null)
            {
                if (enemy.IsSwitching && isSwitching)
                {
                    continue;
                }
                else if (enemy.IsSwitching)
                {
                    enemy.ExplodeAndDestroy();
                    ExplodePlayer();
                    TriggerGameOver("ВЫ ПРОИГРАЛИ: вражеская машина упала на крышу!");
                }
                else if (isSwitching)
                {
                    enemy.ExplodeAndDestroy();
                    score += 10;
                    scorePopTimer = 0.25f;
                }
                else if (Time.time > lastHitTime + hitCooldown)
                {
                    currentSpeed -= slowdownAmount;
                    if (currentSpeed < 5f) currentSpeed = 5f;
                    lastHitTime = Time.time;
                }
            }
        }
    }

    private void TriggerGameOver(string _)
    {
        if (isGameOver) return;
        isGameOver = true;

        if (score > bestScore)
        {
            bestScore = score;
            PlayerPrefs.SetInt(BestScoreKey, bestScore);
            PlayerPrefs.Save();
        }

        StartCoroutine(PlayLoseMusicRoutine());
        StartCoroutine(FreezeAfterDelay());
    }

    private IEnumerator FreezeAfterDelay()
    {
        yield return new WaitForSecondsRealtime(gameOverFreezeDelay);
        Time.timeScale = 0f;
    }

    private void ExplodePlayer()
    {
        if (isExploded) return;
        isExploded = true;

        Vector3 explosionPos = carModel != null ? carModel.position : transform.position;
        SpawnExplosion(explosionPos);

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        if (carModel != null)
            carModel.gameObject.SetActive(false);
    }

    private void SpawnExplosion(Vector3 worldPosition)
    {
        if (explosionPrefab == null) return;

        GameObject fx = Instantiate(explosionPrefab, worldPosition, Quaternion.identity);
        Destroy(fx, explosionLifetime + 0.5f);
    }

    private void SetupAudio()
    {
        engineSource = gameObject.AddComponent<AudioSource>();
        engineSource.clip = engineClip;
        engineSource.loop = true;
        engineSource.playOnAwake = false;
        engineSource.spatialBlend = 0f;
        engineSource.volume = engineVolume;
        engineSource.pitch = minEnginePitch;
        if (engineClip != null) engineSource.Play();

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.clip = gameMusicClip;
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
        musicSource.volume = musicVolume;
        if (gameMusicClip != null) musicSource.Play();

        loseMusicSource = gameObject.AddComponent<AudioSource>();
        loseMusicSource.clip = loseMusicClip;
        loseMusicSource.loop = false;
        loseMusicSource.playOnAwake = false;
        loseMusicSource.spatialBlend = 0f;
        loseMusicSource.volume = loseMusicVolume;
    }

    private void UpdateEngineAudio(bool isBoosting)
    {
        if (engineSource == null) return;

        float speedT = Mathf.InverseLerp(5f, nitroBoostSpeed, currentSpeed);
        float boostExtra = isBoosting ? 0.12f : 0f;
        float targetPitch = Mathf.Lerp(minEnginePitch, maxEnginePitch, speedT) + boostExtra;
        engineSource.pitch = Mathf.Lerp(engineSource.pitch, targetPitch, Time.deltaTime * 6f);
    }

    private IEnumerator PlayLoseMusicRoutine()
    {
        if (engineSource != null)
            engineSource.Stop();

        if (musicSource != null)
        {
            float startVolume = musicSource.volume;
            float timer = 0f;

            while (timer < loseMusicFadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / loseMusicFadeDuration);
                musicSource.volume = Mathf.Lerp(startVolume, 0f, t);
                yield return null;
            }

            musicSource.Stop();
            musicSource.volume = musicVolume;
        }

        if (loseMusicSource != null && loseMusicSource.clip != null)
            loseMusicSource.Play();
    }

    private void CreateGameOverUI()
    {
        if (gameOverCanvasObject != null) return;

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();
        }

        gameOverCanvasObject = new GameObject("GameOverCanvas");
        Canvas canvas = gameOverCanvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        gameOverCanvasObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = gameOverCanvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(gameOverCanvasObject.transform, false);
        Image panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.75f);

        RectTransform panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        GameObject textGo = new GameObject("GameOverText");
        textGo.transform.SetParent(panelGo.transform, false);
        gameOverText = textGo.AddComponent<Text>();
        gameOverText.alignment = TextAnchor.MiddleCenter;
        gameOverText.fontSize = 48;
        gameOverText.color = Color.white;
        gameOverText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        gameOverText.text = "ИГРА ОКОНЧЕНА";

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(1200f, 200f);
        textRect.anchoredPosition = new Vector2(0f, 120f);

        GameObject buttonGo = new GameObject("RestartButton");
        buttonGo.transform.SetParent(panelGo.transform, false);
        Image buttonImage = buttonGo.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        Button restartButton = buttonGo.AddComponent<Button>();
        restartButton.onClick.AddListener(RestartLevel);

        RectTransform buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(420f, 110f);
        buttonRect.anchoredPosition = new Vector2(0f, -20f);

        GameObject buttonTextGo = new GameObject("Text");
        buttonTextGo.transform.SetParent(buttonGo.transform, false);
        Text buttonText = buttonTextGo.AddComponent<Text>();
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.fontSize = 36;
        buttonText.color = Color.white;
        buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        buttonText.text = "Начать снова";

        RectTransform buttonTextRect = buttonTextGo.GetComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;

        gameOverCanvasObject.SetActive(false);
    }

    private void ShowGameOverUI(string message)
    {
        if (gameOverCanvasObject == null)
            CreateGameOverUI();

        if (gameOverText != null)
            gameOverText.text = message;

        gameOverCanvasObject.SetActive(true);
    }

    private void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void BeginSwitch(float newTargetZ)
    {
        switchStartZRotation = transform.eulerAngles.z;
        targetZRotation = newTargetZ;
        switchTotalAngle = Mathf.Abs(Mathf.DeltaAngle(switchStartZRotation, targetZRotation));
        if (switchTotalAngle < 1f) switchTotalAngle = 90f;
    }

    private float GetSwitchProgress(float currentZ)
    {
        float remaining = Mathf.Abs(Mathf.DeltaAngle(currentZ, targetZRotation));
        return 1f - Mathf.Clamp01(remaining / switchTotalAngle);
    }

    private void OnGUI()
    {
        if (score != lastScoreForAnim)
        {
            scorePopTimer = 0.25f;
            lastScoreForAnim = score;
        }

        int popSizeBonus = Mathf.RoundToInt(Mathf.Clamp01(scorePopTimer / 0.25f) * 10f);
        GUIStyle scoreStyle = new GUIStyle(GUI.skin.label);
        scoreStyle.fontSize = 28 + popSizeBonus;
        scoreStyle.normal.textColor = Color.white;
        scoreStyle.alignment = TextAnchor.UpperRight;

        GUI.Label(new Rect(Screen.width - 420f, 20f, 400f, 40f), $"Очки: {score}", scoreStyle);

        if (!isGameOver) return;

        float boxWidth = 560f;
        float boxHeight = 300f;
        Rect boxRect = new Rect((Screen.width - boxWidth) * 0.5f, (Screen.height - boxHeight) * 0.5f, boxWidth, boxHeight);

        GUI.Box(boxRect, "GAME OVER");

        GUIStyle centerStyle = new GUIStyle(GUI.skin.label);
        centerStyle.alignment = TextAnchor.MiddleCenter;
        centerStyle.fontSize = 26;
        centerStyle.normal.textColor = Color.white;

        GUI.Label(new Rect(boxRect.x + 40f, boxRect.y + 70f, boxRect.width - 80f, 40f), $"Итоговый счёт: {score}", centerStyle);
        GUI.Label(new Rect(boxRect.x + 40f, boxRect.y + 110f, boxRect.width - 80f, 40f), $"Максимум: {bestScore}", centerStyle);

        Rect buttonRect = new Rect(boxRect.x + (boxRect.width - 220f) * 0.5f, boxRect.y + 210f, 220f, 55f);
        if (GUI.Button(buttonRect, "Restart"))
        {
            RestartLevel();
        }
    }
}