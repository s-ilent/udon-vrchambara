#define CHAMBARA_DEBUG_MODE 

using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.SDK3.Components;

namespace SilentTools
{
[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class MeleeWeaponEntity : UdonSharpBehaviour
{
    [Header("Settings")]
    [Tooltip("Intensity multiplier for attack swings.")] public float attackPower = 1.0f; 
    [Tooltip("Whether this collider affects people not holding weapons.")]public bool attackNonCombatants = false;

    [Header("Dependencies")]
    [Tooltip("AttackManager component, used to affect players.")] public AttackManager attackManager;
    private bool attackManagerOK = false;

    [Tooltip("Rigidbody component, used for momentum.")] public Rigidbody localRigidbody;
    private bool localRigidbodyOK = false;

    [Tooltip("AudioSource component, used for sound effects.")] public AudioSource localAudioSource;
    private bool localAudioSourceOK = false;
    
    [Tooltip("Object sync component, used for resetting.")] public VRCObjectSync localVRCPickup;
    private bool localVRCPickupOK = false;

    [Header("Sound Effects")]
    public AudioClip hitAttemptAudioClip;
    public AudioClip hitConfirmAudioClip;

    // Used to determine whether the local player is holding the weapon.
    // If they are, the sword attacks aren't sent. (defender authority)
    private bool weaponOwnerIsLocalPlayer = false;
    private VRC.SDKBase.VRCPlayerApi localOwner;
    private bool localOwnerCached = false;

    // Used to determine whether the sword has been held this session.
    private bool localIsHeld = false;
    private bool localWasHeldThisSession = false;

    private double absoluteDropTimer = 0.0;

    public Vector3 debugShow;


    /* Notes on rigidbody setup
    Rigidbody should be on the tip of the weapon, with all position/rotation constraints
    active, NOT kinematic, no gravity. No angular drag, apparently mass is OK.
    Using speculative collision detection. I don't know if that helps.
    In practise, we (Boatfloater helped a lot) can't get this working. 
    */

    /* Notes on networking...
    When values are synched, OnDeserialized is called
    so we can set a velue remotely and act on it when OnDeserialized?
    */

    /* Todo notes
    When striking the opponent's sword, it should count as an attack towards the player. 
    However, to follow the mechanic of the original game, it must be on the normal of the sword.
    Hmm...
    */
    public void Start()
    {
        // Make sure AttackManager exists
        if (attackManager != null)
        {
            attackManagerOK = true;
        }

        // Try getting rigidbody if none exists
        if (localRigidbody == null)
        {
            localRigidbody = GetComponent<Rigidbody>();
        }
        if (localRigidbody != null)
        {
            localRigidbodyOK = true;
        }

        // Try getting audiosource if none exists
        if (localAudioSource == null)
        {
            localAudioSource = GetComponent<AudioSource>();
        }
        if (localAudioSource != null)
        {
            localAudioSourceOK = true;
        }
        
        // Try getting pickup component
        if (localVRCPickup == null)
        {
            localVRCPickup = GetComponent<VRCObjectSync>();
        }
        if (localVRCPickup != null)
        {
            localVRCPickupOK = true;
        }
    }

    public void Update()
    {   
        // Respawn when absoluteDropTimer is in the past
        double utcTime = Convert.ToDouble(DateTime.UtcNow.Ticks) / 1E+07D;
        if (absoluteDropTimer > 0 && utcTime > absoluteDropTimer && localVRCPickupOK && localWasHeldThisSession)
        {
            localVRCPickup.Respawn();
            absoluteDropTimer = 0.0;
        }
    }

    // handle OnPickup and OnDrop...

    public override void OnPickup()
    {
        localIsHeld = true;
        localWasHeldThisSession = true;
        // Call update holding to disable double jump/etc
        attackManager._UpdateHolding();
    }

    public override void OnDrop()
    {
        localIsHeld = false;
        absoluteDropTimer = Convert.ToDouble(DateTime.UtcNow.AddMinutes(3).Ticks) / 1E+07D;
        if (attackManagerOK == true)
        {
            attackManager._UpdateHolding();
            attackManager.SendCustomEventDelayedSeconds("_UpdateHolding", 3.0f);
        }
    }

    public override void OnOwnershipTransferred(VRC.SDKBase.VRCPlayerApi player)
    {
        #if CHAMBARA_DEBUG_MODE
        Debug.Log($"{this.transform.name} OnOwnershipTransferred! by {player.displayName}");
        #endif
        _UpdateWeaponOwnership(player);
    }

    private void  _UpdateWeaponOwnership(VRC.SDKBase.VRCPlayerApi player)
    {
        if (Utilities.IsValid(player))
        {
            // if the player is local and not master OR they are local and master and holding 
            if (player.isLocal)
            {
                weaponOwnerIsLocalPlayer = true;
            }
            else
            {
                weaponOwnerIsLocalPlayer = false;
            }
        } else
        {
            
            weaponOwnerIsLocalPlayer = false;
        }
        localOwner = player;
        localOwnerCached = true;
    }
    

    //used if the collider is a trigger.
    public override void OnPlayerTriggerEnter(VRC.SDKBase.VRCPlayerApi player)
    {
        #if CHAMBARA_DEBUG_MODE
        Debug.Log($"{this.transform.name} OnPlayerTriggerEnter! by {player.displayName} attackManagerOK {attackManagerOK}");
        #endif

        applyAttack(player);
    }

    //used if the collider is not a trigger.
    // As far as I can tell, players don't really "enter" solid colliders. 
    public override void OnPlayerCollisionEnter(VRC.SDKBase.VRCPlayerApi player)
    {
        #if CHAMBARA_DEBUG_MODE
        Debug.Log($"{this.transform.name} OnPlayerCollisionEnter! by {player.displayName} attackManagerOK {attackManagerOK}");
        #endif

        applyAttack(player);
    }

    private void _UpdateWeaponOwnershipOnHit(VRC.SDKBase.VRCPlayerApi player)
    {
        if (localOwnerCached)
        {
            
        }
    }

    // Apply attack to player, either local or remote.
    // When the local player is attacked, they will call the attack trigger.
    // When a remote player is attacked, cosmetic effects are played (SFX).
    private void applyAttack(VRC.SDKBase.VRCPlayerApi player)
    {
        if (attackManagerOK == true)
        {
            #if CHAMBARA_DEBUG_MODE
            Debug.Log($"{this.transform.name} applyAttack by {player.displayName} player.isLocal {player.isLocal} weaponOwnerIsLocalPlayer {weaponOwnerIsLocalPlayer} localIsHeld {localIsHeld}");
            #endif
            // when another player hits you
            if (player.isLocal && (attackNonCombatants || (weaponOwnerIsLocalPlayer == false)) && localIsHeld == false)
            {
                #if CHAMBARA_DEBUG_MODE
                Debug.Log($"{this.transform.name} is striking {player.displayName}");
                #endif
                Vector3 finalAttackPower = new Vector3(0,0,0);
                Vector3 pushbackVector = new Vector3(0,1,0);

                // direction vector from object to player
                Vector3 basePushbackVector = new Vector3(0,1,0);

                // velocity of sword rigidbody
                Vector3 swordVelocity = new Vector3(0,0,0);

                float velocityMult = 0.1f;

                if (Utilities.IsValid(player))
                {
                    Vector3 playerPosition = player.GetBonePosition(HumanBodyBones.Chest);
                    Vector3 pushbackBiasHumanoid = new Vector3(0.0f, 0.25f, 0.0f);
                    Vector3 pushbackBiasOther = new Vector3(0.0f, 1.5f, 0.0f);
                    // If the bone doesn't exist, the position is zero. But humanoids should
                    // always have a chest. 
                    bool isPlayerHumanoid = playerPosition == Vector3.zero;
                    // If the player is humanoid, then the pushback vector should follow their chest
                    // with a small bias upwards for bounce. 
                    // If they aren't, then it follows their position - AFAIK their base, so the
                    // bias upwards needs to be much higher. 
                    playerPosition = isPlayerHumanoid ? playerPosition : player.GetPosition();
                    Vector3 pushbackBias = isPlayerHumanoid ? pushbackBiasHumanoid : pushbackBiasOther;

                    pushbackVector = player.GetPosition() - this.transform.position;
                    pushbackVector = pushbackVector + pushbackBias;

                    basePushbackVector = pushbackVector;
                    
                    if (localRigidbodyOK)
                    {
                        debugShow = localRigidbody.angularVelocity;
                        // Use the angular velocity of the rigidbody. 
                        // Ideally using one on the tip of the weapon.
                        swordVelocity = localRigidbody.angularVelocity;
                        // Pushback vector is normalized later, so the multiplier is more to
                        // ensure that a valid velocity overrides the basic pushback direction. 
                        pushbackVector = pushbackVector + swordVelocity * 10f;
                        float minVelocity = 0.1f;
                        velocityMult = Mathf.Max(minVelocity, localRigidbody.angularVelocity.magnitude);
                    }
                    // This is the direction the player is thrown back to.
                    pushbackVector =  Vector3.Normalize(pushbackVector);
                    // If all else fails, push the player back 1 unit. 
                    finalAttackPower = pushbackVector;
                }

                // 1f is too weak. 10f seems too strong. Maybe 5f.
                finalAttackPower = pushbackVector * velocityMult * 50f * attackPower;

                #if CHAMBARA_DEBUG_MODE
                Debug.Log($"{this.transform.name} applyAttack basePushbackVector {basePushbackVector} swordVelocity {swordVelocity} finalAttackPower {finalAttackPower}");
                #endif

                // Sound effects handling note
                // Attacked will call playHitSE on this object. However, there is a delay between the 
                // hit registering and the hit applying. So, we play a swinging SE before it's confirmed.
                playSwingSE();

                if (attackNonCombatants) 
                {
                    attackManager._AttackedIndiscriminate(finalAttackPower, this);
                }
                else
                {
                    attackManager._Attacked(finalAttackPower, this);
                }
            }
            // when hitting another player with your sword
            if (player.isLocal == false && weaponOwnerIsLocalPlayer == true && localIsHeld == true)
            {
                #if CHAMBARA_DEBUG_MODE
                Debug.Log($"{this.transform.name} is whiffing {player.displayName}");
                #endif
                playSwingSE();
                // Don't play hit SE sound here, the hit player will themself.
            }
        }
    }

    public void playSwingSE()
    {
        if (localAudioSourceOK)
        {
            localAudioSource.pitch = UnityEngine.Random.Range(0.99f, 1.3f);
            // We'd like to use the function to play the sound at a world space position
            // but it is bugged in VRC and doesn't work properly.
            // localAudioSource.PlayOneShot(localAudioSource.clip, 1.0f);   
            localAudioSource.PlayOneShot(hitAttemptAudioClip, 1.0f);   
        }
    }

    public void playHitSE()
    {
        if (localAudioSourceOK)
        {
            localAudioSource.pitch = UnityEngine.Random.Range(0.99f, 1.3f);
            // localAudioSource.PlayOneShot(localAudioSource.clip, 1.0f);   
            localAudioSource.PlayOneShot(hitConfirmAudioClip, 1.0f);   
        }
    }
}
}
