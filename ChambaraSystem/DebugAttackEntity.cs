
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace SilentTools
{
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DebugAttackEntity : UdonSharpBehaviour
{
    public AttackManager attackManager;
    private bool attackManagerOK = false;

    
    public void Start()
    {
        if (attackManager != null)
        {
            attackManagerOK = true;
        }
    }

    //used if the collider is a trigger.
    public override void OnPlayerTriggerEnter(VRC.SDKBase.VRCPlayerApi player)
    {
        Debug.Log("OnPlayerTriggerEnter! " + attackManagerOK);

        applyAttack(player);
    }

    //used if the collider is not a trigger.
    public override void OnPlayerCollisionEnter(VRC.SDKBase.VRCPlayerApi player)
    {
        Debug.Log("OnPlayerCollisionEnter! " + attackManagerOK);

        applyAttack(player);
    }

    private void applyAttack(VRC.SDKBase.VRCPlayerApi player)
    {
        if (attackManagerOK == true && player.isLocal)
        {
            attackManager._Attacked(new Vector3(0.0f, 10.0f, 0.0f), this);
        }
    }
}
}
