using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M15FinalRollCallZhonglingStoneTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void MonumentStoneStaysHiddenWithoutVerifiedOrderAndDeliveryOrOnSpoofedStatus()
        {
            var root = new GameObject("M15ZhonglingStoneHiddenRoot");
            string previousLanguage = SoundwellUiLocalization.Language;
            try
            {
                var values = new Dictionary<string, int>();
                Func<string, int> read = Reader(values);
                Action<string, int> write = (k, v) => values[k] = v;

                using (var host = new M15FinalRollCallHost(
                    root.transform, () => true, () => false, () => 2, null, read, write))
                {
                    Assert.That(host.ShowZhonglingStoneOnMonument, Is.False);

                    // Record ST-04 with an open route, but no quarry order yet.
                    var record = new ST04EchoValleyRecord(
                        ST04ValleyEntry.EnterQuietly,
                        ST04ChamberEntry.ClearGuardBeast,
                        ST04BeastHandling.Slain,
                        ST04RoadResolution.KillAdultsAndCloseWell,
                        rescuedSurveyors: 2,
                        echoRulesVerified: true,
                        companionsMarked: true,
                        drainOpened: true,
                        longRoadFunded: false,
                        alternativeWorksiteReady: false);
                    Assert.That(ST04EchoValleyFacts.TryRecord(record, read, write), Is.True);
                    Assert.That(host.ShowZhonglingStoneOnMonument, Is.False);

                    // Spoofed delivery enum without independent order/delivery proof fails closed.
                    values[ST04EchoValleyFacts.StoneShipmentKey] =
                        (int)ST04StoneShipment.DeliveredToLinchao;
                    Assert.That(host.ShowZhonglingStoneOnMonument, Is.False);
                    values[ST04EchoValleyFacts.StoneShipmentKey] = 0;

                    // Accepted order without delivery is still not enough for the monument.
                    Assert.That(ST04EchoValleyFacts.TryMarkStoneOrderAccepted(read, write), Is.True);
                    Assert.That(host.ShowZhonglingStoneOnMonument, Is.False);

                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                    Assert.That(host.TryOpen(), Is.True);
                    var view = host.ActiveView;
                    Assert.That(view.ShowZhonglingStone, Is.False);
                    view.ProtagonistReaderButton.onClick.Invoke();
                    view.PublicTrueNameButton.onClick.Invoke();
                    Assert.That(view.Body.text, Does.Not.Contain("钟岭"));

                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                    Assert.That(view.Body.text, Does.Not.Contain("Zhongling"));
                }
            }
            finally
            {
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VerifiedZhonglingStoneDeliveryDisplaysOnM15SteleInChineseAndEnglish()
        {
            var root = new GameObject("M15ZhonglingStoneShownRoot");
            string previousLanguage = SoundwellUiLocalization.Language;
            try
            {
                var values = new Dictionary<string, int>();
                Func<string, int> read = Reader(values);
                Action<string, int> write = (k, v) => values[k] = v;

                var preserved = new ST04EchoValleyRecord(
                    ST04ValleyEntry.FollowSangJingAndBell,
                    ST04ChamberEntry.OpenVentGrate,
                    ST04BeastHandling.ContainedAwayFromClosedRoad,
                    ST04RoadResolution.PreserveRainWallAndCloseRoad,
                    rescuedSurveyors: 2,
                    echoRulesVerified: true,
                    companionsMarked: true,
                    drainOpened: true,
                    longRoadFunded: false,
                    alternativeWorksiteReady: true);
                Assert.That(ST04EchoValleyFacts.TryRecord(preserved, read, write), Is.True);
                Assert.That(ST04EchoValleyFacts.TryVerifyAdditionalStoneTransport(read, write), Is.True);
                Assert.That(ST04EchoValleyFacts.TryMarkStoneOrderAccepted(read, write), Is.True);
                Assert.That(ST04EchoValleyFacts.TryMarkStoneDelivered(read, write), Is.True);

                using (var host = new M15FinalRollCallHost(
                    root.transform, () => true, () => false, () => 3, null, read, write))
                {
                    Assert.That(host.ShowZhonglingStoneOnMonument, Is.True);

                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                    Assert.That(host.TryOpen(), Is.True);
                    var view = host.ActiveView;
                    Assert.That(view.ShowZhonglingStone, Is.True);

                    view.HanQiaoReaderButton.onClick.Invoke();
                    view.PublicTrueNameButton.onClick.Invoke();
                    Assert.That(view.Step, Is.EqualTo(M15RollCallViewStep.SelectSteleInscription));
                    Assert.That(view.Body.text, Does.Contain("钟岭采石场运抵临潮的新碑石已立妥"));

                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                    Assert.That(view.Body.text, Does.Contain("delivered Zhongling quarry stone"));

                    view.AddReturnedAliveSteleButton.onClick.Invoke();
                    Assert.That(host.IsCompleted, Is.True);
                    Assert.That(host.ActiveView, Is.Null);
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
