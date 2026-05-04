using UnityEngine;

public class WheelRotator : MonoBehaviour
{
    [Header("Колеса для вращения")]
    public Transform[] wheels;

    [Header("Настройки")]
    public float rotationSpeed = 1000f;

    public Vector3 rotationAxis = new Vector3(1, 0, 0);

    void Update()
    {
        foreach (Transform wheel in wheels)
        {
            if (wheel != null)
            {
                wheel.Rotate(rotationAxis * rotationSpeed * Time.deltaTime, Space.Self);
            }
        }
    }
}