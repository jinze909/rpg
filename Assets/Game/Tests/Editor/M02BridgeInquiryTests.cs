using System;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M02BridgeInquiryTests
    {
        [Test]
        public void WarehouseEvidenceIsOptionalDistinctAndRepeatableWithoutDuplicateFacts()
        {
            var inquiry = new M02BridgeInquiry();
            inquiry.Begin();
            Assert.That(inquiry.Examine(M02BridgeEvidence.CleanCableCut), Is.True);
            Assert.That(inquiry.Examine(M02BridgeEvidence.CleanCableCut), Is.False);
            Assert.That(inquiry.Examine(M02BridgeEvidence.ThirdBellSignal), Is.True);
            Assert.That(inquiry.Has(M02BridgeEvidence.FerrymanLog), Is.False);
            Assert.That(inquiry.Found, Is.EqualTo(
                M02BridgeEvidence.CleanCableCut | M02BridgeEvidence.ThirdBellSignal));
        }

        [Test]
        public void SkippingWarehouseAndArchiveStillEndsInquiryWithoutFabricatingClues()
        {
            var inquiry = new M02BridgeInquiry();
            inquiry.Begin();
            inquiry.LeaveWarehouse();
            inquiry.LeaveArchive();
            Assert.That(inquiry.Location, Is.EqualTo(M02EvidenceLocation.Finished));
            Assert.That(inquiry.Found, Is.EqualTo(M02BridgeEvidence.None));
        }

        [Test]
        public void ArchiveDraftCannotBeFoundAtWarehouseNorReverse()
        {
            var inquiry = new M02BridgeInquiry();
            inquiry.Begin();
            Assert.Throws<InvalidOperationException>(() =>
                inquiry.Examine(M02BridgeEvidence.WarReportDraft));
            inquiry.LeaveWarehouse();
            Assert.Throws<InvalidOperationException>(() =>
                inquiry.Examine(M02BridgeEvidence.FerrymanLog));
            Assert.That(inquiry.Examine(M02BridgeEvidence.WarReportDraft), Is.True);
            Assert.That(inquiry.Has(M02BridgeEvidence.WarReportDraft), Is.True);
        }

        [Test]
        public void CluesDoNotPrematurelyIdentifyWhoOrderedTheBridgeCut()
        {
            Assert.That(M02BridgeInquiryCopy.ClueText(M02BridgeEvidence.CleanCableCut, false),
                Does.Contain("not who ordered it"));
            Assert.That(M02BridgeInquiryCopy.ClueText(M02BridgeEvidence.FerrymanLog, true),
                Does.Contain("三更，工兵队，持总帅令"));
            Assert.That(M02BridgeInquiryCopy.ClueText(M02BridgeEvidence.ThirdBellSignal, true),
                Does.Contain("第三声"));
            Assert.That(M02BridgeInquiryCopy.ClueText(M02BridgeEvidence.WarReportDraft, false),
                Does.Contain("Han Qiao's signature"));
            Assert.That(M02BridgeInquiryCopy.ClueText(M02BridgeEvidence.WarReportDraft, false),
                Does.Contain("not the person who ordered"));
        }

        [Test]
        public void InvalidOrOutOfOrderCluesNeverBecomeFacts()
        {
            var inquiry = new M02BridgeInquiry();
            Assert.Throws<InvalidOperationException>(() => inquiry.LeaveWarehouse());
            Assert.Throws<InvalidOperationException>(() => inquiry.Examine(M02BridgeEvidence.CleanCableCut));
            inquiry.Begin();
            Assert.Throws<InvalidOperationException>(() => inquiry.Begin());
            Assert.Throws<ArgumentOutOfRangeException>(() => inquiry.Examine(M02BridgeEvidence.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => inquiry.Examine(
                M02BridgeEvidence.CleanCableCut | M02BridgeEvidence.FerrymanLog));
            Assert.That(inquiry.Found, Is.EqualTo(M02BridgeEvidence.None));
            inquiry.LeaveWarehouse();
            inquiry.LeaveArchive();
            Assert.Throws<InvalidOperationException>(() => inquiry.Examine(M02BridgeEvidence.WarReportDraft));
        }
    }
}
