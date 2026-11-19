using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    public enum M04QiQueCupStage
    {
        NotStarted,
        OfferingCup,
        ChoosingReply,
        Responding,
        WithoutCup,
        Complete
    }

    public enum M04QiQueCupReply
    {
        TeaseUglyCup,
        AskForReplacement,
        PromiseToSettleDebt
    }

    /// <summary>
    /// The odd chipped cup is a physical gift only if Qi Que is there and
    /// actually hands it over. No ship choice, companion recruitment, or
    /// official passage is implied by this optional dockside farewell.
    /// </summary>
    public sealed class M04QiQueCupEncounter
    {
        public M04QiQueCupEncounter(bool qiQuePresent, bool debtKnown)
        {
            QiQuePresent = qiQuePresent;
            DebtKnown = debtKnown;
        }

        public bool QiQuePresent { get; }
        public bool DebtKnown { get; }
        public bool CupTaken { get; private set; }
        public M04QiQueCupReply? Reply { get; private set; }
        public M04QiQueCupStage Stage { get; private set; }

        public void Begin()
        {
            Require(M04QiQueCupStage.NotStarted);
            Stage = QiQuePresent ? M04QiQueCupStage.OfferingCup :
                M04QiQueCupStage.WithoutCup;
        }

        public void ExamineGift()
        {
            Require(M04QiQueCupStage.OfferingCup);
            Stage = M04QiQueCupStage.ChoosingReply;
        }

        public bool CanChoose(M04QiQueCupReply reply)
        {
            return Enum.IsDefined(typeof(M04QiQueCupReply), reply) &&
                (reply != M04QiQueCupReply.PromiseToSettleDebt || DebtKnown);
        }

        public void Choose(M04QiQueCupReply reply)
        {
            Require(M04QiQueCupStage.ChoosingReply);
            if (!CanChoose(reply))
                throw new InvalidOperationException("Cannot mention an undiscovered ship debt.");
            Reply = reply;
            // Only choosing a response to Qi Que's offered gift accepts it.
            CupTaken = true;
            Stage = M04QiQueCupStage.Responding;
        }

        public void Finish()
        {
            if (Stage != M04QiQueCupStage.Responding &&
                Stage != M04QiQueCupStage.WithoutCup)
                throw new InvalidOperationException("The cup farewell is not settled.");
            Stage = M04QiQueCupStage.Complete;
        }

        private void Require(M04QiQueCupStage stage)
        {
            if (Stage != stage)
                throw new InvalidOperationException("M04 cup dialogue is not at " + stage + ".");
        }
    }

    public static class M04QiQueCupFacts
    {
        public const string CupCarriedKey = "rpg.return.m04.qi-cup-carried";

        // A replay of the dockside encounter must not reuse the last voyage's
        // physical gift. The M04 host calls this before opening the farewell.
        internal static bool TryResetForNewVisit()
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;
            variables.Set(CupCarriedKey, 0);
            return true;
        }

        // Missing data is never evidence that a cup teleported aboard.
        // Makinom records the physical gift; ORK remains save authority.
        public static bool TryRecord(M04QiQueCupEncounter cup)
        {
            if (cup == null) throw new ArgumentNullException(nameof(cup));
            if (cup.Stage != M04QiQueCupStage.Complete)
                throw new InvalidOperationException("Incomplete M04 farewells cannot be recorded.");
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                return false;
            Maki.Game.Variables.Set(CupCarriedKey, cup.CupTaken ? 1 : 2);
            return true;
        }

        public static bool TryRead(out bool carrying)
        {
            carrying = false;
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                return false;
            // 0/unknown means the encounter has not happened; 2 explicitly
            // means no physical cup. Only a completed farewell writes either.
            int recorded = Maki.Game.Variables.GetInt(CupCarriedKey);
            if (recorded != 1 && recorded != 2)
                return false;
            carrying = recorded == 1;
            return true;
        }
    }

    public static class M04QiQueCupCopy
    {
        public static string Narration(M04QiQueCupEncounter cup, bool chinese)
        {
            if (cup == null) throw new ArgumentNullException(nameof(cup));
            switch (cup.Stage)
            {
                case M04QiQueCupStage.OfferingCup:
                    return chinese
                        ? "出发前夜，祁雀在停靠岸边的甲板上打开一只木箱，摆出各地收来的丑杯子。她挑出缺口的黑泥杯：出海带着。丑到你想活着回来换掉它。"
                        : "Before departure Qi Que opens a box of hideous cups on the moored deck. She offers a chipped black-clay cup: 'Take it sailing. Ugly enough to make you return alive and replace it.'";
                case M04QiQueCupStage.ChoosingReply:
                    return chinese
                        ? "杯子还在她手边。收下，放进随船的包里，再告诉她你怎么想。"
                        : "The cup still rests beside her. Accept it into your voyage bag, then answer.";
                case M04QiQueCupStage.Responding:
                    string response;
                    switch (cup.Reply)
                    {
                        case M04QiQueCupReply.TeaseUglyCup:
                            response = chinese ? "你：这杯子确实丑得让人睡不着。" :
                                "You: 'That cup really is too ugly to sleep beside.'";
                            break;
                        case M04QiQueCupReply.AskForReplacement:
                            response = chinese ? "你：等我把沈砾带回来，你得给我换个好看的。" :
                                "You: 'When I bring Shen Li back, you owe me a prettier one.'";
                            break;
                        case M04QiQueCupReply.PromiseToSettleDebt:
                            response = chinese ? "你：船贷的事，等这趟回来一起平。" :
                                "You: 'We'll settle that ship loan when I return.'";
                            break;
                        default:
                            throw new InvalidOperationException("A cup answer must be selected.");
                    }
                    return response + (chinese
                        ? "\n祁雀晃了晃自己的茶杯：我连商号的招牌名字都想好了。你只管别在半路把命丢了。"
                        : "\nQi Que raises her own cup: 'I've already named my trading house. Just don't lose your life halfway there.'");
                case M04QiQueCupStage.WithoutCup:
                    return chinese
                        ? "这趟临行前没能见到祁雀，也没有收到那只黑泥杯。将来需用事先约定的普通遮光识别灯。"
                        : "You did not meet Qi Que or receive the chipped cup before departure. A conventional pre-arranged signal lamp will be needed instead.";
                case M04QiQueCupStage.Complete:
                    return cup.CupTaken
                        ? chinese ? "你收起缺口黑泥杯，继续准备启航。" :
                            "The chipped cup is packed for departure."
                        : chinese ? "你没有收到杯子，继续准备启航。" :
                            "You leave without the cup and continue preparations.";
                default:
                    throw new InvalidOperationException("Begin M04 before showing the farewell.");
            }
        }

        public static string[] Choices(M04QiQueCupEncounter cup, bool chinese)
        {
            if (cup == null) throw new ArgumentNullException(nameof(cup));
            if (cup.Stage != M04QiQueCupStage.ChoosingReply)
                return Array.Empty<string>();
            if (chinese)
                return cup.DebtKnown
                    ? new[] { "这杯子确实丑得让人睡不着。", "回来你得给我换个好看的。", "船贷的事回来一起平。" }
                    : new[] { "这杯子确实丑得让人睡不着。", "回来你得给我换个好看的。" };
            return cup.DebtKnown
                ? new[] { "That cup will keep me awake.", "Replace it when I return.", "We'll settle the loan when I return." }
                : new[] { "That cup will keep me awake.", "Replace it when I return." };
        }
    }
}
