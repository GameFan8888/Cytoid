using UnityEngine;
using UnityEngine.UI;

public class LevelCharterText : MonoBehaviour, ScreenBecameActiveListener
{
    [GetComponent] public Text text;
    public void OnScreenBecameActive()
    {
        text.text = Context.SelectedLevel?.Meta.charter ?? "Unknown";
    }
}