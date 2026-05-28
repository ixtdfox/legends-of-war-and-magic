using LegendsOfWarAndMagic.Game.World.Application;
using LegendsOfWarAndMagic.Game.World.Domain;
using LegendsOfWarAndMagic.Game.World.Ports;
using LegendsOfWarAndMagic.Game.World.Presentation;
using UnityEngine;

namespace LegendsOfWarAndMagic.Game.World.Infrastructure.Unity
{
    [DisallowMultipleComponent]
    public sealed class LocationBoundaryTrigger : MonoBehaviour
    {
        private WorldLocation currentLocation;
        private LocationGateway gateway;
        private ILocationTransitionService transitionService;
        private bool promptOpen;

        public void Configure(WorldLocation currentLocation, LocationGateway gateway, ILocationTransitionService transitionService)
        {
            this.currentLocation = currentLocation;
            this.gateway = gateway;
            this.transitionService = transitionService;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (promptOpen || gateway == null || transitionService == null)
            {
                return;
            }

            if (other.GetComponent<CharacterController>() == null && other.GetComponentInParent<CharacterController>() == null)
            {
                return;
            }

            var world = GeneratedWorldSession.CurrentWorld;
            var target = world?.FindLocation(gateway.ToLocationId);
            if (target == null)
            {
                return;
            }

            promptOpen = true;
            LocationTransitionPromptUI.Ensure().Show(
                target.Name.Value,
                () =>
                {
                    promptOpen = false;
                    transitionService.EnterLocation(gateway.ToLocationId, currentLocation.Id, gateway.EntryDirection);
                },
                () => promptOpen = false);
        }
    }
}
