using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/* ADD NEW TOOL / WEAPON INSTRUCTION
1. Add the sprites of the new weapon/tool with and without a green glow around it to the game. (Look at the current sprites in Assets/Sprites/Tools)
2. Add the Object State script to the new object.
3. Add a boolean for the new weapon/tool to the Object State Script (put it under the right header!)
4. Set this boolean to true in the inspector when clicking the tool/weapon.
5. Drag the sprite for the 'unselected' tool/weapon in 'Unselected' in the Object State Script in the Inspector. Do the same thing with the 'Selected' Script.
6. Add the script for using the weapon/tool in the playerattack or ToolPickup script. (Or add a new script if this is really necessary).
 */
public class ToolPickup : MonoBehaviour
{
    private PlayerAttack pa;
    private PlayerInput pi;
    private Rigidbody2D rgb;
    private PlayerMovement pm;
    private float pickupCooldown;
    public float pickupCooldownTime = 1.5f;
    public bool canPickUp;
    private bool hasWeapon = false;
    public GameObject currentWeapon;

    public int DynamiteCount = 0;

    void Start()
    {
        pm = GetComponent<PlayerMovement>();
        pa = GetComponent<PlayerAttack>();
        pi = GetComponent<PlayerInput>();
        rgb = GetComponent<Rigidbody2D>();
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        // Sprite 'in-range-glow:
        if (col.CompareTag("Tool") || col.CompareTag("Weapon"))
        {
            SpriteRenderer spr = col.GetComponent<SpriteRenderer>();
            Sprite currentSprite = col.GetComponent<SpriteRenderer>().sprite;
            Sprite Unselected = col.GetComponent<ObjectState>().Unselected;
            Sprite Selected = col.GetComponent<ObjectState>().Selected;

            if (currentSprite.name == Unselected.name)
            {
                spr.sprite = Selected;
            }
        }
    }

    void OnTriggerStay2D(Collider2D col)
    {
        if (col.CompareTag("Weapon") || col.CompareTag("Tool") && pickupCooldown <= Time.time)
        {
            canPickUp = true;
        }

        if (canPickUp && pi.controller.Y && pickupCooldown <= Time.time)
        {
            pickUpTool(col.gameObject);
        }
    }

    void OnTriggerExit2D(Collider2D col)
    {
        if (col.CompareTag("Weapon") || col.CompareTag("Tool"))
        {
            canPickUp = false;
        }

        if (col.CompareTag("Tool") || col.CompareTag("Weapon"))
        {
            SpriteRenderer spr = col.GetComponent<SpriteRenderer>();
            Sprite currentSprite = col.GetComponent<SpriteRenderer>().sprite;
            Sprite Unselected = col.GetComponent<ObjectState>().Unselected;
            Sprite Selected = col.GetComponent<ObjectState>().Selected;

            if (currentSprite.name == Selected.name)
            {
                spr.sprite = Unselected;
            }
        }
    }

/* SUMMARY PICK UP TOOL
 * Check whether the picked up tool is a weapon, or an upgrade-tool. Initiate different code accordingly.
 * 
 */
    private void pickUpTool(GameObject col)
    {
        if (col.CompareTag("Weapon"))
        {
            if (hasWeapon)
            {
                currentWeapon.SetActive(true);
                currentWeapon.transform.position = new Vector2(transform.position.x, transform.position.y + 1f);
                currentWeapon = col.gameObject;
                currentWeapon.SetActive(false);
                updateStats(col);
                return;
            }
            else
            {
                currentWeapon = col.gameObject;
                currentWeapon.SetActive(false);
                updateStats(col);
                hasWeapon = true;
                return;
            }
        }

        else if (col.CompareTag("Tool"))
        {
            col.gameObject.SetActive(false);
            updateStats(col);
            return;
        }

        void updateStats(GameObject pickedUpTool)
        {
            ObjectState weaponStats = pickedUpTool.GetComponent<ObjectState>();
            if (pickedUpTool.CompareTag("Weapon"))
            {
                pa.brickDamage = weaponStats.brickDamage;
                pa.woodDamage = weaponStats.woodDamage;
                pa.metalDamage = weaponStats.metalDamage;

                pa.hasAxe = weaponStats.Axe;
                pa.isPunching = false;
                pa.hasWrecking = weaponStats.WreckingBall;
            }
            // TOOLS
            if (pickedUpTool.GetComponent<ObjectState>().Spring)
            {
                pm.JumpVelocity += 20f;
            }

            if (pickedUpTool.GetComponent<ObjectState>().Weight)
            {
                rgb.mass = 10f;
            }

            if (pickedUpTool.GetComponent<ObjectState>().Dynamite)
            {
                DynamiteCount += 3;
            }
                pickupCooldown = Time.time + pickupCooldownTime;
        }
    }
}



