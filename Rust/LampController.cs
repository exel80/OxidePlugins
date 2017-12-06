using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("LampController", "Exel80", 1.0)]
    [Description("Lamps fuel usage controller")]

    public class LampController : RustPlugin
    {
        #region Initialization
        bool debug = true;
        Dictionary<Transform, int> lightFuel = new Dictionary<Transform, int>();
        #endregion

        #region Magic
        void OnConsumeFuel(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            int data = checkLight(oven.transform);
            if (debug) Puts(data.ToString());

            if (data != 6)
            {
                if (data < 3)
                {
                    fuel.amount += 1;
                    return;
                }
                lightFuel.Remove(oven.transform);
                if (debug) Puts($"-1 fuel from {oven.name} {oven.transform.position}!");
            }
        }
        private int checkLight(Transform obj)
        {
            if (debug) Puts($"{obj.name}");

            if (!(obj.name.ToString().Contains("lantern.deployed")
                || obj.name.ToString().Contains("ceilinglight.deployed")))
                return 6;

            if (lightFuel.ContainsKey(obj))
                lightFuel[obj] += 1;
            else
                lightFuel.Add(obj, 1);

            return lightFuel[obj];
        }
        #endregion
    }
}
