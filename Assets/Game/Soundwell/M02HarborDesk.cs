using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    public enum M02HarborDeskStage
    {
        NotStarted,
        Discussing,
        Finished
    }

    public enum M02HarborDeskAction
    {
        PresentLivingRegistration,
        AskAboutQiQueShip,
        AskAboutLuJunEscort,
        AssistTransferList
    }

    /// <summary>
    /// M02's first harbor-office visit. The M01 pension choice is only intent:
    /// registration is effective after papers are actually stamped here.
    /// Discussing Qi Que's boat can reveal its debt without changing voyage_path.
    /// Makinom owns persisted facts; the scene host commits an accepted outcome.
    /// </summary>
    public sealed class M02HarborDesk
    {
        public M02HarborDesk(M01PensionChoice priorPensionChoice)
        {
            M01HomecomingResponse.PensionPathFor(priorPensionChoice);
            PriorPensionChoice = priorPensionChoice;
        }

        public M01PensionChoice PriorPensionChoice { get; }
        public M02HarborDeskStage Stage { get; private set; }
        public bool LivingRegistryStamped { get; private set; }
        public bool QiQueDebtDiscovered { get; private set; }
        public bool LuJunEscortDiscussed { get; private set; }
        public bool TransferAssistanceAccepted { get; private set; }
        public M02HarborDeskAction? LastAction { get; private set; }

        public bool CanPresentLivingPapers =>
            PriorPensionChoice == M01PensionChoice.RegisterLivingIdentity &&
            !LivingRegistryStamped;

        public void Begin()
        {
            Require(M02HarborDeskStage.NotStarted);
            Stage = M02HarborDeskStage.Discussing;
        }

        public bool Act(M02HarborDeskAction action)
        {
            Require(M02HarborDeskStage.Discussing);
            bool changed;
            switch (action)
            {
                case M02HarborDeskAction.PresentLivingRegistration:
                    if (!CanPresentLivingPapers)
                        throw new InvalidOperationException(
                            "M02 cannot stamp living papers without the M01 public-registration decision.");
                    LivingRegistryStamped = true;
                    changed = true;
                    break;
                case M02HarborDeskAction.AskAboutQiQueShip:
                    changed = !QiQueDebtDiscovered;
                    QiQueDebtDiscovered = true;
                    break;
                case M02HarborDeskAction.AskAboutLuJunEscort:
                    changed = !LuJunEscortDiscussed;
                    LuJunEscortDiscussed = true;
                    break;
                case M02HarborDeskAction.AssistTransferList:
                    changed = !TransferAssistanceAccepted;
                    TransferAssistanceAccepted = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }

            LastAction = action;
            return changed;
        }

        public M02HarborDeskOutcome Finish()
        {
            Require(M02HarborDeskStage.Discussing);
            Stage = M02HarborDeskStage.Finished;
            return new M02HarborDeskOutcome(LivingRegistryStamped,
                QiQueDebtDiscovered, TransferAssistanceAccepted);
        }

        // Makinom persists observed harbor-desk facts only when leaving the desk.
        // When that host write fails, restore Discussing while keeping stamped
        // registration, debt discovery, and assistance state so retry stays safe.
        public void Finish(Action<M02HarborDeskOutcome> recordWithHost)
        {
            if (recordWithHost == null)
                throw new ArgumentNullException(nameof(recordWithHost));
            M02HarborDeskStage before = Stage;
            M02HarborDeskOutcome outcome = Finish();
            try
            {
                recordWithHost(outcome);
            }
            catch
            {
                Stage = before;
                throw;
            }
        }

        private void Require(M02HarborDeskStage expected)
        {
            if (Stage != expected)
                throw new InvalidOperationException(
                    "M02 harbor office is not at " + expected + ".");
        }
    }

    /// <summary>
    /// Immutable observed outcome. The office offers a first official ship
    /// after a stamped registration, but does NOT choose/complete voyage_path.
    /// Accepting transfer assistance may later support a missing-return
    /// application; it does NOT itself approve one or change pension_path.
    /// </summary>
    public sealed class M02HarborDeskOutcome
    {
        internal M02HarborDeskOutcome(bool stamped, bool qiDebt, bool assisted)
        {
            LivingRegistryStamped = stamped;
            QiQueDebtDiscovered = qiDebt;
            TransferAssistanceAccepted = assisted;
        }

        public bool LivingRegistryStamped { get; }
        public bool QiQueDebtDiscovered { get; }
        public bool TransferAssistanceAccepted { get; }
        public bool OfficialShipOffered => LivingRegistryStamped;
    }

    /// <summary>
    /// The official dock requires a real M02 stamp: either living papers
    /// accepted at this desk or the separate missing-return approval.
    /// M01's stated intention and an unapproved M02 request cannot book it.
    /// Makinom owns these facts; ORK owns the save lifecycle.
    /// </summary>
    public static class M02OfficialDockFacts
    {
        public const string LivingRegistrationStampedKey =
            "rpg.return.m02.living-registration-stamped";

        public static bool IsEffective(bool livingRegistrationStamped,
            bool missingReturnApproved) =>
            livingRegistrationStamped || missingReturnApproved;

        public static bool TryRecordLivingRegistration(M02HarborDeskOutcome outcome)
        {
            if (outcome == null)
                throw new ArgumentNullException(nameof(outcome));
            if (!outcome.LivingRegistryStamped)
                throw new InvalidOperationException(
                    "Only the observed M02 living-registration stamp can unlock official passage.");
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;
            return TryRecordLivingRegistration(outcome,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecordLivingRegistration(M02HarborDeskOutcome outcome,
            Func<string, int> getInt, Action<string, int> setInt)
        {
            if (outcome == null)
                throw new ArgumentNullException(nameof(outcome));
            if (!outcome.LivingRegistryStamped)
                throw new InvalidOperationException(
                    "Only the observed M02 living-registration stamp can unlock official passage.");
            if (getInt == null || setInt == null ||
                M06RescuePlanFacts.HasOrphanedDownstreamFacts(getInt))
                return false;
            setInt(LivingRegistrationStampedKey, 1);
            return true;
        }

        public static bool TryReadEffective(out bool officialRegistrationEffective)
        {
            officialRegistrationEffective = false;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;

            return TryReadEffective(key => variables.GetInt(key),
                out officialRegistrationEffective);
        }

        public static bool TryReadEffective(Func<string, int> getInt,
            out bool officialRegistrationEffective)
        {
            officialRegistrationEffective = false;
            if (getInt == null)
                return false;

            officialRegistrationEffective = IsEffective(
                getInt(LivingRegistrationStampedKey) == 1,
                getInt(M02MissingReturnFacts.ApprovalKey) == 1);
            return true;
        }
    }

    public static class M02HarborDeskDialogue
    {
        public static string Introduction(M02HarborDesk desk, bool chinese)
        {
            if (desk == null)
                throw new ArgumentNullException(nameof(desk));
            if (desk.Stage == M02HarborDeskStage.NotStarted)
                throw new InvalidOperationException("Begin the M02 office before rendering dialogue.");

            string initial = chinese
                ? "韩峤拨了拨算盘：“按三十个人备四天的面，就够我算一阵。船坞里头一条船我能作保，得有港务署盖过印的身份文书。”"
                : "Han Qiao works his abacus. 'Just the flour for thirty people for four days takes some reckoning. I can vouch for the first ship. The dock needs identity papers stamped by this office.'";

            if (desk.LivingRegistryStamped)
                return initial + (chinese
                    ? "\n你的活人户籍呈文已经盖印。第一条官船可以备着，走哪条航路仍由你定。"
                    : "\nYour living-registration papers bear the seal. The first official ship can be held for you. The route is still yours to choose.");
            if (desk.PriorPensionChoice == M01PensionChoice.RegisterLivingIdentity)
                return initial + (chinese
                    ? "\n你在西巷决定恢复活人户籍，呈文还在你手里，等着递交。"
                    : "\nIn West Lane you decided to restore your living registration. You still have the papers in hand, ready to present.");
            if (desk.PriorPensionChoice == M01PensionChoice.KeepIdentityUnregistered)
                return initial + (chinese
                    ? "\n你在西巷答应姜婶暂不登记。韩峤收起船坞的文书：“商船和旧部的护卫，你都还可以问。”"
                    : "\nIn West Lane you agreed to stay off the register. Han Qiao sets the dock papers aside. 'You can still ask about a merchant ship or an escort from the old brigade.'");
            return initial + (chinese
                ? "\n韩峤：“失踪归还的申请可以先递。要批，我得有凭据，或有你协理移交的约定。等这份文书盖了印，船坞也认。”"
                : "\nHan Qiao: 'You can file a missing-return petition now. I need evidence or your help checking the transfer list before I can approve it. Once it bears my seal, the dock will take it.'");
        }

        public static string Result(M02HarborDeskAction action, bool chinese)
        {
            switch (action)
            {
                case M02HarborDeskAction.PresentLivingRegistration:
                    return chinese
                        ? "你递上用真名恢复户籍的呈文。韩峤当场盖印：第一条官船给你备着，但尚未选定实际航路。"
                        : "You present your living-registration petition under your real name. Han Qiao stamps it and offers to prepare the first official ship; no voyage route is chosen yet.";
                case M02HarborDeskAction.AskAboutQiQueShip:
                    return chinese
                        ? "韩峤：祁雀的船贷下个月初五到期，船还押在港务署，我管着债权。她没跟你说？"
                        : "Han Qiao: 'Qi Que's ship loan comes due on the fifth of next month. Her ship is still mortgaged at this office, and I hold the account. She didn't tell you?'";
                case M02HarborDeskAction.AskAboutLuJunEscort:
                    return chinese
                        ? "陆钧的旧部能出护卫和矿营线人，但他们要求公开挂第三旅旧军旗。能谈，不代表你已经选了这条船。"
                        : "Lu Jun's former soldiers can provide escorts and mine contacts, but insist on openly flying the Third Brigade flag. Asking about them does not choose that ship.";
                case M02HarborDeskAction.AssistTransferList:
                    return chinese
                        ? "你答应替韩峤协理赎俘移交，核对镜湾来的名单。伊蕾能提供合法登岛窗口，不能命令私人押运队。此事不自动批准“失踪归还”。"
                        : "You agree to help verify the transferred captive list. Ilei can offer a lawful landing window but cannot command private escorts. This does not approve a missing-return petition.";
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }
    }
}
