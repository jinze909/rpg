using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class M16EpilogueLettersViewTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        internal static void SeedM15WithCompletedRC01AndST02(Dictionary<string, int> values)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var manifest = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant,
                "Confirmed hull", "A Yuan", 4, "Shallow tender",
                true, 16, 8, "Named receiver", false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);

            var fog = new FogListeningSession(
                new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 80) },
                maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 10, true, true), read, write), Is.True);
            Assert.That(M06RescuePlanFacts.TryRecord(
                new M06ReunionRecord(
                    M06RescuePlan.OfficialHandover, true,
                    M06LetterDeliveryOutcome.NotCarried, true), read, write), Is.True);
            Assert.That(M07ExtractionFacts.TryRecord(new M07ExtractionRecord(
                M07ExecutedRoute.OfficialHandover,
                M07PumpShutdownState.KeptRunning,
                M07RouteCostFlags.HarborSupplyDebt,
                boardedFirstBatchCount: 10,
                lowerLevelEvacuatedCount: 0,
                M07RusFate.Arrested, M07LedgerState.Preserved,
                M07LiangChoice.StayOnIsland,
                M07YidaChoice.HandToInspector,
                taoJiRescued: true), read, write), Is.True);

            var winch = new M08FirstWinchOrderSession();
            Assert.That(winch.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
            Assert.That(winch.TryAcquirePart(M08WinchPart.DockCable), Is.True);
            Assert.That(winch.TryAssemble(), Is.True);
            Assert.That(winch.TryTightenCable(), Is.True);
            Assert.That(winch.TryTestLift(), Is.True);
            Assert.That(M08WorkshopDepositFacts.TryConfirm(winch, read, write), Is.True);
            Assert.That(M08FirstWorldNoticeFacts.TryRecord(true, read, write), Is.True);

            Assert.That(RC01ChenYanFacts.TryRecord(new RC01ChenYanRecord(
                RC01FerryClues.All, RC01TalkOrder.LinSuFirst,
                RC01ChenYanOutcome.KeepMarkedDead, true), read, write), Is.True);
            Assert.That(ST02RiverLanternsFacts.TryRecord(new ST02RiverLanternsRecord(
                ST02InspectionFlags.All, true, true,
                ST02TrialDisclosure.PublishToBoth,
                ST02BidMode.JointBidWithWrittenTerms,
                ST02LockRescueMethod.BraceHullStressPoint), read, write), Is.True);

            Assert.That(M15FinalRollCallFacts.TryRecord(new M15FinalRollCallRecord(
                M15RollCallReader.Protagonist,
                M15FinalIdentityResponse.PublicTrueName,
                M15PensionClosure.StoppedAndReconciled,
                M15SteleInscription.KeepOriginal,
                consentedVerifiedNamesCount: 0,
                xiaoqiCalledByNumberSeventyTwo: false), read, write), Is.True);
        }

        [Test]
        public void DeskDisplaysEachDeliveredLetterSenderAndContentInChineseAndEnglishWithoutFabricatingUnstartedTales()
        {
            var root = new GameObject("M16EpilogueLettersViewRoot");
            string previousLanguage = SoundwellUiLocalization.Language;
            try
            {
                var values = new Dictionary<string, int>();
                SeedM15WithCompletedRC01AndST02(values);
                Func<string, int> read = Reader(values);
                Action<string, int> write = (k, v) => values[k] = v;

                using (var host = new M16EpilogueHost(
                    root.transform, () => true, () => true,
                    () => M16RespectAgencyTone.RespectedOthersChoices, read, write))
                {
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                    Assert.That(host.TryOpen(), Is.True);
                    var view = host.ActiveView;
                    view.ProceedToDeskButton.onClick.Invoke();

                    var readLabel = view.ReadNextLetterButton.GetComponentInChildren<Text>();
                    Assert.That(view.LastReadLetter, Is.EqualTo(M16DeliveredLettersFlags.None));
                    Assert.That(readLabel.text, Does.Contain("砚叔"));

                    // Read RC-01 Chen Yan's letter and verify ZH/EN content + next button advances to ST-02.
                    view.ReadNextLetterButton.onClick.Invoke();
                    Assert.That(view.LastReadLetter, Is.EqualTo(M16DeliveredLettersFlags.RC01ChenYan));
                    Assert.That(view.Body.text, Does.Contain("RC-01 砚叔来信"));
                    Assert.That(readLabel.text, Does.Contain("两盏河灯"));

                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                    Assert.That(view.Body.text, Does.Contain("[RC-01 Chen Yan]"));
                    Assert.That(readLabel.text, Does.Contain("River Lanterns"));

                    // Read ST-02 River Lanterns letter; RC-02 and RC-05 must remain unfabricated.
                    view.ReadNextLetterButton.onClick.Invoke();
                    Assert.That(view.LastReadLetter, Is.EqualTo(M16DeliveredLettersFlags.ST02RiverLanterns));
                    Assert.That(view.Body.text, Does.Contain("[ST-02 River Lanterns]"));
                    Assert.That(view.ReadNextLetterButton.interactable, Is.False);
                    Assert.That(host.Session.LettersRead,
                        Is.EqualTo(M16DeliveredLettersFlags.RC01ChenYan | M16DeliveredLettersFlags.ST02RiverLanterns));
                }
            }
            finally
            {
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
