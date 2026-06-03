using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public Canvas menu;
    public Camera[] renderCams;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Update is called once per frame
    void Update()
    {
        //if the menu's not visible, allow mouse to act as player's view and disable render cam
        if (menu.enabled == false)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            for (int i = 0; i < renderCams.Length; i++)
            {
                renderCams[i].enabled = false;
            }
        }
        //if menu's enabled, allow mouse to interact
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
