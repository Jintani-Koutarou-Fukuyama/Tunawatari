using UnityEngine;

public class BreakBuilding : MonoBehaviour
{
    public BreakablePart[] parts;
    public float debrisLifetime = 3f; // 破片が残る時間（秒）

   

    public GameObject breakSoundPrefab; 

    public void Break(Vector3 hitPos)
    {
        foreach (var p in parts)
        {
            p.Break(hitPos, 12f);
            Destroy(p.gameObject, debrisLifetime);
        }

        //破壊音専用オブジェクトを生成して音を鳴らす
        if (breakSoundPrefab != null)
        {
            Instantiate(breakSoundPrefab, transform.position, Quaternion.identity);
        }

        // 建物を破壊
        Destroy(gameObject);
    }


    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            Break(transform.position);
        }
    }
}
