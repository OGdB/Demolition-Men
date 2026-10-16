using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayerHealth = Demolition.PlayerHealth;

public class CameraMovement : MonoBehaviour
{
    public float movingSpeed = 3f;
    public float smoothZoomIn = 0.05f;
    public float smoothZoomOut = 0.08f;

    [Header("Player framing")]
    [Tooltip("How quickly the camera rises/settles to follow the players vertically.")]
    public float verticalFollowSpeed = 2.5f;
    [Tooltip("Horizontal padding kept around the players when zooming to frame them.")]
    public float frameMarginX = 3f;
    [Tooltip("Vertical padding kept around the players when zooming to frame them.")]
    public float frameMarginY = 2.5f;
    public float minZoom = 6f;
    public float maxZoom = 30f;

    private bool Dead = false;
    private Collider2D deadCollider;
    private float baseY;

    public GameObject p1;
    public GameObject p2;

    public GameObject target1;
    public GameObject target2;

    public Camera cam;

    private void Start()
    {
        baseY = transform.position.y;

        // The players are prefab instances now; scene-serialized references died with
        // the legacy player objects, so resolve them by name like GlobalVariables does.
        if (p1 == null)
        {
            p1 = GameObject.Find("Player_1");
        }
        if (p2 == null)
        {
            p2 = GameObject.Find("Player_2");
        }

        if (GlobalVariables.singlePlayer || p2 == null)
        {
            target1 = p1;
            target2 = p1;
        }
        else
        {
            target1 = p1;
            target2 = p2;
        }

        // Under a scrolling camera there is no safe spawn to return to — the start is
        // long gone behind the kill wall — so running out of health is fatal here.
        // The test bench has no scrolling camera and keeps its respawn behaviour.
        MarkPermanentDeath(p1);
        MarkPermanentDeath(p2);
    }

    private static void MarkPermanentDeath(GameObject player)
    {
        if (player == null)
            return;
        var health = player.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.PermanentDeath = true;
        }
    }
    void Update()
    {
        if (target1 == null || target2 == null)
        {
            return;
        }

        if (IsDead(target1) && IsDead(target2))
        {
            Dead = true;
        }

        /**
        Reseting the camera to look at the place the player failed in
        and moving the camera to the right at a desired speed
        and otherwise moving the camera to the right at a desired speed
        */

        deadChecker();

        deathCamera();

        if (!Dead)
        {
            followPlayers();
        }

        /**
         * Adjusting the position of the box collider of the camera according to its size
         */
        float offset = cam.orthographicSize;
        cam.GetComponent<BoxCollider2D>().offset = new Vector2(-Mathf.Abs((float)(offset * 1.2)), 0);
        GetComponentInChildren<CapsuleCollider2D>().offset = new Vector2(Mathf.Abs((float)(1.5 + offset * 1.2)), 0);
    }

    /**
    Following the players upward (never below the level's baseline) and zooming so
    that every target stays framed — with two players alive that means both of them.
    */
    private void followPlayers()
    {
        Vector3 t1 = target1.transform.position;
        Vector3 t2 = target2.transform.position;

        Vector3 pos = transform.position;
        float focusY = (t1.y + t2.y) * 0.5f;
        pos.y = Mathf.Lerp(pos.y, Mathf.Max(baseY, focusY), Time.deltaTime * verticalFollowSpeed);
        transform.position = pos;

        float halfHeight = Mathf.Max(Mathf.Abs(t1.y - pos.y), Mathf.Abs(t2.y - pos.y)) + frameMarginY;
        // Horizontal framing uses the spread BETWEEN the players (zero in singleplayer),
        // never their distance to the camera's own x: the camera only scrolls once a
        // player reaches the border trigger, and since that trigger's offset grows with
        // zoom, framing against the camera center would push the border away forever.
        float halfWidth = Mathf.Abs(t1.x - t2.x) * 0.5f + frameMarginX;
        float needed = Mathf.Clamp(Mathf.Max(halfHeight, halfWidth / cam.aspect), minZoom, maxZoom);

        // smoothZoomIn/Out are the legacy per-frame steps (authored for 60 fps); scale
        // by dt so zoom speed no longer depends on frame rate.
        float step = (needed > cam.orthographicSize ? smoothZoomOut : smoothZoomIn) * 60f * Time.deltaTime;
        cam.orthographicSize = Mathf.MoveTowards(cam.orthographicSize, needed, step);
    }

    private static bool IsDead(GameObject player)
    {
        if (player == null)
            return true;
        var health = player.GetComponent<PlayerHealth>();
        return health == null || health.IsDead;
    }

    // Updates the target(s) of the camera
    private void deadChecker()
    {
        if (target1 != target2)
        {
            if (IsDead(target1))
            {
                target1 = p2;
            }
            if (IsDead(target2))
            {
                target2 = p1;
            }
        }
    }

    // Camera focusing on the last player who died, if he dies.
    private void deathCamera()
    {
        if (!Dead)
        {
            transform.position += Vector3.right * Time.deltaTime * movingSpeed;
        }
        else
        {
            if (deadCollider != null)
            {
                cam.transform.position = Vector3.Lerp(cam.transform.position, new Vector3(deadCollider.transform.position.x, deadCollider.transform.position.y, cam.transform.position.z), Time.deltaTime * movingSpeed);
            }
            else // If the player died in another way (traps/buildings falling on him)
            {
                deadCollider = target1.GetComponent<Collider2D>();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision != null)
        {
            if (collision.CompareTag("Player"))
            {
                var health = collision.gameObject.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    health.Kill();
                }

                if (target1 == target2 && IsDead(target1))
                {
                    Dead = true;
                    deadCollider = collision;
                }
            }
        }
 /*  if (collision != null)
   {
       if (collision.CompareTag("Player") && target1.GetComponent<PlayerHealth>().playerDied == false)
       {
           if (target1 == target2)
           {
               Dead = true;
               deadCollider = collision;
           }
       }
       else if (collision.CompareTag("Player") && target1.GetComponent<PlayerHealth>().playerDied == true)
       {
           Dead = true;
           collision.gameObject.GetComponent<PlayerHealth>().killPlayer(collision.gameObject);
           deadCollider = collision;
       }
   }*/
    }
}
