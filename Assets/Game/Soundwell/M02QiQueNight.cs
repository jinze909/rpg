using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    public enum M02QiQueNightStage
    {
        NotStarted,
        AtWarehouse,
        ConfrontingDebt,
        OfferingCake,
        ReviewingChart,
        Finished
    }

    /// <summary>
    /// Night-time M02 reunion with Qi Que. The debt is never learned here
    /// retroactively: only M02 harbor's prior discovery makes confrontation
    /// possible. Listening to her offer is not voyage_path commitment.
    /// </summary>
    public sealed class M02QiQueNight
    {
        public M02QiQueNight(bool debtAlreadyKnown)
        {
            DebtAlreadyKnown = debtAlreadyKnown;
        }

        public bool DebtAlreadyKnown { get; }
        public M02QiQueNightStage Stage { get; private set; }
        public bool DebtConfronted { get; private set; }
        public bool ChartStudied { get; private set; }

        public void Begin()
        {
            Require(M02QiQueNightStage.NotStarted);
            Stage = M02QiQueNightStage.AtWarehouse;
        }

        public void Greet()
        {
            Require(M02QiQueNightStage.AtWarehouse);
            Stage = DebtAlreadyKnown ? M02QiQueNightStage.ConfrontingDebt :
                M02QiQueNightStage.OfferingCake;
        }

        public void DiscussDebt()
        {
            Require(M02QiQueNightStage.ConfrontingDebt);
            if (!DebtAlreadyKnown)
                throw new InvalidOperationException("A debt cannot be confronted before discovery.");
            DebtConfronted = true;
            Stage = M02QiQueNightStage.OfferingCake;
        }

        public void AcceptCake()
        {
            Require(M02QiQueNightStage.OfferingCake);
            Stage = M02QiQueNightStage.ReviewingChart;
            ChartStudied = true;
        }

        public void Leave()
        {
            Require(M02QiQueNightStage.ReviewingChart);
            Stage = M02QiQueNightStage.Finished;
        }

        // Host transitions away from the warehouse only after Leave succeeds.
        // When that callback fails, restore ReviewingChart while keeping the
        // player's debt-confrontation and chart state so retry stays safe.
        public void Leave(Action recordWithHost)
        {
            if (recordWithHost == null)
                throw new ArgumentNullException(nameof(recordWithHost));
            M02QiQueNightStage before = Stage;
            Leave();
            try
            {
                recordWithHost();
            }
            catch
            {
                Stage = before;
                throw;
            }
        }

        private void Require(M02QiQueNightStage expected)
        {
            if (Stage != expected)
                throw new InvalidOperationException(
                    "Qi Que's night conversation is not at " + expected + ".");
        }
    }

    /// <summary>
    /// M02 discovery flag read by the night scene; Makinom owns story facts.
    /// The office host calls TryRecordDiscovered only on the explicit
    /// player question about Qi Que's collateral.
    /// </summary>
    public static class M02QiQueDebtFacts
    {
        public const string DebtKnownKey = "rpg.return.m02.qi-debt-known";

        public static bool TryRecordDiscovered()
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;
            return TryRecordDiscovered(
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecordDiscovered(
            Func<string, int> getInt, Action<string, int> setInt)
        {
            if (getInt == null || setInt == null ||
                M06RescuePlanFacts.HasOrphanedDownstreamFacts(getInt))
                return false;
            setInt(DebtKnownKey, 1);
            return true;
        }

        public static bool TryRead(out bool debtKnown)
        {
            debtKnown = false;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;
            return TryRead(key => variables.GetInt(key), out debtKnown);
        }

        public static bool TryRead(Func<string, int> getInt, out bool debtKnown)
        {
            debtKnown = false;
            if (getInt == null)
                return false;
            debtKnown = getInt(DebtKnownKey) == 1;
            return true;
        }
    }

    public static class M02QiQueNightDialogue
    {
        public static string Describe(M02QiQueNight night, bool chinese)
        {
            if (night == null)
                throw new ArgumentNullException(nameof(night));

            switch (night.Stage)
            {
                case M02QiQueNightStage.AtWarehouse:
                    return chinese
                        ? "夜里，码头货栈。祁雀正要从点心匣里再递给你一块桂花糕。"
                        : "Night at the dock warehouse. Qi Que is about to offer another osmanthus cake.";
                case M02QiQueNightStage.ConfrontingDebt:
                    return chinese
                        ? "你提起韩峤账上的船贷。祁雀的手停在半空：“……韩峤那个嘴快的。是，船还押在他管的账下。我没说，是怕你嫌我拖后腿，另找别人。”"
                        : "You mention the loan in Han Qiao's ledger. Qi Que stops mid-reach: 'Han Qiao talks too much. Yes, my ship is still mortgaged under his office. I feared you would find someone else.'";
                case M02QiQueNightStage.OfferingCake:
                    return chinese
                        ? "祁雀把桂花糕塞进你手里：“我说过要帮你去赤盐，这话算数。”她正准备摊开海峡图。"
                        : "Qi Que presses the cake into your hand. 'I meant it when I promised to help you reach Red Salt.' She opens her sea chart.";
                case M02QiQueNightStage.ReviewingChart:
                    return chinese
                        ? "祁雀：“先去海边见见陆钧，听听他的条件。要是看中了我的商路，咱们再到码头商量。”你看过了海图，还没订船或确定航路。"
                        : "Qi Que: 'Visit Lu Jun on the coast and hear his terms. If you want my route, we can work out the details at the dock.' You've seen the chart but not booked a ship or chosen a route.";
                case M02QiQueNightStage.Finished:
                    return chinese
                        ? "你记下她的提议，离开货栈，动身去海边见陆钧。"
                        : "You leave the warehouse with her offer in mind and head to the coast to see Lu Jun.";
                default:
                    throw new InvalidOperationException("Begin the M02 night scene first.");
            }
        }
    }
}
