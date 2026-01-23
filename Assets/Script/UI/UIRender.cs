using UnityEngine;

public class UIRender : MonoBehaviour
{
    [SerializeField] GameObject _panel;
    bool _enabled = false;
    // Update is called once per frame
    void Update()
    {
        if(Input.GetButtonDown("Jump"))
        {
            _enabled = !_enabled;
            _panel.SetActive(_enabled);
        }
    }
}
