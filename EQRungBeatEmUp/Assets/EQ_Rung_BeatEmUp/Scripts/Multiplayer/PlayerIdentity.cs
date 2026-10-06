using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BeatEmUp
{
    // Characters keep their existing independent combat, health, combo and run-build components.
    [DisallowMultipleComponent]
    public sealed class PlayerIdentity : MonoBehaviour
    {
        public int slot;
        public int character;
        public ulong owner;
        public CharacterMotor Motor => GetComponent<CharacterMotor>();
        public CharacterHealth Health => GetComponent<CharacterHealth>();
        public bool Living => isActiveAndEnabled && Health && !Health.IsDead;
        public bool LocallyControlled => !MultiplayerSession.Active || MultiplayerSession.Active.IsLocalOwner(owner);
        void OnEnable() => PlayerRoster.Register(this);
        void OnDisable() => PlayerRoster.Unregister(this);
    }

    public static class PlayerRoster
    {
        static readonly List<PlayerIdentity> players = new List<PlayerIdentity>();
        public static IEnumerable<PlayerIdentity> Players => players.Where(p=>p && p.isActiveAndEnabled).OrderBy(p=>p.slot);
        public static IEnumerable<PlayerIdentity> Living => Players.Where(p=>p.Living);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => players.Clear();
        public static void Register(PlayerIdentity player) { if(!players.Contains(player)) players.Add(player); }
        public static void Unregister(PlayerIdentity player) => players.Remove(player);
        public static IEnumerable<CharacterMotor> Motors(CharacterMotor fallback) => Players.Any() ? Players.Select(p=>p.Motor) : fallback ? new[]{fallback} : Enumerable.Empty<CharacterMotor>();
        public static CharacterMotor Nearest(Vector2 point) => Living.OrderBy(p=>((Vector2)p.transform.position-point).sqrMagnitude).Select(p=>p.Motor).FirstOrDefault();
    }
}
