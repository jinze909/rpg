using System;
using System.Collections.Generic;

namespace Rpg.Soundwell
{
    // These are actionable problems with this voyage, not story choices.
    // Display code can localize each item without guessing a different ship.
    public enum M04DepartureBlocker
    {
        NoConfirmedHull,
        NoConfirmedPilot,
        NoCrewAssigned,
        UncheckedDraft,
        NoShallowTender,
        NoLandingReceiver,
        NoReturneeDemand,
        InsufficientBerths,
        MissingOfficialStamp,
        UnacceptedEscortFlag
    }

    /// <summary>
    /// M04's factual departure check, separate from the M03 decision about
    /// where to seek a ship. A route choice alone never creates a hull, crew,
    /// landing boat, receiver, or enough bunks for the named returnees.
    /// The host supplies only actual, confirmed resources from this voyage.
    /// This object does not create an independent save or voyage authority.
    /// </summary>
    public sealed class M04DepartureManifest
    {
        public M04DepartureManifest(M03VoyageChoice chosenRoute,
            string actualHull, string confirmedPilot, int assignedCrew,
            string shallowTender, bool draftChecked, int availableBerths,
            int returneesToBook, string landingReceiver,
            bool officialRegistrationEffective, bool escortFlagAccepted)
        {
            if (!Enum.IsDefined(typeof(M03VoyageChoice), chosenRoute))
                throw new ArgumentOutOfRangeException(nameof(chosenRoute));
            if (assignedCrew < 0) throw new ArgumentOutOfRangeException(nameof(assignedCrew));
            if (availableBerths < 0) throw new ArgumentOutOfRangeException(nameof(availableBerths));
            if (returneesToBook < 0) throw new ArgumentOutOfRangeException(nameof(returneesToBook));

            Route = chosenRoute;
            ActualHull = actualHull;
            ConfirmedPilot = confirmedPilot;
            AssignedCrew = assignedCrew;
            ShallowTender = shallowTender;
            DraftChecked = draftChecked;
            AvailableBerths = availableBerths;
            ReturneesToBook = returneesToBook;
            LandingReceiver = landingReceiver;
            OfficialRegistrationEffective = officialRegistrationEffective;
            EscortFlagAccepted = escortFlagAccepted;
        }

        public M03VoyageChoice Route { get; }
        public string ActualHull { get; }
        public string ConfirmedPilot { get; }
        public int AssignedCrew { get; }
        public string ShallowTender { get; }
        public bool DraftChecked { get; }
        public int AvailableBerths { get; }
        public int ReturneesToBook { get; }
        public string LandingReceiver { get; }
        public bool OfficialRegistrationEffective { get; }
        public bool EscortFlagAccepted { get; }
        public bool DepartureConfirmed { get; private set; }

        public M04DepartureBlocker[] Check()
        {
            var blockers = new List<M04DepartureBlocker>();
            if (string.IsNullOrWhiteSpace(ActualHull))
                blockers.Add(M04DepartureBlocker.NoConfirmedHull);
            if (string.IsNullOrWhiteSpace(ConfirmedPilot))
                blockers.Add(M04DepartureBlocker.NoConfirmedPilot);
            if (AssignedCrew == 0)
                blockers.Add(M04DepartureBlocker.NoCrewAssigned);
            if (!DraftChecked)
                blockers.Add(M04DepartureBlocker.UncheckedDraft);
            if (string.IsNullOrWhiteSpace(ShallowTender))
                blockers.Add(M04DepartureBlocker.NoShallowTender);
            if (string.IsNullOrWhiteSpace(LandingReceiver))
                blockers.Add(M04DepartureBlocker.NoLandingReceiver);
            if (ReturneesToBook == 0)
                blockers.Add(M04DepartureBlocker.NoReturneeDemand);
            else if (ReturneesToBook > AvailableBerths)
                blockers.Add(M04DepartureBlocker.InsufficientBerths);

            if ((Route == M03VoyageChoice.HanQiaoOfficial ||
                 Route == M03VoyageChoice.MixedWithOfficial) &&
                !OfficialRegistrationEffective)
                blockers.Add(M04DepartureBlocker.MissingOfficialStamp);
            if (Route == M03VoyageChoice.LuJunEscort && !EscortFlagAccepted)
                blockers.Add(M04DepartureBlocker.UnacceptedEscortFlag);
            return blockers.ToArray();
        }

        // The manifest cannot depart on a guessed route, or on a previously
        // rejected choice. This is an in-memory action until an actual M04
        // host records it through the single existing RPG save path.
        public bool ConfirmDeparture()
        {
            if (DepartureConfirmed || Check().Length != 0)
                return false;
            DepartureConfirmed = true;
            return true;
        }
    }
}
