﻿using System;
using System.Collections.Generic;
using UnityEngine;
using RPG_GameData;

namespace RPG_GameState
{
    [Serializable]
    public class GameStateData
    {
        public class Config
        {
            public int Gold;
            public float PlayTime;
            public Vector2 Location;
            public string SceneName;
            public List<CharacterInfo> PartyMembers = new List<CharacterInfo>();
            public List<ItemData> Items = new List<ItemData>();
            public List<ItemData> KeyItems = new List<ItemData>();
            public List<QuestData> Quests = new List<QuestData>();
            public List<AreaData> Areas = new List<AreaData>();
        }

        private static int gid = 0;
        private int id;

        public int gold;
        public float playTime;
        public Vector2 location;
        public string sceneName;
        public List<CharacterInfo> partyMembers = new List<CharacterInfo>();
        public List<ItemData> items = new List<ItemData>();
        public List<ItemData> keyItems = new List<ItemData>();
        public List<QuestData> quests = new List<QuestData>();
        public List<AreaData> areas = new List<AreaData>();

        public GameStateData() { }

        public GameStateData(Config config)
        {
            SetData(config);
        }

        public void SetData(Config config)
        {
            if (config == null)
            {
                LogManager.LogError("Null Config passed to GameState SetData.");
                return;
            }
            gold = config.Gold;
            playTime = config.PlayTime;
            sceneName = config.SceneName;
            location = config.Location;
            partyMembers = config.PartyMembers;
            items = config.Items;
            quests = config.Quests;
            areas = config.Areas;
        }

        public QuestData GetQuestData(string id)
        {
            foreach (var quest in quests)
                if (quest.Id.Equals(id))
                    return quest;
            return null;
        }
    }

    [Serializable]
    public class ItemData
    {
        public int Count;
        public string Id;

        public static List<ItemData> FromItems(List<Item> items)
        {
            var itemData = new List<ItemData>();
            foreach (var quest in items)
                itemData.Add(FromItem(quest));
            return itemData;
        }

        public static ItemData FromItem(Item item)
        {
            return new ItemData { Count = item.Count, Id = item.ItemInfo.Id};
        }
    }

    [Serializable]
    public class QuestData
    {
        public bool IsStarted;
        public bool IsComplete;
        public string Id;

        public static List<QuestData> FromQuests(List<Quest> quests)
        {
            var questData = new List<QuestData>();
            foreach (var quest in quests)
                questData.Add(FromQuest(quest));
            return questData;
        }

        public static QuestData FromQuest(Quest quest)
        {
            return new QuestData { Id = quest.Id, IsComplete = quest.IsComplete, IsStarted = quest.IsStarted };
        }
    }

    [Serializable]
    public class AreaData
    {
        public string Id;
        public List<AreaEventData> Events = new List<AreaEventData>();
    }

    [Serializable]
    public class AreaEventData
    {
        public bool Complete;
        public string Id;
    }
}