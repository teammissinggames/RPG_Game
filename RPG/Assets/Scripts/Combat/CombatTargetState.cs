using System;
using System.Collections.Generic;
using UnityEngine;
using RPG_Character;

namespace RPG_Combat
{
    public enum CombatTargetType { One, Side, All };

    public class CombatTargetState : ConfigMonoBehaviour, IGameState
    {
        public class Config
        {
            public bool CanSwitchSides = true;
            public CombatTargetType CombatTargetType = CombatTargetType.One;
            public ICombatState GameState;
            public Func<ICombatState, bool, List<Actor>> DefaultSelector;
            public Action<List<Actor>> OnSelect;
            public Action OnExit;
        }

        private RectTransform[] SelectionArrows = new RectTransform[Constants.MAX_ENEMIES];

        private Config config;
        private List<Actor> party = new List<Actor>();
        private List<Actor> enemies = new List<Actor>();
        private List<Actor> targets = new List<Actor>();

        public void Init(Config config)
        {
            if (CheckUIConfigAndLogError(config, GetName()))
                return;
            this.config = config;
            if (config.DefaultSelector == null)
            {
                if (config.CombatTargetType == CombatTargetType.One)
                    config.DefaultSelector = (state, hurt) => CombatSelector.FindWeakestEnemy(state);
                else if (config.CombatTargetType == CombatTargetType.Side)
                    config.DefaultSelector = (state, hurt) => CombatSelector.Enemies(state);
                else if (config.CombatTargetType == CombatTargetType.All)
                    config.DefaultSelector = (state, hurt) => CombatSelector.SelectAll(state);
                else
                {
                    LogManager.LogError($"$Unkown Type of CombatTargetType [{config.CombatTargetType}]. Setting to Weakest Enemy.");
                    config.DefaultSelector = (state, hurt) => CombatSelector.FindWeakestEnemy(state);
                }
            }
        }

        public void Enter(object o = null)
        {
            gameObject.SafeSetActive(true);
            var enemiesActors = config.GameState.GetEnemyActors();
            if (enemiesActors.Count > 0)
                enemies.AddRange(enemiesActors);
            var partyActors = config.GameState.GetPartyActors();
            if (partyActors.Count > 0)
                party.AddRange(partyActors);
            targets = config.DefaultSelector(config.GameState, false);
            if (targets.Count < 0)
                return;
            SetArrowPositionsForAllTargets();
        }

        public bool Execute(float deltaTime)
        {
            return true;
        }

        public void Exit()
        {
            enemies.Clear();
            party.Clear();
            targets.Clear();
            config.OnExit?.Invoke();
            gameObject.SafeSetActive(false);

            for (int i = 0; i < SelectionArrows.Length; i++)
                if (SelectionArrows[i] != null)
                    SelectionArrows[i].gameObject.SafeSetActive(false);
        }

        public string GetName()
        {
            return "CombatTargetState";
        }

        public void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Backspace))
                config.GameState.Stack().Pop();
            else if (Input.GetKeyDown(KeyCode.UpArrow))
                Up();
            else if (Input.GetKeyDown(KeyCode.DownArrow))
                Down();
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
                Left();
            else if (Input.GetKeyDown(KeyCode.RightArrow))
                Right();
            else if (Input.GetKeyDown(KeyCode.Space))
                config.OnSelect?.Invoke(targets);
        }

        private List<Actor> GetActorList(Actor actor)
        {
            var inParty = config.GameState.IsPartyMember(actor);
            return inParty ? party : enemies;
        }

        private void Right()
        {
            if (config.CombatTargetType != CombatTargetType.One)
                return;
            var target = targets[0];
            var side = GetActorList(target);
            var index = GetActorIndex(side, target);
            index++;
            if (index >= side.Count)
                index = 0;
            targets.Clear();
            targets.Add(side[index]);
            SetArrowPosition(0);
        }

        private void Left()
        {
            if (config.CombatTargetType != CombatTargetType.One)
                return;
            var target = targets[0];
            var side = GetActorList(target);
            var index = GetActorIndex(side, target);
            index--;
            if (index < 0)
                index = side.Count - 1;
            targets.Clear();
            targets.Add(side[index]);
            SetArrowPosition(0);
        }

        private void Down()
        {
            if (!config.CanSwitchSides)
                return;
            if (config.GameState.IsPartyMember(targets[0]))
                return;
            targets.Clear();
            if (config.CombatTargetType == CombatTargetType.One)
            {
                targets.Add(enemies[0]);
                SetArrowPosition(0);
            }
            else if (config.CombatTargetType == CombatTargetType.Side)
            {
                targets.AddRange(enemies);
                SetArrowPositionsForAllTargets();
            }
            
        }

        private void Up()
        {
            if (!config.CanSwitchSides)
                return;
            if (!config.GameState.IsPartyMember(targets[0]))
                return;
            targets.Clear();
            if (config.CombatTargetType == CombatTargetType.One)
            {
                targets.Add(party[0]);
                SetArrowPosition(0);
            }
            else if (config.CombatTargetType == CombatTargetType.Side)
            {
                targets.AddRange(party);
                SetArrowPositionsForAllTargets();
            }
        }

        public int GetActorIndex(List<Actor> actors, Actor actor)
        {
            for(int i = 0; i < actors.Count; i++)
                if (actors[i].Id == actor.Id)
                    return i;
            return -1;
        }

        private void SetArrowPosition(int target, int arrow = 0)
        {
            var position = targets[target].GetComponent<Entity>().GetTargetPosition();
            position = Camera.main.WorldToScreenPoint(position);
            SelectionArrows[arrow].position = position;
        }

        private void SetArrowPositionsForAllTargets()
        {
            var assetManager = ServiceManager.Get<AssetManager>();
            int i = 0;
            for (; i < targets.Count; i++)
            {
                if (SelectionArrows[i] == null)
                {
                    assetManager.Load<RectTransform>(Constants.UI_SELECTION_CARET_PREFAB, (asset) =>
                    {
                        var arrow = Instantiate(asset);
                        SelectionArrows[i] = arrow;
                        arrow.SetParent(transform, false);
                        arrow.gameObject.SafeSetActive(true);
                        SetArrowPosition(i, i);
                    });
                }
                else
                {
                    SelectionArrows[i].gameObject.SafeSetActive(true);
                    SetArrowPosition(i, i);
                }
            }
            for (; i < SelectionArrows.Length; i++)
                if (SelectionArrows[i] != null)
                    SelectionArrows[i].gameObject.SafeSetActive(false);
        }
    }
}