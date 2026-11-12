using UnityEngine;

/// <summary>
/// Sub-component anexado ao GameObject do colisor de gatilho do God Ray.
/// Encaminha os eventos de física (OnTriggerEnter / OnTriggerExit) para o GodRayBeam principal.
/// </summary>
public class GodRayTriggerZone : MonoBehaviour
{
    private GodRayBeam parentBeam;

    public void Initialize(GodRayBeam beam)
    {
        parentBeam = beam;
    }

    private void Awake()
    {
        if (parentBeam == null)
        {
            parentBeam = GetComponentInParent<GodRayBeam>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (parentBeam != null)
        {
            parentBeam.NotifyTriggerEnter(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (parentBeam != null)
        {
            parentBeam.NotifyTriggerExit(other);
        }
    }
}
