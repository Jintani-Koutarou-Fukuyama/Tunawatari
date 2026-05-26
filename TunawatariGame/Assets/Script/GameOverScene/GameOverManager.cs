using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // ゲームパッドが接続されているかのチェック
        if(Gamepad.current != null)
        {

        }

        // キーボードが接続されているかのチェック
        if(Keyboard.current != null)
        {
            if(Keyboard.current.aKey.wasPressedThisFrame)
            {

                Debug.Log("Aボタンが押されています。");
                SceneManager.LoadScene("TitleScene");
            }
        }
    }
}
