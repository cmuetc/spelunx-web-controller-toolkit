using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using TMPro;
using UnityEngine;

public class LanAddressDisplay : MonoBehaviour
{
    [Header("UI Settings")]
    public TextMeshProUGUI ipText;
    public TextMeshProUGUI roomCodeText;
    [Header("QR Code for IP(Optional and you have to provide your own QR code. The script will not generate one for you)")]
    public GameObject QRCodeImage;

    [Header("Relay info")]
    public string controllerPath = "/controller.html";
    private int relayPort;

    [Header("HostClient")]
    public HostClient hostClient;


    void Start()
    {
        relayPort = hostClient.relayPort;
        showRoomCode();
        if(!QRCodeImage) Render();

        // Update the displayed LAN IPs and room code every 0.5 seconds (Important delay for the host to get the room code from the relay)
        InvokeRepeating(nameof(showRoomCode), 0.5f, 0.5f);
    }

    void showRoomCode()
    {
        if (roomCodeText != null && hostClient != null)
        {

            roomCodeText.text = "Room Code: " + hostClient.RoomCode;
        }
    }

    void Render()
    {
        string ip = GetLanIPv4();
        if(hostClient.isRemoted) ip = hostClient.relayHost;
        var code = hostClient != null ? (hostClient.RoomCode ?? "") : "";

        if (ip == "")
        {
            ipText.text = "No LAN IPv4 found. Is Wi-Fi/Ethernet connected?";
        }
        else
        {
            ipText.text = $"{ip}:{relayPort}{controllerPath}\n";
        }

    }


    public static string GetLanIPv4()
    {
        string localIp = "";
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;

            // Prefer typical physical adapters:
            if (ni.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Ethernet)
                continue;

            var ipProps = ni.GetIPProperties();
            foreach (var ua in ipProps.UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue; // IPv4 only
                var ip = ua.Address;
                if (IPAddress.IsLoopback(ip)) continue;

                var b = ip.GetAddressBytes();
                if (b.Length == 4 && b[0] == 169 && b[1] == 254) continue; // skip APIPA 169.254.x.x
                localIp += ip.ToString();
            }
        }

        return localIp;
    }
}