using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/* ControllerManager Summary
 * The Controller manager manages the controllers and assigns a different controllerName(string with index-number) to each registered controller.
 * When a controller is connected, integer connectedController is updated, activating the loop below as the condition is not fulfilled. The loop then adds the name of the controller to the ControllerName string.
*/

public class ControllerManager : MonoBehaviour
{
    public int connectedControllers;
    public string[] controllerNames;

    private void Start()
    {
        connectedControllers = Gamepad.all.Count;
    }

    private void Update()
    {
        connectedControllers = Gamepad.all.Count;

        controllerNames = new string[Gamepad.all.Count];
        for(int n = 0; n < controllerNames.Length; n++)
        {
            controllerNames[n] = Gamepad.all[n].name;
        }
    }
}