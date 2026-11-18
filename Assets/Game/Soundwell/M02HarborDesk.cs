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
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                return false;
            Maki.Game.Variables.Set(LivingRegistrationStampedKey, 1);
            return true;
        }

        public static bool TryReadEffective(out bool officialRegistrationEffective)
        {
            officialRegistrationEffective = false;
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                return false;

            var variables = Maki.Game.Variables;
            officialRegistrationEffective = IsEffective(
                variables.GetInt(LivingRegistrationStampedKey) == 1,
                variables.GetInt(M02MissingReturnFacts.ApprovalKey) == 1);
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
                ? "韩峤还在算粮。第一条官船可以备给你，但船坞只认实际盖印的活人户籍；三十个人四天的口粮也要有人负责。"
                : "Han Qiao is counting provisions. He can prepare the first official ship, but the dock requires an actually stamped living registry and four days of supplies for thirty passengers.";

            string registration = desk.LivingRegistryStamped
                ? (chinese ? "\n韩峤已经在你的活人户籍呈文上盖印；这不是替整船人担保，更不是已选定航路。"
                           : "\nHan Qiao has stamped the living-registration papers. He has not guaranteed everyone aboard or selected your voyage route.")
                : desk.PriorPensionChoice == M01PensionChoice.RegisterLivingIdentity
                    ? (chinese ? "\n你在西巷决定公开身份，但尚未递交盖印呈文。"
                               : "\nYou decided to register in West Lane, but the papers are not yet stamped.")
                    : (chinese ? "\n你尚未公开登记活人身份。失踪归还须另有证据或协理移交后再申请，不能自动获准。"
                               : "\nYou have no public living registration. Missing-return review needs separate evidence or transfer assistance and later approval.");

            return initial + registration;
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
