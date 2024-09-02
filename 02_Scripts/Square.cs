using UnityEngine;

public class Square : MonoBehaviour
{
    [SerializeField]
    private string m_AssetKey;
    // Start is called before the first frame update
    void Start()
    {
        GetComponent<SpriteRenderer>().sprite = AssetManager.LoadSprite(m_AssetKey);
    }

}
