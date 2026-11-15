using Unity.Netcode.Components;

namespace Swarm
{
    // Local movement stays responsive; NGO relays it to the other players.
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
