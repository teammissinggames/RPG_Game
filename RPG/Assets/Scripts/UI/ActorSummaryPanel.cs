﻿using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RPG_Character;

namespace RPG_UI
{
    public class ActorSummaryPanel : ConfigMonoBehaviour
    {
        public class Config
        {
            public bool ShowExp;
            public Actor Actor;
        }

        [SerializeField] Image CharacterImage;
        [SerializeField] TextMeshProUGUI NameText;
        [SerializeField] TextMeshProUGUI LevelText;
        [SerializeField] TextMeshProUGUI HpText;
        [SerializeField] TextMeshProUGUI MpText;
        [SerializeField] TextMeshProUGUI ExpText;
        [SerializeField] ProgressBar HpBar;
        [SerializeField] ProgressBar MpBar;
        [SerializeField] ProgressBar ExpBar;

        private Config config;

        public void Init(Config config)
        {
            if (CheckUIConfigAndLogError(config, name))
                return;
            this.config = config;
            var actor = config.Actor;
            CharacterImage.gameObject.SetActive(false);
            CharacterImage.sprite = ServiceManager.Get<AssetManager>().Load<Sprite>(Constants.PORTRAIT_PATH + actor.Portrait, (_) => CharacterImage.gameObject.SetActive(true));
            NameText.SetText(ServiceManager.Get<LocalizationManager>().Localize(actor.Name));
            LevelText.SetText(actor.Level.ToString());

            var exp = actor.Exp;
            var nextLevelExp = actor.NextLevelExp;
            ExpBar.SetTargetFillAmountImmediate(exp / (float)nextLevelExp);
            ExpText.gameObject.SetActive(config.ShowExp);
            if (config.ShowExp)
            {
                ExpText.SetText(string.Format(Constants.STAT_FILL_TEXT, exp, nextLevelExp));
            }

            var stats = actor.Stats;
            var hp = stats.Get(Stat.HP);
            var maxHp = stats.Get(Stat.MaxHP);
            var mp = stats.Get(Stat.MP);
            var maxMp = stats.Get(Stat.MaxMP);
            HpText.SetText(string.Format(Constants.STAT_FILL_TEXT, hp, maxHp));
            MpText.SetText(string.Format(Constants.STAT_FILL_TEXT, mp, maxMp));
            HpBar.SetTargetFillAmountImmediate(hp / (float)maxHp);
            MpBar.SetTargetFillAmountImmediate(mp / (float)maxMp);
            gameObject.SetActive(true);
        }

        /*
         function ActorSummary:Create(actor, params)
            params = params or {}

            local this =
            {
                mX = 0,
                mY = 0,
                mWidth = 340, -- width of entire box
                mActor = actor,
                mHPBar = ProgressBar:Create
                {
                    value = actor.mStats:Get("hp_now"),
                    maximum = actor.mStats:Get("hp_max"),
                    background = Texture.Find("hpbackground.png"),
                    foreground = Texture.Find("hpforeground.png"),
                },
                mMPBar = ProgressBar:Create
                {
                    value = actor.mStats:Get("mp_now"),
                    maximum = actor.mStats:Get("mp_max"),
                    background = Texture.Find("mpbackground.png"),
                    foreground = Texture.Find("mpforeground.png"),
                },
                mAvatarTextPad = 14,
                mLabelRightPad = 15,
                mLabelValuePad = 8,
                mVerticalPad = 18,
                mShowXP = params.showXP
            }

            if this.mShowXP then
                this.mXPBar = ProgressBar:Create
                {
                    value = actor.mXP,
                    maximum = actor.mNextLevelXP,
                    background = Texture.Find("xpbackground.png"),
                    foreground = Texture.Find("xpforeground.png"),
                }
            end

            setmetatable(this, self)
            this:SetPosition(this.mX, this.mY)
            return this
        end

        function ActorSummary:SetPosition(x, y)
            self.mX = x
            self.mY = y

            if self.mShowXP then
                local boxRight = self.mX + self.mWidth
                local barX = boxRight - self.mXPBar.mHalfWidth
                local barY = self.mY - 44
                self.mXPBar:SetPosition(barX, barY)
            end

            -- HP & MP
            local avatarW = self.mActor.mPortraitTexture:GetWidth()
            local barX = self.mX + avatarW + self.mAvatarTextPad
            barX = barX + self.mLabelRightPad + self.mLabelValuePad
            barX = barX + self.mMPBar.mHalfWidth

            self.mMPBar:SetPosition(barX, self.mY - 68)
            self.mHPBar:SetPosition(barX, self.mY - 50)
        end

        function ActorSummary:GetCursorPosition()
            return Vector.Create(self.mX, self.mY - 40)
        end

        function ActorSummary:Render(renderer)


            local statFont = gGame.Font.stat
            local font = gGame.Font.default
            local actor = self.mActor

            --
            -- Position avatar image from top left
            --
            local avatar = actor.mPortrait
            local avatarW = actor.mPortraitTexture:GetWidth()
            local avatarH = actor.mPortraitTexture:GetHeight()
            local avatarX = self.mX + avatarW * 0.5
            local avatarY = self.mY - avatarH * 0.5

            avatar:SetPosition(avatarX, avatarY)
            renderer:DrawSprite(avatar)

            --
            -- Position basic stats to the left of the
            -- avatar
            --
            font:AlignText("left", "top")


            local textPadY = 2
            local textX = avatarX + avatarW * 0.5 + self.mAvatarTextPad
            local textY = self.mY - textPadY
            font:DrawText2d(renderer, textX, textY, actor.mName)

            --
            -- Draw LVL, HP and MP labels
            --
            font:AlignText("right", "top")
            textX = textX + self.mLabelRightPad
            textY = textY - 20
            local statsStartY = textY
            font:DrawText2d(renderer, textX, textY, "LV")
            textY = textY - self.mVerticalPad
            font:DrawText2d(renderer, textX, textY, "HP")
            textY = textY - self.mVerticalPad
            font:DrawText2d(renderer, textX, textY, "MP")
            --
            -- Fill in the values
            --
            local textY = statsStartY
            local textX = math.floor(textX + self.mLabelValuePad)
            font:AlignText("left", "top")
            statFont:AlignText("left", "top")
            local level = actor.mLevel
            local hp = actor.mStats:Get("hp_now")
            local maxHP = actor.mStats:Get("hp_max")
            local mp = actor.mStats:Get("mp_now")
            local maxMP = actor.mStats:Get("mp_max")

            local counter = "%d/%d"
            local hp = string.format(counter,
                                     hp,
                                     maxHP)
            local mp = string.format(counter,
                                     mp,
                                     maxMP)


            statFont:DrawText2d(renderer, textX, textY, level)
            textY = textY - self.mVerticalPad

            statFont:DrawText2d(renderer, textX, textY, hp)
            textY = textY - self.mVerticalPad

            statFont:DrawText2d(renderer, textX, textY, mp)


            --
            -- Next Level area
            --
            if self.mShowXP then
                self.mXPBar:Render(renderer)

                local boxRight = self.mX + self.mWidth
                local textY = statsStartY
                local left = boxRight - self.mXPBar.mHalfWidth * 2

                font:AlignText("left", "top")
                font:DrawText2d(renderer, left, textY, "Next Level")
            end

            --
            -- MP & HP bars
            --
            self.mHPBar:Render(renderer)
            self.mMPBar:Render(renderer)
        end


             */
    }
}