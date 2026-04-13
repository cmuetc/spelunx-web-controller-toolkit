using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
 

public class SampleGameplay : MonoBehaviour
{
    public PlayerInputRouter router;       // drag your PlayerInputRouter here
    [Header("Player 1")]
    public TextMeshProUGUI p1SliderValueText;

    [Header("Player 2")]
    public TextMeshProUGUI p2messgaeText;

    [Header("Player 3")]
    public GameObject ball;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    void updatePlayer1()
    {
        var p1 = router.hostClient.GetSlot(1);
        if (p1 != null)
        {
            p1SliderValueText.text = $"P1 Slider: {p1.sliderValue:0}";
        }
    }

    void updatePlayer2()
    {
        var p2 = router.hostClient.GetSlot(2);
        if (p2 != null && p2.newMessage)
        {
            p2messgaeText.text = $"Player 2 just said something, only player 4 can see it." + $"\nMessage: {p2.lastMessage}";

            var p4 = router.hostClient.GetSlot(4);
    
        }
    }

    void updatePlayer3()
    {
        var p3 = router.hostClient.GetSlot(3);
        var p1 = router.hostClient.GetSlot(1);
        
        if (p3 != null && p3.actionPressed)
        {
            Debug.Log($"Player 3 pressed the action button! Applying upward force to the ball based on Player 1's slider value.");
            Rigidbody rb = ball.GetComponent<Rigidbody>();
            rb.AddForce(Vector3.up * p1.sliderValue, ForceMode.Impulse);
        }
    }

    /*
    void updatePlayer4()
    {
        var p2 = hostClient.GetSlot(2);
        var p4 = hostClient.GetSlot(4);
        if (p4 != null && p2.newMessage)
        {
            hostClient.SendTextToDisplay(p2.lastMessage, p2.playerName);
        }
    }
    */

    // Update is called once per frame
    void Update()
    {
        updatePlayer1();
        updatePlayer2();
        updatePlayer3();
        // updatePlayer4();
    }
}
