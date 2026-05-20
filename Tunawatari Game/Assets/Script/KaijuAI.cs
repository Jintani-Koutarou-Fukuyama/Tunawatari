using UnityEngine;
using System.Linq;
using System.Collections;

public class KaijuAI : MonoBehaviour
{
    Animator anim;

    public float moveSpeed = 1.0f;
    public float rotateSpeed = 5.0f;

    public float attackDistance = 4.5f;

    public float attackCooldown = 2f;
    float attackTimer = 0f;

    GameObject targetBuilding;
    BreakBuilding targetBreakScript;

    bool isAttacking = false;

    float fixedY;

    public ParticleSystem footSmokeLeft;//左足エフェクト
    public ParticleSystem footSmokeRight;//右足エフェクト
    public Transform[] patrolPoints;// 巡回ポイント
    int patrolIndex = 0;//今向かっているポイント

    void Start()
    {
        anim = GetComponent<Animator>();
        FindNewBuilding();

        fixedY = transform.position.y;
    }

    void Update()
    {
       //アニメーションの速度
        anim.speed = 0.7f;

        // Y固定
        transform.position = new Vector3(transform.position.x, fixedY, transform.position.z);

        if (attackTimer > 0f)
            attackTimer -= Time.deltaTime;

        if (targetBuilding == null)
        {
            Patrol();
            return;
        }


        Vector3 dir = targetBuilding.transform.position - transform.position;
        dir.y = 0;

        float dist = dir.magnitude;

        // 回転
        if (dir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
        }

        
        if (dist < attackDistance && !isAttacking)
        {
            isAttacking = true;
            attackTimer = attackCooldown;

            anim.SetTrigger("Attack");
            anim.SetFloat("Speed", 0);

            return;
        }

        
        if (!isAttacking)
        {
            Vector3 move = dir.normalized * moveSpeed * Time.deltaTime;
            transform.position += move;

            anim.SetFloat("Speed", move.magnitude);
        }
        else
        {
            anim.SetFloat("Speed", 0);
        }
    }

    void FindNewBuilding()
    {
        var buildings = GameObject.FindGameObjectsWithTag("Building")
            .Where(b => b != null && b.activeInHierarchy)
            .ToArray();

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

   
    public void PunchHit()
    {
        StartCoroutine(PunchHitRoutine());
    }

    IEnumerator PunchHitRoutine()
    {
        if (targetBreakScript != null)
        {
            targetBreakScript.Break(transform.position);

           
            targetBuilding.SetActive(false);

            targetBuilding = null;
            targetBreakScript = null;

          
            attackTimer = 0f;

            FindNewBuilding();
        }

        //硬直
        float stunTime = 1.2f;
        yield return new WaitForSeconds(stunTime);

        
        isAttacking = false;

       
        anim.SetFloat("Speed", 0.01f);
    }

    public void FootStepLeft()
    {
        if (footSmokeLeft != null)
            footSmokeLeft.Play();
    }

    public void FootStepRight()
    {
        if (footSmokeRight != null)
            footSmokeRight.Play();
    }
    void Patrol()
    {
        //巡回ポイントが無い場合は止まる
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            anim.SetFloat("Speed", 0);
            return;
        }

        Transform target = patrolPoints[patrolIndex];

        //方向
        Vector3 dir = target.position - transform.position;
        dir.y = 0;

        //回転
        if (dir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
        }

        //移動
        Vector3 move = dir.normalized * moveSpeed * Time.deltaTime;
        transform.position += move;

        anim.SetFloat("Speed", move.magnitude);

        //近づいたら次のポイントへ
        if (dir.magnitude < 2.0f)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }

        
        FindNewBuilding();
    }

}
