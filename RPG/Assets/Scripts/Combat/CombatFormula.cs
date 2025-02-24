﻿using System.Collections.Generic;
using UnityEngine;
using RPG_Character;
using RPG_GameData;

namespace RPG_Combat
{
    public class FormulaResult
    {
        public int Damage;
        public CombatFormula.HitResult Result;
    }

    public class CombatFormula : MonoBehaviour
    {
        private static FormulaResult result = new FormulaResult();

        public enum HitResult
        {
            Miss = 0,
            Dodge = 1,
            Hit = 2,
            Critical = 3
        }

        public static FormulaResult MeleeAttack(Actor attacker, Actor target)
        {
            var stats = attacker.Stats;
            var enemyStats = target.Stats;
            result.Damage = 0;
            var hitResult = IsHit(attacker, target);
            if (hitResult == HitResult.Miss)
            {
                result.Result = HitResult.Miss;
                return result;
            }
            if (IsDodge(attacker, target))
            {
                result.Result = HitResult.Miss;
                return result;
            }
            result.Damage = CalculateDamage(attacker, target);
            if (hitResult == HitResult.Hit)
            {
                result.Result = HitResult.Hit;
                return result;
            }

            DebugAssert.Assert(hitResult == HitResult.Critical, $"HitResult should be critical but is {hitResult}.");
            result.Damage += BaseAttack(attacker, target);
            result.Result = HitResult.Critical;
            return result;
        }

        public static HitResult IsHit(Actor attacker, Actor target)
        {
            var stats = attacker.Stats;
            var speed = stats.Get(Stat.Speed);
            var intelligence = stats.Get(Stat.Attack);
            // max value is 255 if we add then divide by 255 we get 0-1
            var bonus = ((speed + intelligence) * 0.5f) / 255.0f;
            var chanceToHit = Constants.CHANCE_TO_HIT + bonus * 0.5f;
            var value = Random.Range(0.0f, 1.0f);
            var isHit = value <= chanceToHit;
            var isCrit = value <= Constants.CHANCE_TO_CRIT;
            return isCrit ? HitResult.Critical : isHit ?
                HitResult.Hit : HitResult.Miss;
        }

        public static bool IsDodge(Actor attacker, Actor target)
        {
            var stats = attacker.Stats;
            var enemyStats = target.Stats;
            var speed = stats.Get(Stat.Speed);
            var enemySpeed = enemyStats.Get(Stat.Speed);
            float speedDifference = speed - enemySpeed;
            // Clamp to 0-1
            speedDifference = Mathf.Clamp(speedDifference, -10, 10) * 0.01f;
            var chanceToDodge = Mathf.Max(0, Constants.CHANCE_TO_DODGE + speedDifference);
            return Random.Range(0.0f, 1.0f) <= chanceToDodge;
        }

        public static bool IsCounter(Actor attacker, Actor target)
        {
            var counter = target.Stats.Get(Stat.Counter);
            return Random.Range(0, 1) * Constants.COUNTER_MULTIPLIER < counter;
        }

        public static int BaseAttack(Actor attacker, Actor target)
        {
            var attack = attacker.Stats.Get(Stat.Attack);
            var defense = target.Stats.Get(Stat.Defense);
            var attackIncrease = Random.Range(0, attack * 0.5f);
            var attackStrength = attack * attack  / (attack - defense);// + attackIncrease;
            return (int)Random.Range(attackStrength, attackStrength * 2);

        }

        public static int CalculateDamage(Actor attacker, Actor target)
        {
            var enemyStats = target.Stats;
            var defense = enemyStats.Get(Stat.Defense);
            var attack = BaseAttack(attacker, target);
            return (int)Mathf.Floor(Mathf.Max(0, attack - defense));
        }

        public static bool CanEscape(List<Actor> enemies, Actor actor)
        {
            var stats = actor.Stats;
            var speed = stats.Get(Stat.Speed);
            int enemyCount = enemies.Count;
            int totalSpeed = 0;
            foreach (var a in enemies)
            {
                var enemySpeed = a.Stats.Get(Stat.Speed);
                totalSpeed += enemySpeed;
            }
            var averageSpeed = totalSpeed / enemyCount;
            var escapeChance = Constants.ESCAPE_CHANCE;
            escapeChance += speed > averageSpeed ? Constants.ESCAPE_BONUS : -Constants.ESCAPE_BONUS;
            return Random.Range(0, 1) <= escapeChance;
        }

        public static bool CanSteal(Actor attacker, Actor target)
        {
            var chanceToSteal = Constants.CHANCE_TO_STEAL;
            if (attacker.Level > target.Level)
            {
                chanceToSteal = (Constants.STEAL_BONUS + attacker.Level - target.Level) / 128;
                chanceToSteal = Mathf.Clamp(chanceToSteal, Constants.STEAL_MIN_CHANCE, Constants.STEAL_MAX_CHANCE);
            }
            return Random.Range(0, 1) <= chanceToSteal;
        }

        public static HitResult IsHitMagic(Actor attacker, Actor target, Spell Spell)
        {
            var hitChance = Spell.HitChance;
            return Random.Range(0.0f, 1.0f) < hitChance ? HitResult.Hit : HitResult.Miss;
        }

        public static int CalculateSpellDamage(Actor attacker, Actor target, Spell Spell)
        {
            var damageRange = Spell.BaseDamage;
            var baseDamage = damageRange.x;
            if (damageRange.y != 0)
                baseDamage = Random.Range(damageRange.x, damageRange.y);
            var damage = baseDamage * 4;
            var level = attacker.Level;
            var stats = attacker.Stats;
            float bonus = level * stats.Get(Stat.Magic) * (baseDamage / 32);
            damage += bonus;
            if (Spell.SpellElement != SpellElement.None)
            {
                var modifier = target.Stats.GetSpellElementModifer(Spell.SpellElement);
                damage += damage * modifier;
            }

            var resistance = Mathf.Min(Constants.MAX_STAT_VALUE, target.Stats.Get(Stat.Resist));
            float resist = 1 - (resistance / 255.0f); // Divide by 255
            damage *= resist;
            return Mathf.FloorToInt(damage);
        }

        public static FormulaResult MagicAttack(Actor attacker, Actor target, Spell spell)
        {
            result.Damage = 0;
            result.Result = IsHitMagic(attacker, target, spell);
            if (result.Result == HitResult.Hit)
                result.Damage = CalculateSpellDamage(attacker, target, spell);
            return result;
        }
    }
}