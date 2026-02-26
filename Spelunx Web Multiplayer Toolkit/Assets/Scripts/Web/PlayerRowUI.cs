using UnityEngine;

public class PlayerRowUI : MonoBehaviour
{
    public TMPro.TMP_Text nameTMP;
    public UnityEngine.UI.Text nameText; 

    public TMPro.TMP_Text idTMP;
    public UnityEngine.UI.Text idText;

    public void Set(string displayName, string id)
    {
        if (nameTMP) nameTMP.text = displayName;  if (nameText) nameText.text = displayName;
        if (idTMP)   idTMP.text   = id;           if (idText)   idText.text   = id;
    }
}