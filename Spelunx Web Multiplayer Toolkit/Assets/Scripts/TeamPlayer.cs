// TeamPlayer.cs
// Top-down Rigidbody3D player controller driven by relay input.
// Movement is on the XZ plane. Jump pops on Y with gravity.

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class TeamPlayer : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed    = 6f;
    public float acceleration = 20f;
    public float deceleration = 25f;

    [Header("Jump")]
    public float jumpForce      = 7f;
    [Tooltip("Extra downward force while airborne — makes the arc feel snappier")]
    public float fallMultiplier = 3f;
    [Tooltip("Sphere cast radius for ground check")]
    public float groundCheckRadius = 0.25f;
    [Tooltip("Layer(s) considered ground")]
    public LayerMask groundLayer = ~0;   // default: everything

    [Header("Visuals")]
    public Renderer bodyRenderer;

    // ---- Set by spawner ----
    public string     Team       { get; private set; }
    public HostClient HostClient { get; private set; }

    // ---- Internal ----
    Rigidbody _rb;
    bool      _isGrounded;
    bool      _jumpConsumed;   // prevents holding jump from multi-firing

    static readonly Dictionary<string, Color> TeamColors = new()
    {
        { "red",   new Color(0.85f, 0.15f, 0.05f) },
        { "blue",  new Color(0.05f, 0.30f, 0.90f) },
        { "green", new Color(0.00f, 0.75f, 0.55f) }
    };

    // ============================================================
    public void Init(string team, HostClient hostClient)
    {
        Team       = team;
        HostClient = hostClient;

        if (bodyRenderer != null && TeamColors.TryGetValue(team, out Color c))
        {
            var mat = bodyRenderer.material;
            if      (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     c);
        }

        gameObject.name = $"Player_{team}";
    }

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        // Only freeze rotation — Y is free so jump works
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
        _rb.useGravity  = true;
    }

    // ============================================================
    void FixedUpdate()
    {
        if (HostClient == null || string.IsNullOrEmpty(Team)) return;

        CheckGround();

        Vector2 dir    = GetTeamDirection();
        bool    doJump = GetTeamJump();

        ApplyMovement(dir);
        ApplyJump(doJump);
        ApplyFallGravity();

        // Face movement direction (XZ only)
        if (dir.sqrMagnitude > 0.01f)
        {
            var lookDir = new Vector3(dir.x, 0f, dir.y);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(lookDir),
                Time.fixedDeltaTime * 12f
            );
        }
    }

    // ============================================================
    void CheckGround()
    {
        // SphereCast downward from the base of the collider
        Vector3 origin = transform.position + Vector3.up * groundCheckRadius;
        _isGrounded = Physics.SphereCast(
            origin,
            groundCheckRadius,
            Vector3.down,
            out _,
            groundCheckRadius + 0.05f,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );
    }

    // ============================================================
    // Aggregate input from all players on this team
    Vector2 GetTeamDirection()
    {
        float x = 0f, y = 0f;
        foreach (var player in HostClient.Players.Values)
        {
            if (player.team != Team) continue;
            Vector2 d = player.DirectionVector;
            if (Mathf.Abs(d.x) > Mathf.Abs(x)) x = d.x;
            if (Mathf.Abs(d.y) > Mathf.Abs(y)) y = d.y;
        }
        var v = new Vector2(x, y);
        return v.magnitude > 1f ? v.normalized : v;
    }

    // Any player on the team pressing jump this frame triggers a jump
    bool GetTeamJump()
    {
        foreach (var player in HostClient.Players.Values)
            if (player.team == Team && player.jumpPressed) return true;
        return false;
    }

    // ============================================================
    void ApplyMovement(Vector2 dir)
    {
        Vector3 target  = new Vector3(dir.x, 0f, dir.y) * moveSpeed;
        float   rate    = dir.sqrMagnitude > 0.01f ? acceleration : deceleration;
        Vector3 current = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        Vector3 next    = Vector3.MoveTowards(current, target, rate * Time.fixedDeltaTime);

        // Preserve Y velocity so gravity and jump aren't overwritten
        _rb.linearVelocity = new Vector3(next.x, _rb.linearVelocity.y, next.z);
    }

    void ApplyJump(bool doJump)
    {
        if (doJump && _isGrounded && !_jumpConsumed)
        {
            // Zero out any downward velocity before applying impulse
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
            _jumpConsumed = true;
        }

        // Reset consumed flag once we leave the ground and come back
        if (!_isGrounded) _jumpConsumed = false;
    }

    // Extra downward gravity while falling — makes the arc feel less floaty
    void ApplyFallGravity()
    {
        if (!_isGrounded && _rb.linearVelocity.y < 0f)
        {
            _rb.AddForce(Vector3.down * Physics.gravity.magnitude * (fallMultiplier - 1f),
                         ForceMode.Acceleration);
        }
    }
}