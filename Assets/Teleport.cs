using UnityEngine;

public class ViewpointSwitcher : MonoBehaviour
{
    public Transform[] spawnPoints;
    int current = 0;

    void Start() { Teleport(0); }

    void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.One) || Input.GetKeyDown(KeyCode.N))
        {
            current = (current + 1) % spawnPoints.Length;
            Teleport(current);
        }
    }

    void Teleport(int i)
    {
        transform.SetPositionAndRotation(spawnPoints[i].position, spawnPoints[i].rotation);
    }
}