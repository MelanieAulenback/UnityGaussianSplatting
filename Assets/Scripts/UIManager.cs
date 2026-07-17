using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public Canvas menu;
    public Button startButton;
    public SplatAnimator animator;
    private Camera[] renderCams;

    public static bool startAnimation;

    void Start()
    {
        //free range mouse
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        startAnimation = false;
    }

    void Update()
    {
        //if the menu's not visible, allow mouse to act as player's view and disable render cam
        if (menu.enabled == false)
        {
            renderCams = animator.renderCameras;
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

        //if the dataset isn't loaded, disable start button
        if (!startAnimation)
        {
            startButton.interactable = false;
            ColorBlock colors = startButton.colors;
            colors.normalColor = Color.gray;
            startButton.colors = colors;
        }

        //if the dataset is loaded, enable start button
        if (startAnimation)
        {
            startButton.interactable = true;
            ColorBlock colors = startButton.colors;
            colors.normalColor = Color.white;
            startButton.colors = colors;
        }
    }
}
