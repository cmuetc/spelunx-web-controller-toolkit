using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Mono.Cecil.Cil;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZXing;
using ZXing.QrCode;

public class LanAddressDisplay : MonoBehaviour
{
    [Header("UI Settings")]
    public TextMeshProUGUI ipText;
    public TextMeshProUGUI roomCodeText;

    [Header("QR Code for IP(Optional and you have to provide your own QR code. The script will not generate one for you)")]
    public RawImage QRCodeImage;

    [Header("Relay info")]
    public string controllerPath = "/controller.html";

    [Header("HostClient")]
    public HostClient hostClient;

    // Cached values for optimization
    private string ipAddress;
    private string roomCode;
    private int relayPort;

    void Start()
    {
        //relayPort = hostClient.relayPort;
        //showRoomCode();
        //if (!QRCodeImage) Render();

        // Update the displayed LAN IPs and room code every 0.5 seconds (Important delay for the host to get the room code from the relay)
        //InvokeRepeating(nameof(showRoomCode), 0.5f, 0.5f);
        StartCoroutine(UpdateLanInfo());
    }

    IEnumerator UpdateLanInfo()
    {
        while (true)
        {
            // Wait until the host client has a room code before updating the display
            if (hostClient == null || String.IsNullOrEmpty(hostClient.RoomCode))
            {
                yield return new WaitForSeconds(0.5f);
            }

            // Update the cached values
            ipAddress = GetPrimaryIPv4Address();
            roomCode = hostClient.RoomCode;
            relayPort = hostClient.relayPort;

            // Update the server URL and QR code
            if (String.IsNullOrEmpty(ipAddress))
            {
                ipText.text = "No LAN IPv4 found. Is Wi-Fi/Ethernet connected?";
            }
            else
            {
                string server_url = $"http://{ipAddress}:{relayPort}{controllerPath}";
                if (ipText != null)
                {
                    ipText.text = server_url;
                }

                if (QRCodeImage != null)
                {
                    Texture2D qrcode = GenerateQRCode($"{server_url}?code={roomCode}");
                    QRCodeImage.texture = qrcode;
                }
            }


            // Update the displayed LAN IPs and room code
            if (roomCodeText != null)
            {
                roomCodeText.text = $"Room Code: {roomCode}";
            }

            // Exit coroutine
            yield break;
        }
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
        string ip = GetPrimaryIPv4Address();
        if (hostClient.isRemoted) ip = hostClient.relayHost;
        var code = hostClient != null ? (hostClient.RoomCode ?? "") : "";

        if (ip == "")
        {
            ipText.text = "No LAN IPv4 found. Is Wi-Fi/Ethernet connected?";
        }
        else
        {   
            string server_url = $"http://{ip}:{relayPort}{controllerPath}";
            ipText.text = server_url;
            Texture2D qrcode = GenerateQRCode($"{server_url}?code={code}");
            QRCodeImage.texture = qrcode;
        }
        
    }

    [Obsolete("This method has been deprecated. Use GetPrimaryIPv4Address instead.")]
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

    /// <summary>
    /// Get's the server's public-facing IPv4 address.
    /// </summary>
    /// <returns>The IPv4 address as a string</returns>
    /// <remarks>
    /// Gleened from https://stackoverflow.com/questions/6803073/get-local-ip-address
    /// </remarks>
    public static string GetPrimaryIPv4Address()
    {
        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, 0);
        // Connect to a common public IP (Google DNS) to trigger route selection
        socket.Connect("8.8.8.8", 65530);
        IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
        return endPoint?.Address.ToString();
    }

    public static Texture2D GenerateQRCode(string text, int width = 256, int height = 256)
    {
        var qrcode = new Texture2D(width, height);

        var writer = new BarcodeWriter
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions
            {
                Width = width,
                Height = height
            }
        };

        var color32 = writer.Write(text);
        qrcode.SetPixels32(color32);
        qrcode.Apply();

        return qrcode;
    }
}