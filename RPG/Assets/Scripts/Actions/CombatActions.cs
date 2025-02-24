using System;
using System.Collections.Generic;
using UnityEngine;
using RPG_Character;
using RPG_GameData;

namespace RPG_Combat
{
    public class CombatActionConfig
    {
        public string StateId;
        public Actor Owner;
        public List<Actor> Targets;
        public object Def;
    }

    public class CombatActions
	{
        public static void ApplyCounter(Actor target, Actor attacker, ICombatState combat)
        {
            LogManager.LogDebug($"Target [{target.name}] countered Attacker [{attacker.name}].");
            var targetAlive = target.Stats.Get(Stat.HP) > 0;
            if (!targetAlive)
                return;
            var config = new CEAttack.Config
            {
                IsCounter = true,
                IsPlayer = combat.IsPartyMember(target),
                CombatState = combat,
                Targets = new List<Actor>() { attacker },
                Actor = target
            };
            var attack = new CEAttack(config);
            int speed = -1;
            combat.CombatTurnHandler().AddEvent(attack, speed);
        }

        public static void ApplyMiss(Actor target)
        {
            LogManager.LogDebug($"Target [{target.name}] missed.");
        }

        public static void ApplyDodge(Actor target)
        {
            LogManager.LogDebug($"Target [{target.name}] dodged.");
            if (target == null)
            {
                LogManager.LogError("Null target passed to Applydodge.");
                return;
            }
            var character = target.GetComponent<Character>();
            if (character.Controller.CurrentState.GetName() != Constants.HURT_STATE)
            {
                character.Controller.Change(Constants.HURT_STATE, new CombatStateParams { State = character.Controller.CurrentState.GetName() });
            }
        }

        public static void ApplyDamage(Actor target, int damage, bool isCrit)
        {
            var stats = target.Stats;
            var hp = stats.Get(Stat.HP) - damage;
            stats.SetStat(Stat.HP, hp);
            LogManager.LogDebug($"{target.name} took {damage} damage and HP is now {hp}. Was critical hit: {isCrit}");
            var controller = target.GetComponent<Character>().Controller;
            if (damage > 0 && controller.CurrentState.GetName() != Constants.HURT_STATE)
            {
                controller.Change(Constants.HURT_STATE, new CombatStateParams { State = controller.CurrentState.GetName() });
                GameEventsManager.BroadcastMessage(GameEventConstants.ON_DAMAGE_TAKEN, target);
            }
        }

        public static void RunAction(string action, CombatActionConfig config)
        {
            if (config == null)
            {
                LogManager.LogError("Null Config passed to RunAction.");
                return;
            }
            switch (action)
            {
                case Constants.HP_RESTORE_COMBAT_ACTION:
                    HpRestore(config);
                    break;
                case Constants.MP_RESTORE_COMBAT_ACTION:
                    HpRestore(config);
                    break;
                case Constants.REVIVE_COMBAT_ACTION:
                    HpRestore(config);
                    break;
                case Constants.SPELL_COMBAT_ACTION:
                    ElementSpell(config);
                    break;
                default:
                    LogManager.LogError($"Unknown action [{action}] passed to RunAction.");
                    break;
            }
        }

        private static void HpRestore(CombatActionConfig config)
        {
            if (!(config.Def is ItemUse itemUse) || itemUse == null)
            {
                LogManager.LogError("ItemUse was null inside Config. Cannot Run HP Restore.");
                return;
            }
            var restoreAmount = itemUse.Amount > 0 ? itemUse.Amount : 250;
            foreach(var target in config.Targets)
            {
                var stats = target.Stats;
                var maxHP = stats.Get(Stat.MaxHP);
                var hp = stats.Get(Stat.HP);
                if (hp <= 0)
                {
                    LogManager.LogDebug($"Actor [{target.Name}] has HP [{hp}], cannot use HpRestore.");
                    continue;
                }
                stats.SetStat(Stat.HP, Mathf.Max(maxHP, hp + restoreAmount));
            }
            // TODO effects

            //    if stateId == "item" then
            //        return
            //    end

            //    local animEffect = gEntities.fx_restore_hp
            //    local restoreColor = Vector.Create(0, 1, 0, 1)

            //    for k, v in ipairs(targets) do

            //                local stats, character, entity = StatsCharEntity(state, v)

            //        if stats:Get("hp_now") > 0 then
            //            AddTextNumberEffect(state, entity, restoreAmount, restoreColor)
            //        end

            //        AddAnimEffect(state, entity, animEffect, 0.1)
            //    end

        }


        private static void ElementSpell(CombatActionConfig config)
        {
            if (!(config.Def is Spell spell) || spell == null)
            {
                LogManager.LogError("Spell was null inside Config. Cannot Run ElementSpell.");
                return;
            }
            foreach(var target in config.Targets)
            {
                var result = CombatFormula.MagicAttack(config.Owner, target, spell);
                LogManager.LogDebug($"{config.Owner.Name} cast spell {spell.LocalizedName()} at {target.Name}. Result is {result.Result}, damage is {result.Damage}");
                if (result.Result == CombatFormula.HitResult.Hit)
                    ApplyDamage(target, result.Damage, false);
            }
            // TODO Spell effect
        }

        private static void Revive(CombatActionConfig config)
        {
            if (!(config.Def is ItemUse itemUse) || itemUse == null)
            {
                LogManager.LogError("ItemUse was null inside Config. Cannot Run Revive.");
                return;
            }

            var restoreAmount = itemUse.Amount > 0 ? itemUse.Amount : 250;
            foreach(var target in config.Targets)
            {
                var stats = target.Stats;
                var maxHP = stats.Get(Stat.MaxHP);
                var hp = stats.Get(Stat.HP);
                if (hp > 0)
                {
                    LogManager.LogDebug($"Actor [{target.Name}] has HP [{hp}], cannot use Revive.");
                    continue;
                }
                stats.SetStat(Stat.HP, Mathf.Max(maxHP, hp + restoreAmount));
                target.GetComponent<Character>().Controller.SetCurrentState(Constants.STAND_STATE);
            }
        }
    }
}