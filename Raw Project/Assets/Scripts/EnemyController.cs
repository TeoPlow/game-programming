using UnityEngine;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("Модель машинки (должна быть внутри)")]
    public Transform carModel;

    [Header("Характеристики врага")]
    public float forwardSpeed = 10f;
    public float switchSpeed = 200f;
    public float switchSpeedFactor = 0.45f;
    public float steerSpeed = 10f;
    public float maxSteerAngle = 20f;
    public float maxLaneOffset = 3.5f;
    public float switchArcToCenter = 0.75f;
    public float switchPitchAngle = 30f;

    [Header("Настройки ИИ")]
    public float switchIntervalMin = 2f;
    public float switchIntervalMax = 6f;
    public float steerIntervalMin = 2f;
    public float steerIntervalMax = 4f;

    [Header("Взрыв")]
    public GameObject explosionPrefab;
    public float explosionLifetime = 1.5f;

    [Header("Звук")]
    public AudioClip engineClip;
    [Range(0f, 1f)] public float engineVolume = 0.15f;
    public float minEnginePitch = 0.85f;
    public float maxEnginePitch = 1.15f;

    private float targetZRotation = 0f;
    private float currentHorizontalPosition = 0f;
    private float currentSteerAngle = 0f;
    private float targetHorizontalPosition = 0f;
    private float initialModelYRotation = 0f;
    private float baseModelY = 0f;
    private float baseModelZ = 0f;
    private float nextSwitchTime = 0f;
    private float nextSteerTime = 0f;
    private bool isSwitching = false;
    private float switchStartZRotation = 0f;
    private float switchTotalAngle = 90f;
    private bool isDestroyed = false;
    private AudioSource engineSource;

    public bool IsSwitching => isSwitching;

    private bool isInitialized = false;

    public void InitSpawn(float xOffset, float radius, float angle)
    {
        ResolveCarModelReference();

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        SetupEngineAudio();

        transform.localRotation = Quaternion.Euler(0, 0, angle);
        targetZRotation = angle;
        switchStartZRotation = angle;
        switchTotalAngle = 90f;

        if (carModel != null)
        {
            xOffset = GetLaneValue(Random.Range(0, 3));
            carModel.localPosition = new Vector3(xOffset, -radius, 0);
            currentHorizontalPosition = xOffset;
            targetHorizontalPosition = xOffset;
            initialModelYRotation = carModel.localEulerAngles.y;
            baseModelY = -radius;
            baseModelZ = 0f;
        }

        ScheduleNextSwitch();
        ScheduleNextSteer();

        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized || isDestroyed) return;

        if (carModel == null) return;

        transform.Translate(Vector3.forward * forwardSpeed * Time.deltaTime, Space.Self);
        UpdateEngineAudio();

        if (Time.time >= nextSwitchTime)
        {
            float newTargetZ = targetZRotation + (Random.value > 0.5f ? 90f : -90f);
            BeginSwitch(newTargetZ);
            ScheduleNextSwitch();
        }

        if (Time.time >= nextSteerTime)
        {
            int currentLane = GetClosestLaneIndex(currentHorizontalPosition);
            int laneStep = Random.value > 0.5f ? 1 : 2;
            int nextLane = (currentLane + laneStep) % 3;
            targetHorizontalPosition = GetLaneValue(nextLane);

            ScheduleNextSteer();
        }

        Vector3 currentAngles = transform.localEulerAngles;
        float effectiveSwitchSpeed = switchSpeed * switchSpeedFactor;
        float newZ = Mathf.MoveTowardsAngle(currentAngles.z, targetZRotation, effectiveSwitchSpeed * Time.deltaTime);
        transform.localEulerAngles = new Vector3(currentAngles.x, currentAngles.y, newZ);

        float angleDiff = Mathf.Abs(Mathf.DeltaAngle(newZ, targetZRotation));
        isSwitching = angleDiff > 1f;

        currentHorizontalPosition = Mathf.MoveTowards(currentHorizontalPosition, targetHorizontalPosition, steerSpeed * Time.deltaTime);

        if (Camera.main != null && transform.position.z < Camera.main.transform.position.z - 15f)
        {
            Destroy(gameObject);
        }
    }

    void LateUpdate()
    {
        if (!isInitialized || carModel == null || isDestroyed) return;

        float switchProgress = GetSwitchProgress(transform.localEulerAngles.z);
        float arc = Mathf.Sin(switchProgress * Mathf.PI);
        float switchY = Mathf.Lerp(baseModelY, baseModelY * (1f - switchArcToCenter), arc);
        carModel.localPosition = new Vector3(currentHorizontalPosition, switchY, baseModelZ);

        float targetAngle = 0f;
        if (Mathf.Abs(targetHorizontalPosition - currentHorizontalPosition) > 0.1f)
        {
            float turnDirection = Mathf.Sign(targetHorizontalPosition - currentHorizontalPosition);
            targetAngle = turnDirection * maxSteerAngle;
        }

        currentSteerAngle = Mathf.LerpAngle(currentSteerAngle, targetAngle, Time.deltaTime * 6f);
        float switchPitch = -switchPitchAngle * arc;
        carModel.localRotation = Quaternion.Euler(switchPitch, initialModelYRotation + currentSteerAngle, 0f);

    }

    void ScheduleNextSwitch()
    {
        nextSwitchTime = Time.time + Random.Range(switchIntervalMin, switchIntervalMax);
    }

    void ScheduleNextSteer()
    {
        nextSteerTime = Time.time + Random.Range(steerIntervalMin, steerIntervalMax);
    }

    private float GetLaneValue(int laneIndex)
    {
        if (laneIndex <= 0) return -maxLaneOffset;
        if (laneIndex == 1) return 0f;
        return maxLaneOffset;
    }

    private int GetClosestLaneIndex(float x)
    {
        float leftDist = Mathf.Abs(x - (-maxLaneOffset));
        float centerDist = Mathf.Abs(x - 0f);
        float rightDist = Mathf.Abs(x - maxLaneOffset);

        if (leftDist <= centerDist && leftDist <= rightDist) return 0;
        if (centerDist <= rightDist) return 1;
        return 2;
    }

    private void BeginSwitch(float newTargetZ)
    {
        switchStartZRotation = transform.localEulerAngles.z;
        targetZRotation = newTargetZ;
        switchTotalAngle = Mathf.Abs(Mathf.DeltaAngle(switchStartZRotation, targetZRotation));
        if (switchTotalAngle < 1f) switchTotalAngle = 90f;
    }

    private float GetSwitchProgress(float currentZ)
    {
        float remaining = Mathf.Abs(Mathf.DeltaAngle(currentZ, targetZRotation));
        return 1f - Mathf.Clamp01(remaining / switchTotalAngle);
    }

    private void ResolveCarModelReference()
    {
        if (carModel != null && carModel.IsChildOf(transform)) return;

        Transform found = transform.Find("Policecar");
        if (found == null && transform.childCount > 0)
            found = transform.GetChild(0);

        carModel = found;
    }

    public void ExplodeAndDestroy()
    {
        if (isDestroyed) return;
        isDestroyed = true;
        isInitialized = false;

        Vector3 explosionPos = carModel != null ? carModel.position : transform.position;
        SpawnExplosion(explosionPos);

        if (engineSource != null)
            engineSource.Stop();

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        if (carModel != null)
            carModel.gameObject.SetActive(false);

        Destroy(gameObject, explosionLifetime);
    }

    private void SpawnExplosion(Vector3 worldPosition)
    {
        if (explosionPrefab == null) return;

        GameObject fx = Instantiate(explosionPrefab, worldPosition, Quaternion.identity);
        Destroy(fx, explosionLifetime + 0.5f);
    }

    private void SetupEngineAudio()
    {
        if (engineClip == null) return;

        if (engineSource == null)
        {
            engineSource = GetComponent<AudioSource>();
            if (engineSource == null)
                engineSource = gameObject.AddComponent<AudioSource>();
        }

        engineSource.clip = engineClip;
        engineSource.loop = true;
        engineSource.playOnAwake = false;
        engineSource.spatialBlend = 1f;
        engineSource.minDistance = 8f;
        engineSource.maxDistance = 60f;
        engineSource.volume = engineVolume;
        engineSource.pitch = Random.Range(minEnginePitch, maxEnginePitch);

        if (!engineSource.isPlaying)
            engineSource.Play();
    }

    private void UpdateEngineAudio()
    {
        if (engineSource == null) return;

        float speedNormalized = Mathf.InverseLerp(5f, 30f, forwardSpeed);
        float targetPitch = Mathf.Lerp(minEnginePitch, maxEnginePitch, speedNormalized);
        engineSource.pitch = Mathf.Lerp(engineSource.pitch, targetPitch, Time.deltaTime * 3f);
    }
}