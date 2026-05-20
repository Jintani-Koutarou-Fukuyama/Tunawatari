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
            // Aキーが接続されていたら
            if(Keyboard.current.aKey.isPressed)
            {
                Debug.Log("Aキーが押されています。");
                // タイトルシーンへ遷移する
                // 注意 : まだシーンリストに追加する作業はしていない
                //SceneManager.LoadScene("");
            }
        }
    }
}
