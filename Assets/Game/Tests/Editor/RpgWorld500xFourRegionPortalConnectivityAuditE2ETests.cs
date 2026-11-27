using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPortalConnectivityAuditE2ETests
    {
        [Test]
        public void AllFourRegions_DirectedPortalAuditDetectsAnyMissingOutgoingOrIncomingRegionLinks()
        {
            var mapIds = new List<RpgWorldMapId>();
            var allRoutes = new List<RpgWorld500xRegionRouteDef>();

            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                mapIds.Add(spec.MapId);
                allRoutes.AddRange(RpgWorld500xWorldAtlasPortalService.GetConnectedRegions(spec.MapId));
            }

            Assert.AreEqual(4, mapIds.Count);
            Assert.IsTrue(RpgWorld500xMaintenanceReport.AreRegionRoutesStronglyConnected(mapIds, allRoutes));

            // Null, empty, and duplicate map inputs fail closed.
            Assert.IsFalse(RpgWorld500xMaintenanceReport.AreRegionRoutesStronglyConnected(null, allRoutes));
            Assert.IsFalse(RpgWorld500xMaintenanceReport.AreRegionRoutesStronglyConnected(mapIds, null));
            Assert.IsFalse(RpgWorld500xMaintenanceReport.AreRegionRoutesStronglyConnected(
                Array.Empty<RpgWorldMapId>(), allRoutes));
            Assert.IsFalse(RpgWorld500xMaintenanceReport.AreRegionRoutesStronglyConnected(
                new[] { RpgWorldMapId.Soundwell, RpgWorldMapId.Soundwell }, allRoutes));

            // Removing all outgoing or all incoming routes for any of the 4 regions breaks strong connectivity.
            for (int i = 0; i < mapIds.Count; i++)
            {
                RpgWorldMapId isolated = mapIds[i];

                var withoutOutgoing = allRoutes.FindAll(r => r.FromMap != isolated);
                Assert.IsFalse(
                    RpgWorld500xMaintenanceReport.AreRegionRoutesStronglyConnected(mapIds, withoutOutgoing),
                    "Removing outgoing routes from " + isolated + " must fail strong connectivity.");

                var withoutIncoming = allRoutes.FindAll(r => r.ToMap != isolated);
                Assert.IsFalse(
                    RpgWorld500xMaintenanceReport.AreRegionRoutesStronglyConnected(mapIds, withoutIncoming),
                    "Removing incoming routes to " + isolated + " must fail strong connectivity.");
            }
        }
    }
}
