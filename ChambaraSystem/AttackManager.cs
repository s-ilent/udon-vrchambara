#define CHAMBARA_DEBUG_MODE 

using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
// if Airtime is used...
using Airtime.Player.Movement;

namespace SilentTools
{
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class AttackManager : UdonSharpBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Animator component, used for controlling effects.")]public Animator localAnimator;
    private bool localAnimatorCached;
    [Tooltip("Airtiem Playrr Controller component, used for controlling players.")]public PlayerController localAirtimePC;
    private bool localAirtimePCCached;

    private Vector3 lastVelocity;
    private bool couldDoubleJump = false;

    [Header("Settings")]
    public float cooldownTimerLength = 3.0f;
    public float attackThreshold = 0.25f;

    private bool canBeAttacked = false;
    private float attackCooldownTimer = 3.0f;

    private bool isHoldingLeft = false;
    private bool isHoldingRight = true;

/*
I could have a particle system that moves to the attack location and emits particles?

*/

    // VRC stuff
    private VRCPlayerApi localPlayer;
    private bool localPlayerCached = false;

    public void Start()
    {
        localPlayer = Networking.LocalPlayer;
        if (localPlayer != null)
        {
            localPlayerCached = true;
        }

        localAnimator = GetComponent<Animator>();
        if (localAnimator != null)
        {
            localAnimatorCached = true;
        }

        if (localAirtimePC != null)
        {
            localAirtimePCCached = true;
            couldDoubleJump = localAirtimePC.doubleJumpEnabled;
        }
    }

    public void Update()
    {
        attackCooldownTimer = Mathf.Max(-1, attackCooldownTimer - Time.deltaTime);
    }

    private bool CheckIfHoldingWeapon_internal()
    {
        if (attackCooldownTimer >= 0.0f)
        {
            return true;
        }
        if (localPlayerCached && Utilities.IsValid(localPlayer) && localPlayer.IsValid())
        {
            // If the player is holding a weapon, they can be attacked
            VRC_Pickup leftHeld = localPlayer.GetPickupInHand(VRC_Pickup.PickupHand.Left);
            VRC_Pickup rightHeld = localPlayer.GetPickupInHand(VRC_Pickup.PickupHand.Right);
            
            if ((leftHeld != null) || (rightHeld != null))
            {
                attackCooldownTimer = cooldownTimerLength;
                return true;
            }
        }
        return false;
    }

    private bool CheckIfHoldingWeapon()
    {
        bool result = CheckIfHoldingWeapon_internal();
        if (localAirtimePCCached == true)
        {
            // set double jump
            localAirtimePC.doubleJumpEnabled = result? false : couldDoubleJump;
        }
        return result;
    }

    public void _Attacked(Vector3 velocity, UdonSharpBehaviour attacker)
    {
        #if CHAMBARA_DEBUG_MODE
        Debug.Log("Attacked! " + velocity);
        #endif
        if (velocity.magnitude > attackThreshold && localAnimatorCached && CheckIfHoldingWeapon())
        {
            lastVelocity += velocity;
            localAnimator.SetTrigger("OnHit");
            attacker.SendCustomNetworkEvent(NetworkEventTarget.All, "playHitSE");
        }
    }

    public void _AttackedIndiscriminate(Vector3 velocity, UdonSharpBehaviour attacker)
    {
        #if CHAMBARA_DEBUG_MODE
        Debug.Log("AttackedIndiscriminate! " + velocity);
        #endif
        if (velocity.magnitude > attackThreshold && localAnimatorCached)
        {
            lastVelocity += velocity;
            localAnimator.SetTrigger("OnHit");
            attacker.SendCustomNetworkEvent(NetworkEventTarget.All, "playHitSE");
        }
    }

    public void _UpdateHolding()
    {
        CheckIfHoldingWeapon();
    }

    public void _OnHit1()
    {
        if (localPlayerCached && Utilities.IsValid(localPlayer))
        {
            // Sometimes the player isn't immobilised properly. 
            // Teleport them upwards to make them responsize to movements.
            //localPlayer.SetVelocity(new Vector3(0f, 0.1f, 0f));
            localPlayer.TeleportTo(localPlayer.GetPosition() + new Vector3(0f, 0.01f, 0f), localPlayer.GetRotation());
            localPlayer.Immobilize(true);
        }
    }

    public void _OnHit2()
    {
        if (localPlayerCached && Utilities.IsValid(localPlayer))
        {
            localPlayer.Immobilize(false);
            localPlayer.SetVelocity(lastVelocity);
            lastVelocity = Vector3.zero;
        }
    }
}
}
