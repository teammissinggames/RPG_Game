using System;
using System.Collections.Generic;
using UnityEngine;
using RPG_Character;

namespace RPG_Combat
{
    public abstract class CombatEvent : IEvent
    {
        protected bool finished;
        protected int priority;
        protected CombatGameState state;
        protected Actor actor;

        public CombatEvent(Actor actor, CombatGameState state)
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
        public CETurn(Actor actor, CombatGameState state)
            : base(actor, state) { }

        public override void Execute(EventQueue queue)
        {
            LogManager.LogDebug($"Executing CETurn for {actor.name}");
            // Player first
            if (state.IsPartyMember(actor))
                HandlePlayerTurn();
            else
                HandleEnemyTurn();
        }

        private void HandlePlayerTurn()
        {
            var config = new CombatChoiceState.Config
            {
                Actor = actor,
                State = state
            };
            var nextstate = state.CombatChoice;
            nextstate.Init(config);
            state.CombatStack.Push(nextstate);
            finished = true;
        }

        private void HandleEnemyTurn()
        {
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
            var queue = state.EventQueue;
            var priority = attackEvent.CalculatePriority(queue);
            queue.Add(attackEvent, priority);
            finished = true;
        }

        public override string GetName()
        {
            return $"CombatEventTurn for {actor.name}";
        }
    }

    public class CEAttack : CombatEvent
    {
        public class Config
        {
            public bool IsCounter;
            public bool IsPlayer;
            public Actor Actor;
            public CombatGameState CombatState;
            public List<Actor> Targets = new List<Actor>();
        }

        private bool counter;
        private int speed = 0;
        private FormulaResult result;
        private Character character;
        private Storyboard storyboard;
        private Func<CombatGameState, List<Actor>> targeter;
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
            events.Add(StoryboardEventFunctions.Function(() => state.ShowNotice(string.Format(text, actor.Name))));
            if (config.IsPlayer)
            {
                //this.mAttackAnim = gEntities.slash
                targeter = (state) => CombatSelector.FindWeakestEnemy(state);
                var attackMoveParams = new CombatStateParams
                {
                    Direction = 1,
                    MovePosition = Vector2.left * 2.0f
                };
                var returnMoveParams = new CombatStateParams
                {
                    Direction = 1,
                    MovePosition = Vector2.right * 2.0f
                };
                events.Add(StoryboardEventFunctions.RunCombatState(character.Controller, Constants.COMBAT_MOVE_STATE, attackMoveParams));
                // TODO remove Temp for testing
                events.Add(StoryboardEventFunctions.Wait(1.0f));
                //
                events.Add(StoryboardEventFunctions.RunCombatState(character.Controller, Constants.RUN_ANIMATION_STATE, new CSRunAnimation.Config { Animation = Constants.ATTACK_ANIMATION }));
                events.Add(StoryboardEventFunctions.Function(DoAttack));
                events.Add(StoryboardEventFunctions.Function(ShowResult));
                events.Add(StoryboardEventFunctions.RunCombatState(character.Controller, Constants.COMBAT_MOVE_STATE, returnMoveParams));
                events.Add(StoryboardEventFunctions.Wait(1.0f));
                events.Add(StoryboardEventFunctions.Function(OnFinish));
            }
            else
            {
                //this.mAttackAnim = gEntities.claw
                targeter = (state) => CombatSelector.RandomPlayer(state);
                var targetPosition = (state.EnemyActors[0].transform.position - actor.transform.position) * 0.5f;
                targetPosition.y = actor.transform.position.y;
                var attackMoveParams = new CombatStateParams
                {
                    Direction = 1,
                    MovePosition = Vector2.right
                };
                var returnMoveParams = new CombatStateParams
                {
                    Direction = -1,
                    MovePosition = Vector2.left
                };
                events.Add(StoryboardEventFunctions.RunCombatState(character.Controller, Constants.COMBAT_MOVE_STATE, attackMoveParams));
                // TODO remove Temp for testing
                events.Add(StoryboardEventFunctions.Wait(1.0f));
                events.Add(StoryboardEventFunctions.RunCombatState(character.Controller, Constants.RUN_ANIMATION_STATE, new CSRunAnimation.Config { Animation = Constants.ATTACK_ANIMATION }));
                events.Add(StoryboardEventFunctions.Function(DoAttack));
                events.Add(StoryboardEventFunctions.Function(ShowResult));
                events.Add(StoryboardEventFunctions.RunCombatState(character.Controller, Constants.COMBAT_MOVE_STATE, returnMoveParams));
                events.Add(StoryboardEventFunctions.Wait(1.0f));
                events.Add(StoryboardEventFunctions.Function(OnFinish));
            }
            storyboard = new Storyboard(state.CombatStack, events);
        }

        public override void Execute(EventQueue queue)
        {
            LogManager.LogDebug($"Executing CEAttack for {actor.name}");
            state.CombatStack.Push(storyboard);
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
            state.HideNotice();
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
            var countered = CombatFormula.IsCounter(state, actor, target);
            if (countered)
            {
                LogManager.LogDebug($"Countering [{actor}] with Target [{target}]");
                state.ApplyCounter(target, actor);
            }
        }

        private void AttackTarget(Actor target)
        {
            LogManager.LogDebug($"Attacking {target.name}");
            result = CombatFormula.MeleeAttack(state, actor, target);
            var entity = target.GetComponent<Entity>();
            if (result.Result == CombatFormula.HitResult.Miss)
            {
                state.ApplyMiss(target);
                return;
            }
            if (result.Result == CombatFormula.HitResult.Dodge)
                state.ApplyDodge(target);
            else
            {
                var isCrit = result.Result == CombatFormula.HitResult.Critical;
                state.ApplyDamage(target, result.Damage, isCrit);
            }
            var entityPosition = entity.transform.position;
            // TODO
            //    local effect = AnimEntityFx:Create(x, y,
            //                            self.mAttackAnim,
            //                            self.mAttackAnim.frames)

            //    self.mState:AddEffect(effect)
        }

        private void ShowResult()
        {
            if (result == null)
                return;
            
            var message = CreateResultMessage();
            state.ShowNotice(message);
        }

        private string CreateResultMessage()
        {
            var localization = ServiceManager.Get<LocalizationManager>();
            if (result.Result == CombatFormula.HitResult.Miss)
            {
                var message = localization.Localize("ID_MISS_TEXT");
                return string.Format(message, actor.Name);
            }
            else if (result.Result == CombatFormula.HitResult.Dodge)
                return localization.Localize("ID_DODGE_TEXT");
            else if (result.Result == CombatFormula.HitResult.Hit)
            { 
                var message = localization.Localize("ID_ATTACK_HIT_TEXT");
                return string.Format(message, result.Damage);
            }
            else
            {
                var message = localization.Localize("ID_CRITICAL_HIT_TEXT");
                return string.Format(message, result.Damage);
            }
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
            public CombatGameState State;
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
                    state.ShowNotice("ID_ATTEMPT_FLEE_TEXT");
                }),
                StoryboardEventFunctions.Wait(1.0f)
            };
            if (CombatFormula.CanEscape(state, actor))
            {
                events.Add(StoryboardEventFunctions.Function(() => FleeSuccessPart1()));
                StoryboardEventFunctions.Wait(0.3f);
                events.Add(StoryboardEventFunctions.Function(() => FleeSuccessPart2()));
                StoryboardEventFunctions.Wait(0.6f);
            }
            else
            {
                events.Add(StoryboardEventFunctions.Function(() => state.ShowNotice("ID_FLEE_FAILED_TEXT")));
                StoryboardEventFunctions.Wait(0.3f);
                events.Add(StoryboardEventFunctions.Function(() => OnFleeFail()));
            }
            storyboard = new Storyboard(state.CombatStack, events);
            var character = actor.GetComponent<Character>();
            character.direction = direction;
            LogManager.LogDebug($"Trying to flee for {actor.Name}");
        }

        public override void Execute(EventQueue queue)
        {
            LogManager.LogDebug($"Executing CEFlee for {actor.name}");
            state.CombatStack.Push(storyboard);
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
            state.ShowNotice("ID_SUCCESS_TEXT");
            var targetPosition = actor.transform.position + Vector3.right * 5.0f; // Run off screen, does not matter if position is ever reached
            var moveParams = new MoveParams(targetPosition);
            actor.GetComponent<Character>().Controller.Change(Constants.MOVE_STATE, moveParams);
        }

        private void FleeSuccessPart2()
        {
            foreach (var actor in state.PartyActors)
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
            state.HideNotice();
        }

        private void OnFleeFail()
        {
            var character = actor.GetComponent<Character>();
            character.direction = direction == Direction.East ? Direction.West : Direction.East;
            character.Controller.Change(Constants.STAND_STATE);
            finished = true;
            state.HideNotice();
        }
    }

    public class CEUseItemEvent : CombatEvent
    {
        public class Config
        {
            public bool IsPlayer;
            public Actor Actor;
            public CombatGameState CombatState;
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
            var direction = config.IsPlayer ? 1 : -1;
            var attackMoveParams = new CombatStateParams
            {
                Direction = direction,
                MovePosition = Vector2.left * 2.0f
            };
            var returnMoveParams = new CombatStateParams
            {
                Direction = direction * -1,
                MovePosition = Vector2.right * 2.0f
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
            storyboard = new Storyboard(state.CombatStack, events);
        }

        public override void Execute(EventQueue queue)
        {
            LogManager.LogDebug($"Executing CEUseItemEvent for {actor.name}");
            state.CombatStack.Push(storyboard);
        }

        private void ShowItemNotice()
        {
            var notice = $"Item: {item.GetName()}";
            state.ShowNotice(notice);
        }

        private void HideNotice()
        { 
            state.HideNotice();
        }

        private void UseItem()
        {
            HideNotice();
            var position = actor.GetComponent<Entity>().GetSelectPosition();
            // TODO create effect
            /*local effect = AnimEntityFx:Create(pos:X(), pos:Y(),
                            gEntities.fx_use_item,
                            gEntities.fx_use_item.frames, 0.1)
              self.mState:AddEffect(effect)*/
            var config = new CombatActionConfig
            {
                Owner = actor,
                State = state,
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
            public CombatGameState CombatState;
            public Spell spell;
            public List<Actor> Targets = new List<Actor>();
        }

        private Spell spell;
        private StateMachine controller;
        private Storyboard storyboard;
        private List<Actor> targets;
        public CECastSpellEvent(Config config)
            : base(config.Actor, config.CombatState)
        {
            spell = config.spell;
            targets = config.Targets;
            finished = false;
            controller = actor.GetComponent<Character>().Controller;
            // TODO change to ready to attack state controller.Change(Constants.)
            // Remove now to take from inventory
            var direction = config.IsPlayer ? 1 : -1;
            var attackMoveParams = new CombatStateParams
            {
                Direction = direction,
                MovePosition = Vector2.left * 2.0f
            };
            var returnMoveParams = new CombatStateParams
            {
                Direction = direction * -1,
                MovePosition = Vector2.right * 2.0f
            };
            var events = new List<IStoryboardEvent>()
            { 
                StoryboardEventFunctions.Function(() => ShowNotice()),
                StoryboardEventFunctions.RunCombatState(controller, Constants.COMBAT_MOVE_STATE, attackMoveParams),
                StoryboardEventFunctions.Wait(0.5f),
                StoryboardEventFunctions.RunCombatState(controller, Constants.USE_STATE, new CSRunAnimation.Config { Animation = Constants.CAST_ANIMATION_STATE }), // TODO change to cast?
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
            storyboard = new Storyboard(state.CombatStack, events);
        }

        public override void Execute(EventQueue queue)
        {
            LogManager.LogDebug($"Executing CECastSpellEvent for {actor.name}");
            state.CombatStack.Push(storyboard);
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
            state.ShowNotice(notice);
        }

        private void HideNotice()
        {
            state.HideNotice();
        }

        private void Cast()
        {
            var position = actor.GetComponent<Character>().Entity.GetSelectPosition();
            // TODO effect
            //local effect = AnimEntityFx:Create(pos:X(), pos:Y(),
            //                        gEntities.fx_use_item,
            //                        gEntities.fx_use_item.frames, 0.1)
            //self.mState:AddEffect(effect)
            actor.ReduceManaForSpell(spell);
            state.UpdateActorMp(actor);
            var config = new CombatActionConfig
            {
                Owner = actor,
                State = state,
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