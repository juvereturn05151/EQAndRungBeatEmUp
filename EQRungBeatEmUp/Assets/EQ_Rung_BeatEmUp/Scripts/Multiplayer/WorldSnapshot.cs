using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeatEmUp
{
    [Serializable] public sealed class LobbySlot
    {
        public int slot;
        public int character;
        public ulong owner;
        public string name;
        public bool ready;
        [NonSerialized] public UnityEngine.InputSystem.InputDevice device;
    }
    [Serializable] public sealed class LobbyState
    {
        public int requiredPlayerCount = 1;
        public bool allowDuplicateCharacters = true;
        public List<LobbySlot> slots=new List<LobbySlot>();
        public bool running;
        public string code;
        public string contentHash;
    }
    [Serializable] public sealed class SpriteState
    {
        public int id, sprite, order, layer;
        public Vector3 position, scale;
        public float rotation;
        public Color color;
        public bool flipX, flipY;
        public bool hubBlend;
        public int teleportVersion;
    }
    [Serializable] public sealed class PlayerState
    {
        public int slot, hits, best, frame, attack, visualId;
        public int character;
        public ulong owner;
        public Vector3 position;
        public MetaProfile meta;
        public int hubStation=-1;
        public float hp, maxHp, damage, comboTimer;
        public float meter, maxMeter;
        public bool activeCombo, dead, choosing, rewardDone;
        public string state;
        public string[] upgrades, choices, descriptions;
    }
    [Serializable] public sealed class FeedbackState
    {
        public int id, attack, facing;
        public int source;
        public int character;
        public bool impact;
        public string signal;
        public Vector2 point;
    }
    [Serializable] public sealed class EntityState
    {
        public int id;
        public string kind, name, state;
        public Vector3 position;
        public float hp, maxHp;
        public bool dead, invulnerable;
        public int phase, vulnerabilityFrames, warpIndex, warpVersion, respawnFrames;
        public string action;
    }
    [Serializable] public sealed class TotemWaveState
    {
        public int id, age, duration;
        public Vector3 position;
        public float radius, maximumRadius;
    }
    [Serializable] public sealed class NextAreaMarkerState
    {
        public string id;
        public Vector3 position;
        public bool showEdgeArrow;
        public int arrowSprite, labelSprite;
    }
    [Serializable] public sealed class WorldSnapshot
    {
        public long tick;
        public int stage;
        public bool exitOpen, completed, gameOver, rewardPending;
        public string status;
        public bool cameraLocked;
        public Rect encounterCameraBounds;
        public string encounter;
        public NextAreaMarkerState nextAreaMarker;
        public string validationPhase;
        public ulong validationOwner;
        public List<SpriteState> sprites=new List<SpriteState>();
        public List<PlayerState> players=new List<PlayerState>();
        public List<FeedbackState> feedback=new List<FeedbackState>();
        public List<EntityState> entities=new List<EntityState>();
        public List<TotemWaveState> totemWaves=new List<TotemWaveState>();
    }
}
