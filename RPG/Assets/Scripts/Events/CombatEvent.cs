using System;
using System.Collections.Generic;
using UnityEngine;
using RPG_AI;
using RPG_Character;
using RPG_GameData;

namespace RPG_Combat
{
    public abstract class CombatEvent : IEvent
    {
        protected bool finished;
        protected int priority;
        protected ICombatState state;
        protected Actor actor;

        public CombatEvent(Actor actor, ICombatState state)
        {
            this.actor = actor;
            this.state = state;
        }

        public virtual void Execute(EventQueue queue) { }

        public virtual void Update() { }

        public Actor GetActor()
        {
            return actor;
        }

        public int GetPriority()
        {
            return priority;
        }

        public abstract string GetName();

        public virtual bool IsFinished()
        {
            return finished;
        }

        public void SetPriority(int value)
        {
            priority = value;
        }

        public virtual int CalculatePriority(EventQueue queue)
        {
            if (queue == null)
                return 0;
            var speed = actor.Stats.Get(Stat.Speed);
            return queue.SpeedToPoints(speed);
        }
    }

    public class CETurn : CombatEvent
    {
        public CETurn(Actor actor, ICombatState state)
            : base(actor, state) { }

        public override void Execute(EventQueue queue)
        {
            LogManager.LogDebug($"Executing CETurn for {actor.name}");
            if (!state.IsPartyMember(actor))
            {
                LogManager.LogError($"AI Character {actor.name} has player turn event.");
                finished = true;
                return;
            }
            HandlePlayerTurn();
        }

        private void HandlePlayerTurn()
        {
            var config = new CombatChoiceState.Config
            {
                Actor = actor,
                State = state
            };
            var nextstate = state.GetUI().CombatChoice;
            nextstate.Init(config);
            state.Stack().Push(nextstate);
            finished = true;
        }

        public override string GetName()
        {
            return $"CombatEventTurn for {actor.name}";
        }
    }

    public class CEAITurn : CombatEvent
    {
        public CEAITurn(Actor actor, ICombatState state)
            : base(actor, state) { }

        public override void Execute(EventQueue queue)
        {
            LogManager.LogDebug($"Executing CETurn for {actor.name}");
            if (state.IsPartyMember(actor))
            {
                LogManager.LogError($"Player Character {actor.name} has AI turn event.");
                finished = true;
                return;
            }
            HandleEnemyTurn();
        }

        private void HandleEnemyTurn()
        {
            var ai = ServiceManager.Get<GameData>().EnemyAI;
            if (!ai.ContainsKey(actor.GameDataId))
            {
                LogManager.LogError($"Actor {actor.name} GameDataId {actor.GameDataId} not found in EnemyAI.");
                finished = true;
                return;
            }
            var aiData = ai[actor.GameDataId];
            var tree = state.GetBehaviorTreeForAIType(aiData.Type);
            if (tree == null)
            {
                LogManager.LogError($"Behavior Tree type {aiData.Type} not found in CombatGameState for Actor{actor.name}.");
                finished = true;
                return;
            }
            if (tree.Evaluate(actor) == NodeState.Failure)
            {
                LogManager.LogError($"Actor [{actor.name}] decision tree failed. Attacking random enemy.");
                var targets = new List<Actor>();
                targets.AddRange(CombatSelector.RandomPlayer(state));
                var config = new CEAttack.Config
                {
                    IsCounter = false,
                    IsPlayer = false,
                    Actor = actor,
                    CombatState = state,
                    Targets = targets
                };
                var attackEvent = new CEAttack(config);
                var turnHandler = state.CombatTurnHandler();
                turnHandler.AddEvent(attackEvent, priority);
            }
            finished = true;
        }

        public override string GetName()
        {
            return $"CombatEventAITurn for {actor.name}";
        }
    }

    public class CEAttack : CombatEvent
    {
        public class Config
        {
            public bool IsCounter;
            public bool IsPlayer;
            public Actor Actor;
            public ICombatState CombatState;
            public List<Actor> Targets = new List<Actor>();
        }

        private bool counter;
        private FormulaResult result;
        private Character character;
        private Storyboard storyboard;
        private Func<ICombatState, List<Actor>> targeter;
        private List<Actor> targets;

        public CEAttack(Config config) : base(config.Actor, config.CombatState)
        {
            targets = config.Targets;
            counter = config.IsCounter;
            character = actor.GetComponent<Character>();//state.ActorToCharacterMap[config.Actor.Id];
            if (character == null)
            {
                LogManager.LogError($"Character is null for actor {config.Actor}");
                return;
            }

            var events = new List<IStoryboardEvent>();
            var text = ServiceManager.Get<LocalizationManager>().Localize("ID_ATTACK_NOTICE_TEXT");
            events.Add(StoryboardEventFunctions.Function(() => state.CombatReporter().DisplayInfo(string.Format(text, actor.Name))));
            if (config.IsPlayer)
            {
                //this.mAttackAnim = gEntities.slash
                targeter = (state) => CombatSelector.FindWeakestEnemy(state);
                var attackMoveParams = new CombatStateParams
                {
                    Direction = 1,
                    MovePosition = Vector2.up * 2.0f
                };
                var returnMoveParams = new CombatStateParams
                {
                    Direction = 1,
                    MovePosition = Vector2.down * 2.0f
                };
                events.Add(StoryboardEventFunctions.RunCombatState(character.Controller, Constants.COMBAT_MOVE_STATE, attackMoveParams));
                // TODO remove Temp for testing
                events.Add(StoryboardEventFunctions.Wait(1.0f));
                //
                events.Add(StoryboardEventFunctions.RunCombatState(character.Controller, Constants.RUN_ANIMATION_STATE, 
                    new CSRunAnimation.Config 
                    { 
                        Animation = Constants.ATTACK_ANIMATION, 
                        StartAnimation = () => 
                        {
                            character.animator.SetInteger("Attack Value", 1);
                            character.animator.SetTrigger("Attack");
                        }
                    }));
                events.Add(StoryboardEventFunctions.Wait(1.0f));
                events.Add(StoryboardEventFunctions.Function(DoAttack));
                events.Add(StoryboardEventFunctions.Function(ShowResult));
                events.Add(StoryboardEventFunctions.RunCombatState(character.Controller, Constants.COMBAT_MOVE_STATE, returnMoveParams));
                events.Add(StoryboardEventFunctions.Wait(1.0f));
                events.Add(StoryboardEventFunctions.Function(OnFinish));
            }
            else
            {
                targeter = (state) => CombatSelector.RandomPlayer(state);
                var targetPosition = (state.GetEnemyActors()[0].transform.position - actor.transform.position) * 0.5f;
                targetPosition.y = actor.transform.position.y;
                var attackMoveParams = new CombatStateParams
                {
                    Direction = 0,
                    MovePosition = Vector2.down
                };
                var returnMoveParams = new CombatStateParams
                {
                    Direction = 0,
                    MovePosition = Vector2.up
                };
                events.Add(StoryboardEventFunctions.RunCombatState(character.Controller, Constants.COMBAT_MOVE_STATE, attackMoveParams));
                // TODO remove Temp for testing
                events.Add(StoryboardEventFunctions.Wait(1.0f));
                events.Add(StoryboardEventFunctions.RunCombatState(character.Controller, Constants.RUN_ANIMATION_STATE, 
                    new CSRunAnimation.Config 
                    { 
                        Animation = Constants.ATTACK_ANIMATION, 
                        StartAnimation = () => 
                        {
                            character.animator.SetInteger("Attack Value", 1);
                            character.animator.SetTrigger("Attack");
                        }
                    }));
                events.Add(StoryboardEventFunctions.Wait(1.0f));
                events.Add(StoryboardEventFunctions.Function(DoAttack));
                events.Add(StoryboardEventFunctions.Function(ShowResult));
                events.Add(StoryboardEventFunctions.RunCombatState(character.Controller, Constants.COMBAT_MOVE_STATE, returnMoveParams));
                events.Add(StoryboardEventFunctions.Wait(1.0f));
                events.Add(StoryboardEventFunctions.Function(OnFinish));
            }
            storyboard = new Storyboard(state.Stack(), events);
        }

        public override void Execute(EventQueue queue)
        {
            LogManager.LogDebug($"Executing CEAttack for {actor.name}");
            state.Stack().Push(storyboard);
            for (int i = targets.Count - 1; i > -1; i--)
            {
                var hp = targets[i].Stats.Get(Stat.HP);
                if (hp <= 0)
                    targets.RemoveAt(i);
            }
            if (targets.Count < 1)
                targets.AddRange(targeter(state));
        }

        private void OnFinish()
        {
            finished = true;
            state.GetUI().HideNotice();            
        }

        private void DoAttack()
        {
            foreach (var target in targets)
            {
                AttackTarget(target);
                if (!counter)
                    CounterTarget(target);
            }
        }

        private void CounterTarget(Actor target)
        {
            var countered = CombatFormula.IsCounter(actor, target);
            if (countered)
            {
                LogManager.LogDebug($"Countering [{actor}] with Target [{target}]");
                CombatActions.ApplyCounter(target, actor, state);
            }
        }

        private void AttackTarget(Actor target)
        {
            LogManager.LogDebug($"Attacking {target.name}");
            result = CombatFormula.MeleeAttack(actor, target);
            var entity = target.GetComponent<Entity>();
            if (result.Result == CombatFormula.HitResult.Miss)
            {
                CombatActions.ApplyMiss(target);
                return;
            }
            if (result.Result == CombatFormula.HitResult.Dodge)
                CombatActions.ApplyDodge(target);
            else
            {
                var isCrit = result.Result == CombatFormula.HitResult.Critical;
                CombatActions.ApplyDamage(target, result.Damage, isCrit);
            }
            var entityPosition = entity.transform.position;
            // TODO
        }

        private void ShowResult()
        {
            if (result == null)
                return;
            state.CombatReporter().ReportResult(result, actor.name);
        }

        public override string GetName()
        {
            return "CombatEventAttack";
        }
    }

    public class CEFlee : CombatEvent
    {
        public class Config
        {
            public float Time = 0.6f;
            public Direction Direction = Direction.East;
            public Actor Actor;
            public ICombatState State;
        }

        private float time;
        private Direction direction;
        private Storyboard storyboard;

        public CEFlee(Config config) : base(config.Actor, config.State)
        {
            if (config == null)
            {
                LogManager.LogError("CEFleeEvent passed null config.");
                finished = true;
                return;
            }
            direction = config.Direction;
            time = config.Time;

            var events = new List<IStoryboardEvent>
            {
                StoryboardEventFunctions.Function(() => {
                    state.CombatReporter().DisplayInfo("ID_ATTEMPT_FLEE_TEXT");
                }),
                StoryboardEventFunctions.Wait(1.0f)
            };
            if (CombatFormula.CanEscape(state.GetEnemyActors(), actor))
            {
                events.Add(StoryboardEventFunctions.Function(() => FleeSuccessPart1()));
                StoryboardEventFunctions.Wait(0.3f);
                events.Add(StoryboardEventFunctions.Function(() => FleeSuccessPart2()));
                StoryboardEventFunctions.Wait(0.6f);
                
            }
            else
            {
                events.Add(StoryboardEventFunctions.Function(() => state.CombatReporter().DisplayInfo("ID_FLEE_FAILED_TEXT")));
                StoryboardEventFunctions.Wait(0.3f);
                events.Add(StoryboardEventFunctions.Function(() => OnFleeFail()));
            }
            storyboard = new Storyboard(state.Stack(), events);
            var character = actor.GetComponent<Character>();
            character.direction = direction;
            LogManager.LogDebug($"Trying to flee for {actor.Name}");
        }

        public override void Execute(EventQueue queue)
        {
            LogManager.LogDebug($"Executing CEFlee for {actor.name}");
            state.Stack().Push(storyboard);
        }

        public override int CalculatePriority(EventQueue queue)
        {
            return Constants.MAX_STAT_VALUE;
        }

        public override string GetName()
        {
            return "CombatFleeEvent";
        }

        private void FleeSuccessPart1()
        {
            state.CombatReporter().DisplayInfo("ID_SUCCESS_TEXT");
            var targetPosition = actor.transform.position + Vector3.right * 5.0f; // Run off screen, does not matter if position is ever reached
            var moveParams = new MoveParams(targetPosition);
            actor.GetComponent<Character>().Controller.Change(Constants.MOVE_STATE, moveParams);
        }

        private void FleeSuccessPart2()
        {
            foreach (var actor in state.GetPartyActors())
            {
                var alive = actor.Stats.Get(Stat.HP) > 0;
                var isFleer = actor.Id == this.actor.Id;
                if (alive && !isFleer)
                {
                    var character = actor.GetComponent<Character>();
                    character.direction = direction;
                    var targetPosition = actor.transform.position + Vector3.right * 5.0f; // Run off screen, does not matter if position is ever reached
                    var moveParams = new MoveParams(targetPosition);
                    character.Controller.Change(Constants.MOVE_STATE, moveParams);
                }
            }
            state.OnFlee();
            state.GetUI().HideNotice();
        }

        private void OnFleeFail()
        {
            var character = actor.GetComponent<Character>();
            character.direction = direction == Direction.East ? Direction.West : Direction.East;
            character.Controller.Change(Constants.STAND_STATE);
            finished = true;
            state.GetUI().HideNotice();
        }
    }

    public class CEUseItemEvent : CombatEvent
    {
        public class Config
        {
            public bool IsPlayer;
            public Actor Actor;
            public ICombatState CombatState;
            public ItemInfo Item;
            public ItemUse ItemUse;
            public List<Actor> Targets = new List<Actor>();
        }

        private ItemInfo item;
        private ItemUse itemUse;
        private StateMachine controller;
        private Storyboard storyboard;
        private List<Actor> targets;
        public CEUseItemEvent(Config config)
            : base(config.Actor, config.CombatState)
        {
            item = config.Item;
            itemUse = config.ItemUse;
            targets = config.Targets;
            finished = false;
            controller = actor.GetComponent<Character>().Controller;
            // TODO change to ready to attack state controller.Change(Constants.)
            // Remove now to take from inventory
            ServiceManager.Get<World>().RemoveItem(item.Id);
            var direction = config.IsPlayer ? 1 : 0;
            var attackMoveParams = new CombatStateParams
            {
                Direction = direction,
                MovePosition = config.IsPlayer ? Vector2.up : Vector2.down
            };
            var returnMoveParams = new CombatStateParams
            {
                Direction = direction,
                MovePosition = config.IsPlayer ? Vector2.down : Vector2.up
            };
            var events = new List<IStoryboardEvent>
            {
                StoryboardEventFunctions.Function(() => ShowItemNotice()),
                StoryboardEventFunctions.RunCombatState(controller, Constants.COMBAT_MOVE_STATE, attackMoveParams),
                // Temp for testing
                StoryboardEventFunctions.Wait(0.5f),
                //
                StoryboardEventFunctions.RunCombatState(controller, Constants.USE_STATE, new CSRunAnimation.Config { Animation = Constants.ATTACK_ANIMATION }),
                StoryboardEventFunctions.Function(() => UseItem()),
                StoryboardEventFunctions.Wait(1.3f),
                StoryboardEventFunctions.RunCombatState(controller, Constants.COMBAT_MOVE_STATE, returnMoveParams),
                StoryboardEventFunctions.Function(() => Finish())
            };
            storyboard = new Storyboard(state.Stack(), events);
        }

        public override void Execute(EventQueue queue)
        {
            LogManager.LogDebug($"Executing CEUseItemEvent for {actor.name}");
            state.Stack().Push(storyboard);
        }

        private void ShowItemNotice()
        {
            var notice = $"Item: {item.GetName()}";
            state.CombatReporter().DisplayInfo(notice);
        }

        private void HideNotice()
        { 
            state.GetUI().HideNotice();
        }

        private void UseItem()
        {
            HideNotice();
            var position = actor.GetComponent<Entity>().GetSelectPosition();
            // TODO create effect
            var config = new CombatActionConfig
            {
                Owner = actor,
                StateId = "item",
                Targets = targets,
                Def = itemUse
            };
            CombatActions.RunAction(itemUse.Action, config);
        }

        private void Finish()
        {
            finished = true;
        }

        public override string GetName()
        {
            return "CEUseItemEvent";
        }
    }
    public class CECastSpellEvent : CombatEvent
    {
        public class Config
        {
            public bool IsPlayer;
            public Actor Actor;
            public ICombatState CombatState;
            public Spell Spell;
            public List<Actor> Targets = new List<Actor>();
        }

        private Spell spell;
        private StateMachine controller;
        private Storyboard storyboard;
        private List<Actor> targets;
        public CECastSpellEvent(Config config)
            : base(config.Actor, config.CombatState)
        {
            spell = config.Spell;
            targets = config.Targets;
            finished = false;
            controller = actor.GetComponent<Character>().Controller;
            // TODO change to ready to attack state controller.Change(Constants.)
            var direction = config.IsPlayer ? 1: 0;
            var attackMoveParams = new CombatStateParams
            {
                Direction = direction,
                MovePosition = config.IsPlayer ? Vector2.up : Vector2.down
            };
            var returnMoveParams = new CombatStateParams
            {
                Direction = direction,
                MovePosition = config.IsPlayer ? Vector2.down : Vector2.up
            };
            var events = new List<IStoryboardEvent>()
            { 
                StoryboardEventFunctions.Function(() => ShowNotice()),
                StoryboardEventFunctions.RunCombatState(controller, Constants.COMBAT_MOVE_STATE, attackMoveParams),
                StoryboardEventFunctions.Wait(0.5f),
                StoryboardEventFunctions.RunCombatState(controller, Constants.RUN_ANIMATION_STATE, new CSRunAnimation.Config { Animation = Constants.CAST_ANIMATION_STATE }), // TODO change to cast?
                StoryboardEventFunctions.Wait(0.12f),
                StoryboardEventFunctions.NoBlock(
                    StoryboardEventFunctions.RunCombatState(controller, Constants.RUN_ANIMATION_STATE, new CSRunAnimation.Config { Animation = Constants.WAIT_STATE })
                ),
                StoryboardEventFunctions.Function(() => Cast()),
                StoryboardEventFunctions.Wait(1.0f),
                StoryboardEventFunctions.Function(() => HideNotice()),
                StoryboardEventFunctions.RunCombatState(controller, Constants.COMBAT_MOVE_STATE, returnMoveParams),
                StoryboardEventFunctions.Function(() => Finish())
            };
            storyboard = new Storyboard(state.Stack(), events);
        }

        public override void Execute(EventQueue queue)
        {
            LogManager.LogDebug($"Executing CECastSpellEvent for {actor.name}");
            state.Stack().Push(storyboard);
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                var hp = targets[i].Stats.Get(Stat.HP);
                var isEnemy = !state.IsPartyMember(targets[i]);
                if (isEnemy && hp <= 0) // TODO keep enemies, possibly for enemy revive spells?
                    targets.RemoveAt(i);
            }

            if (targets.Count < 1)
                targets = spell.ItemTarget.Selector(state, true);
        }

        private void ShowNotice()
        {
            var notice = $"Spell: {spell.LocalizedName()}";
            state.CombatReporter().DisplayInfo(notice);
        }

        private void HideNotice()
        {
            state.GetUI().HideNotice();
        }

        private void Cast()
        {
            var position = actor.GetComponent<Character>().Entity.GetSelectPosition();
            // TODO effect
            actor.ReduceManaForSpell(spell);
            var data = new RPG_UI.HpMpWidget.UpdateData 
            {
                Value = actor.Stats.Get(Stat.MP),
                Id = actor.GameDataId
            };
            GameEventsManager.BroadcastMessage(GameEventConstants.ON_MP_CHANGED, data);
            var config = new CombatActionConfig
            {
                Owner = actor,
                StateId = "magic",
                Targets = targets,
                Def = spell
            };
            CombatActions.RunAction(spell.Action, config);
        }

        private void Finish()
        {
            finished = true;
        }

        public override string GetName()
        {
            return "CECastSpellEvent";
        }
    }
}