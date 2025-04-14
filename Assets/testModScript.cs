using System.Collections;
using UnityEngine;

public class testModScript : MonoBehaviour
{
    public KMAudio Audio;
    public KMBombInfo BombInfo;
    public KMBombModule Module;

    public KMSelectable Button;

    private void Start()
    {
        Button.OnInteract += ButtonPress;
    }

    private bool ButtonPress()
    {
        Module.HandlePass();
        return false;
    }

#pragma warning disable 414
    private string TwitchHelpMessage = "!{0} solve to test Forget Enigma!";
#pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        yield break;
    }
}
