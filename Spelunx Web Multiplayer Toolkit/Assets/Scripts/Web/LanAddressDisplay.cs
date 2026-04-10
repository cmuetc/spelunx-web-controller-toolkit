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


    /// <summary>
    /// Waits until the room code is available then updates the display.
    /// </summary>
    IEnumerator UpdateLanInfo()
    {
        while (true)
        {
            // Wait until the host client has a room code before updating the display
            if (hostClient == null || String.IsNullOrEmpty(hostClient.RoomCode))
            {
                yield return new WaitForSeconds(1.0f);
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


    /// <summary>
    /// Generates a QR code texture from the given text using the ZXing library.
    /// </summary>
    /// <param name="text">The text to encode in the QR code.</param>
    /// <param name="width">The width of the generated QR code texture.</param>
    /// <param name="height">The height of the generated QR code texture.</param>
    /// <returns>A Texture2D containing the generated QR code.</returns>
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