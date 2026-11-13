using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    [CreateAssetMenu(menuName = "RPG/Soundwell/Party Config", fileName = "SoundwellPartyConfig")]
    public sealed class SoundwellPartyConfig : ScriptableObject
    {
        [SerializeField] private string rescueQuestGuid;
        [SerializeField] private int rescueExperience;
        [SerializeField] private int experienceStatus;
        [SerializeField] private Vector2 secondFollowerOffset;
        [SerializeField] private Vector2 thirdFollowerOffset;
        [SerializeField, Min(0f)] private float formationFollowSpeed;
        [SerializeField, Min(0f)] private float followerLagSeconds;
        [SerializeField, Min(0.01f)] private float walkFrameSeconds;
        [SerializeField, Min(0f)] private float leaderMoveSpeed;
        [SerializeField, Range(0f, 1f)] private float carryingSpeedMultiplier;
        [SerializeField, Min(0f)] private float battleStepDistance;

        private static SoundwellPartyConfig cached;
        public static SoundwellPartyConfig Load()
        {
            if (cached == null)
                cached = Resources.Load<SoundwellPartyConfig>("SoundwellPartyConfig");
            if (cached == null)
                throw new InvalidOperationException("Missing Assets/Game/Resources/SoundwellPartyConfig.asset");
            return cached;
        }

        public string RescueQuestGuid => rescueQuestGuid;
        public int RescueExperience => rescueExperience;
        public int ExperienceStatus => experienceStatus;
        public Vector2 SecondFollowerOffset => secondFollowerOffset;
        public Vector2 ThirdFollowerOffset => thirdFollowerOffset;
        public float FormationFollowSpeed => formationFollowSpeed;
        public float FollowerLagSeconds => followerLagSeconds;
        public float WalkFrameSeconds => walkFrameSeconds;
        public float LeaderMoveSpeed => leaderMoveSpeed;
        public float CarryingSpeedMultiplier => carryingSpeedMultiplier;
        public float BattleStepDistance => battleStepDistance;
    }
}
