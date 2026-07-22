using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public abstract class DebrisBase : MonoBehaviour
{
    [Header("Debris Audio")]
    [Tooltip("Sound to play when the debris is clicked/damaged.")]
    public AudioClip clickSfx;
    [Tooltip("Sound to play when the debris is completely destroyed.")]
    public AudioClip destroySfx;
    public AudioSource audioSource;

    [Header("Events")]
    public UnityEvent OnDebrisDestroyed;

    protected virtual void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        
        if (string.IsNullOrEmpty(gameObject.tag) || gameObject.tag == "Untagged")
        {
            gameObject.tag = "Debris";
        }
    }

    protected virtual void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            CheckForMouseClick();
        }
    }

    private void CheckForMouseClick()
    {
        if (Camera.main == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            DebrisBase clickedDebris = hit.collider.GetComponentInParent<DebrisBase>();
            if (clickedDebris == this)
            {
                OnClicked();
            }
        }
    }
    
    protected abstract void OnClicked();

    protected void PlayClickSound()
    {
        if (audioSource != null && clickSfx != null)
        {
            audioSource.PlayOneShot(clickSfx);
        }
    }

    protected void TriggerDestruction()
    {
        OnDebrisDestroyed?.Invoke();
        
        if (destroySfx != null)
        {
            AudioSource.PlayClipAtPoint(destroySfx, transform.position);
        }

        Destroy(gameObject);
    }
}
