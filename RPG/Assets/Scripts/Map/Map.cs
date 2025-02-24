﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using RPG_Character;
using RPG_GameData;
using Cinemachine;

public class Map : MonoBehaviour
{
    [SerializeField] public string MapName;
    [SerializeField] public string CombatBackground = Constants.DEFAULT_COMBAT_BACKGROUND;
    [SerializeField] public Vector3 HeroStartingPosition;
    [SerializeField] public CinemachineVirtualCamera Camera;
    [SerializeField] protected Transform NPCParent;
    [SerializeField] Tilemap Encounters;
    [SerializeField] CheckConditionTrigger Conditions;
    [SerializeField] List<Character> MapNPCs = new List<Character>();
    [SerializeField] Dictionary<Vector2Int, Entity> Entities = new Dictionary<Vector2Int, Entity>();
    
    protected Encounter Encounter = new Encounter();
    protected Area Area = new Area();

    private void Start()
    {
        var gameData = ServiceManager.Get<GameData>();
        var map = MapName.ToLower();
        if (gameData.Encounters.ContainsKey(map))
        {
            Encounter = gameData.Encounters[map];
        }
        if (gameData.Areas.ContainsKey(map))
        {
            Area = gameData.Areas[map];
        }
    }

    public virtual void Init()
    {
        LogManager.LogDebug($"Current background music is: {Area.BackgroundMusic}");
        var background = !Area.BackgroundMusic.IsEmptyOrWhiteSpace() ? Area.BackgroundMusic : Constants.DEFAULT_BACKGROUND_MUSIC;
        ServiceManager.Get<RPG_Audio.AudioManager>().SetBackgroundAudio(background);
        Conditions?.Init(Area);
        RegisterNPCs();
    }

    public void AddNPC(Character npc)
    {
        npc.transform.SetParent(NPCParent, false);
        ServiceManager.Get<NPCManager>().AddNPC(MapName, npc);
    }

    public void AddCombatNPC(Character npc)
    {
        npc.transform.SetParent(NPCParent, false);
        ServiceManager.Get<NPCManager>().AddNPC("combat", npc);
    }

    public void RemoveNPC(Character npc)
    {
        var character = ServiceManager.Get<NPCManager>().RemoveNPC(MapName, npc);
        Destroy(character.gameObject);
    }

    public void AddEntity(Entity entity)
    {
        var pos = entity.transform.position;
        var position = new Vector2Int((int)pos.x, (int)pos.y);
        if (Entities.ContainsKey(position))
            return;
        Entities.Add(position, entity);
    }

    public void AddEntity(Entity entity, Vector2Int position)
    {
        if (Entities.ContainsKey(position))
            return;
        Entities.Add(position, entity);
    }

    public void RemoveEntity(Entity entity)
    {
        var pos = entity.transform.position;
        var position = new Vector2Int((int)pos.x, (int)pos.y);
        if (!Entities.ContainsKey(position))
            return;
        Entities.Remove(position);
    }

    public Entity GetEntity(Vector2 pos)
    {
        var position = new Vector2Int((int)pos.x, (int)pos.y);
        if (!Entities.ContainsKey(position))
            return null;
        return Entities[position];
    }

    public bool TryEncounter(Vector3Int position)
    {
        if (Encounters == null)
            return false;
        if (!Encounters.HasTile(position))
            return false;
        var tile = Encounters.GetTile(position) as EncounterTile;
        if (tile == null)
            return false;
        var encounter = Encounter.GetEncounter(tile.EncounterId).Pick();
        if (encounter.Items.Count == 0)
            return false;
        StartCombat(encounter.Items);
        return true;
    }

    private void StartCombat(List<string> enemies)
    {
        var gameLogic = ServiceManager.Get<GameLogic>();
        var config = new Actions.StartCombatConfig
        { 
            CanFlee = true,
            Background = CombatBackground,
            Stack = gameLogic.Stack,
            Party = gameLogic.GameState.World.Party.Members,
            Enemies  = enemies
        };
        Actions.Combat(config);
    }

    private void RegisterNPCs()
    {
        foreach (var npc in MapNPCs)
            ServiceManager.Get<NPCManager>().AddNPC(MapName, npc);
    }
}
