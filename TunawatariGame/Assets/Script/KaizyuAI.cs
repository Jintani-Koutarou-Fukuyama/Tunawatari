using UnityEngine;
using UnityEngine.AI;
using System.Linq;

public class KaijuFindAndBreak : MonoBehaviour
{
    Animator anim;
    NavMeshAgent agent;

    public float attackDistance = 3f;
    GameObject targetBuilding;
    BreakBuilding targetBreakScript;

    void Start()
    {
        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        FindNewBuilding();
    }

    void Update()
    {
        anim.SetFloat("Speed", agent.velocity.magnitude);

        if (targetBuilding == null)
        {
            FindNewBuilding();
            return;
        }

        float dist = Vector3.Distance(transform.position, targetBuilding.transform.position);

        if (dist < attackDistance)
        {
            agent.isStopped = true;
            anim.SetTrigger("Attack");
        }
        else
        {
            agent.isStopped = false;
            agent.SetDestination(targetBuilding.transform.position);
        }
    }

    void FindNewBuilding()
    {
        var buildings = GameObject.FindGameObjectsWithTag("Building");

        if (buildings.Length == 0)
        {
            targetBuilding = null;
            return;
        }

        targetBuilding = buildings
            .OrderBy(b => Vector3.Distance(transform.position, b.transform.position))
            .FirstOrDefault();

        targetBreakScript = targetBuilding.GetComponent<BreakBuilding>();
    }

    // Attack アニメの途中で呼ぶ（Animation Event）
    public void BreakTargetBuilding()
    {
        if (targetBreakScript != null)
        {
            targetBreakScript.Break(transform.position);
            targetBuilding = null;
            targetBreakScript = null;
            FindNewBuilding();
        }
    }
}
