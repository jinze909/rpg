using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Lets the player record what is actually visible at Red Salt after the
    /// fog crossing. Departure limits are ceilings, never pre-filled arrival
    /// facts; the Makinom landing record is written only by the host callback.
    /// </summary>
    public sealed class M05LandingObservationView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform authored;
        private readonly Font latinFont;
        private readonly M04DepartureRecord departure;
        private readonly Func<string, int> getInt;
        private Action<M05LandingReport> onConfirmed;
        private readonly Text heading;
        private readonly Text instructions;
        private readonly Text crewLabel;
        private readonly Text crewValue;
        private readonly Text tenderLabel;
        private readonly Text receiverLabel;
        private readonly Text berthLabel;
        private readonly Text berthValue;
        private readonly Text status;
        private readonly Text crewMinusLabel;
        private readonly Text crewPlusLabel;
        private readonly Text tenderButtonLabel;
        private readonly Text receiverButtonLabel;
        private readonly Text berthMinusLabel;
        private readonly Text berthPlusLabel;
        private readonly Text confirmLabel;
        private readonly Button crewMinus;
        private readonly Button crewPlus;
        private readonly Button tenderButton;
        private readonly Button receiverButton;
        private readonly Button berthMinus;
        private readonly Button berthPlus;
        private readonly Button confirmButton;
        private int crewArrived;
        private int readyRescueBerths;
        private bool tenderArrived;
        private bool receiverMet;
        private bool delivered;
        private bool disposed;

        public int CrewArrived => crewArrived;
        public int ReadyRescueBerths => readyRescueBerths;
        public bool TenderArrived => tenderArrived;
        public bool ReceiverMet => receiverMet;
        public M05LandingReport Report => new M05LandingReport(
            crewArrived, readyRescueBerths, tenderArrived, receiverMet);
        public bool CanConfirm => CanConfirmCurrentReport();
        public Text Instructions => instructions;
        public Text Status => status;
        public Text CrewValue => crewValue;
        public Text BerthValue => berthValue;
        public Button CrewMinusButton => crewMinus;
        public Button CrewPlusButton => crewPlus;
        public Button TenderButton => tenderButton;
        public Button ReceiverButton => receiverButton;
        public Button BerthMinusButton => berthMinus;
        public Button BerthPlusButton => berthPlus;
        public Button ConfirmButton => confirmButton;
        // Never recommend a disabled control as the fallback navigation target.
        // This matters when the observed crew reaches the manifest maximum.
        public Button PreferredFocusButton =>
            confirmButton.IsActive() && confirmButton.IsInteractable() ? confirmButton :
            crewPlus.IsActive() && crewPlus.IsInteractable() ? crewPlus :
            tenderButton.IsActive() && tenderButton.IsInteractable() ? tenderButton :
            receiverButton.IsActive() && receiverButton.IsInteractable() ? receiverButton :
            berthPlus.IsActive() && berthPlus.IsInteractable() ? berthPlus :
            crewMinus.IsActive() && crewMinus.IsInteractable() ? crewMinus :
            berthMinus.IsActive() && berthMinus.IsInteractable() ? berthMinus : null;
        public RectTransform AuthoredPanel => authored;
        public bool IsDisposed => disposed || IsCanvasDestroyed();

        public M05LandingObservationView(
            Transform parent,
            M04DepartureRecord departure,
            Func<string, int> getInt,
            Action<M05LandingReport> onConfirmed)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (getInt == null) throw new ArgumentNullException(nameof(getInt));
            if (onConfirmed == null) throw new ArgumentNullException(nameof(onConfirmed));
            if (!Enum.IsDefined(typeof(M03VoyageChoice), departure.Route) ||
                departure.AssignedCrew <= 0 || departure.AvailableBerths <= 0)
                throw new ArgumentOutOfRangeException(nameof(departure));

            this.departure = departure;
            this.getInt = getInt;
            this.onConfirmed = onConfirmed;
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = SoundwellUiTheme.CreateOverlayCanvas(
                parent, "M05 actual arrival observation canvas", 131,
                out authored);

            heading = SoundwellUiTheme.CreateLabel(authored, 24,
                new Vector2(0f, 214f), new Vector2(840f, 42f));
            instructions = SoundwellUiTheme.CreateLabel(authored, 16,
                new Vector2(0f, 163f), new Vector2(840f, 52f));
            instructions.alignment = TextAnchor.MiddleCenter;

            crewLabel = SoundwellUiTheme.CreateLabel(authored, 18,
                new Vector2(-285f, 91f), new Vector2(310f, 38f));
            crewLabel.alignment = TextAnchor.MiddleLeft;
            crewValue = SoundwellUiTheme.CreateLabel(authored, 20,
                new Vector2(-20f, 91f), new Vector2(110f, 38f));
            crewValue.alignment = TextAnchor.MiddleCenter;
            crewMinus = SoundwellUiTheme.CreateButton(authored,
                "M05 observed crew decrease", new Vector2(100f, 91f),
                new Vector2(52f, 40f), 20, out crewMinusLabel);
            crewPlus = SoundwellUiTheme.CreateButton(authored,
                "M05 observed crew increase", new Vector2(160f, 91f),
                new Vector2(52f, 40f), 20, out crewPlusLabel);

            tenderLabel = SoundwellUiTheme.CreateLabel(authored, 18,
                new Vector2(-285f, 31f), new Vector2(310f, 38f));
            tenderLabel.alignment = TextAnchor.MiddleLeft;
            tenderButton = SoundwellUiTheme.CreateButton(authored,
                "M05 observed tender toggle", new Vector2(145f, 31f),
                new Vector2(220f, 40f), 16, out tenderButtonLabel);

            receiverLabel = SoundwellUiTheme.CreateLabel(authored, 18,
                new Vector2(-285f, -29f), new Vector2(310f, 38f));
            receiverLabel.alignment = TextAnchor.MiddleLeft;
            receiverButton = SoundwellUiTheme.CreateButton(authored,
                "M05 observed receiver toggle", new Vector2(145f, -29f),
                new Vector2(220f, 40f), 16, out receiverButtonLabel);

            berthLabel = SoundwellUiTheme.CreateLabel(authored, 18,
                new Vector2(-285f, -89f), new Vector2(310f, 38f));
            berthLabel.alignment = TextAnchor.MiddleLeft;
            berthValue = SoundwellUiTheme.CreateLabel(authored, 20,
                new Vector2(-20f, -89f), new Vector2(110f, 38f));
            berthValue.alignment = TextAnchor.MiddleCenter;
            berthMinus = SoundwellUiTheme.CreateButton(authored,
                "M05 observed berths decrease", new Vector2(100f, -89f),
                new Vector2(52f, 40f), 20, out berthMinusLabel);
            berthPlus = SoundwellUiTheme.CreateButton(authored,
                "M05 observed berths increase", new Vector2(160f, -89f),
                new Vector2(52f, 40f), 20, out berthPlusLabel);

            status = SoundwellUiTheme.CreateLabel(authored, 15,
                new Vector2(0f, -145f), new Vector2(840f, 34f));
            status.alignment = TextAnchor.MiddleCenter;
            confirmButton = SoundwellUiTheme.CreateButton(authored,
                "M05 confirm actual arrival", new Vector2(0f, -207f),
                new Vector2(300f, 44f), 18, out confirmLabel);

            crewMinus.onClick.AddListener(DecreaseCrew);
            crewPlus.onClick.AddListener(IncreaseCrew);
            tenderButton.onClick.AddListener(ToggleTender);
            receiverButton.onClick.AddListener(ToggleReceiver);
            berthMinus.onClick.AddListener(DecreaseBerths);
            berthPlus.onClick.AddListener(IncreaseBerths);
            confirmButton.onClick.AddListener(Confirm);

            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            SoundwellUiLocalization.Changed += Refresh;
            Refresh();
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (!HasLiveUi()) return;
            SoundwellUiTheme.ApplyViewportAspectScale(authored, aspect);
        }

        private bool CanConfirmCurrentReport()
        {
            if (disposed || delivered || crewArrived <= 0 ||
                !M05LandingFacts.TryRead(getInt, out M05LandingReport? saved))
                return false;
            if (saved.HasValue)
                return SameLanding(saved.Value, Report);
            return M05LandingFacts.CanRecord(Report, getInt);
        }

        private static bool SameLanding(M05LandingReport a, M05LandingReport b)
        {
            return a.CrewArrived == b.CrewArrived &&
                a.ReadyRescueBerths == b.ReadyRescueBerths &&
                a.TenderArrived == b.TenderArrived &&
                a.ReceiverMet == b.ReceiverMet;
        }

        public void Refresh()
        {
            if (!HasLiveUi()) return;
            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = instructions.font = crewLabel.font = crewValue.font =
                tenderLabel.font = receiverLabel.font = berthLabel.font =
                berthValue.font = status.font = crewMinusLabel.font =
                crewPlusLabel.font = tenderButtonLabel.font =
                receiverButtonLabel.font = berthMinusLabel.font =
                berthPlusLabel.font = confirmLabel.font = font;

            heading.text = M05LandingObservationCopy.Title(departure.Route, zh);
            instructions.text = M05LandingObservationCopy.Instructions(zh);
            crewLabel.text = M05LandingObservationCopy.CrewLabel(
                departure.AssignedCrew, zh);
            crewValue.text = crewArrived + " / " + departure.AssignedCrew;
            crewMinusLabel.text = "−";
            crewPlusLabel.text = "+";
            tenderLabel.text = zh ? "浅水驳船实际到港" : "Shallow tender actually arrived";
            tenderButtonLabel.text = tenderArrived
                ? (zh ? "已到港" : "Arrived")
                : (zh ? "尚未到港" : "Not yet arrived");
            receiverLabel.text = zh ? "外港接应人实际接头" : "Harbor receiver actually met";
            receiverButtonLabel.text = receiverMet
                ? (zh ? "已接头" : "Met")
                : (zh ? "尚未接上" : "Not yet met");
            berthLabel.text = M05LandingObservationCopy.BerthLabel(
                departure.AvailableBerths, zh);
            berthValue.text = readyRescueBerths + " / " + departure.AvailableBerths;
            berthMinusLabel.text = "−";
            berthPlusLabel.text = "+";
            confirmLabel.text = zh ? "确认本次实到记录" : "Confirm observed arrival";

            bool active = !delivered;
            crewMinus.interactable = active && crewArrived > 0;
            crewPlus.interactable = active && crewArrived < departure.AssignedCrew;
            tenderButton.interactable = active;
            receiverButton.interactable = active;
            bool capacityCanBeObserved = tenderArrived && receiverMet;
            berthMinus.interactable = active && capacityCanBeObserved &&
                readyRescueBerths > 0;
            berthPlus.interactable = active && capacityCanBeObserved &&
                readyRescueBerths < departure.AvailableBerths;
            confirmButton.interactable = active && CanConfirmCurrentReport();
            status.text = M05LandingObservationCopy.Status(
                crewArrived, readyRescueBerths, tenderArrived, receiverMet, zh);

            if (active)
                KeepArrivalFocus();
        }

        private void KeepArrivalFocus()
        {
            EventSystem events = EventSystem.current;
            if (Application.isPlaying && events != null && !events.alreadySelecting)
            {
                GameObject selected = events.currentSelectedGameObject;
                if (selected != null && selected.transform.IsChildOf(canvasObject.transform))
                {
                    Selectable control = selected.GetComponent<Selectable>();
                    if (control == null || !control.IsActive() || !control.IsInteractable())
                        events.SetSelectedGameObject(null);
                }
            }
            SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, PreferredFocusButton);
        }

        private void DecreaseCrew()
        {
            if (!HasLiveUi() || delivered || crewArrived <= 0) return;
            crewArrived--;
            Refresh();
        }

        private void IncreaseCrew()
        {
            if (!HasLiveUi() || delivered || crewArrived >= departure.AssignedCrew) return;
            crewArrived++;
            Refresh();
        }

        private void ToggleTender()
        {
            if (!HasLiveUi() || delivered) return;
            tenderArrived = !tenderArrived;
            if (!tenderArrived) readyRescueBerths = 0;
            Refresh();
        }

        private void ToggleReceiver()
        {
            if (!HasLiveUi() || delivered) return;
            receiverMet = !receiverMet;
            if (!receiverMet) readyRescueBerths = 0;
            Refresh();
        }

        private void DecreaseBerths()
        {
            if (!HasLiveUi() || delivered || !tenderArrived || !receiverMet ||
                readyRescueBerths <= 0) return;
            readyRescueBerths--;
            Refresh();
        }

        private void IncreaseBerths()
        {
            if (!HasLiveUi() || delivered || !tenderArrived || !receiverMet ||
                readyRescueBerths >= departure.AvailableBerths) return;
            readyRescueBerths++;
            Refresh();
        }

        private void Confirm()
        {
            if (!HasLiveUi() || delivered || !CanConfirmCurrentReport())
                return;
            delivered = true;
            Refresh();
            try
            {
                onConfirmed?.Invoke(Report);
                onConfirmed = null;
            }
            catch
            {
                delivered = false;
                if (!disposed) Refresh();
                throw;
            }
        }

        private bool IsCanvasDestroyed()
        {
            return canvasObject == null || authored == null || heading == null ||
                instructions == null || crewLabel == null || crewValue == null ||
                tenderLabel == null || receiverLabel == null || berthLabel == null ||
                berthValue == null || status == null || crewMinus == null ||
                crewPlus == null || tenderButton == null || receiverButton == null ||
                berthMinus == null || berthPlus == null || confirmButton == null ||
                crewMinusLabel == null || crewPlusLabel == null ||
                tenderButtonLabel == null || receiverButtonLabel == null ||
                berthMinusLabel == null || berthPlusLabel == null || confirmLabel == null;
        }

        private bool HasLiveUi()
        {
            if (disposed) return false;
            if (!IsCanvasDestroyed()) return true;
            Dispose();
            return false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onConfirmed = null;
            SoundwellUiLocalization.Changed -= Refresh;
            if (crewMinus != null) crewMinus.onClick.RemoveAllListeners();
            if (crewPlus != null) crewPlus.onClick.RemoveAllListeners();
            if (tenderButton != null) tenderButton.onClick.RemoveAllListeners();
            if (receiverButton != null) receiverButton.onClick.RemoveAllListeners();
            if (berthMinus != null) berthMinus.onClick.RemoveAllListeners();
            if (berthPlus != null) berthPlus.onClick.RemoveAllListeners();
            if (confirmButton != null) confirmButton.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }

    public static class M05LandingObservationCopy
    {
        public static string Title(M03VoyageChoice route, bool zh)
        {
            string routeName = RouteName(route, zh);
            return zh
                ? "赤盐外港 · " + routeName + "实到核对"
                : "RED SALT HARBOR · " + routeName + " ARRIVAL CHECK";
        }

        public static string Instructions(bool zh)
        {
            return zh
                ? "启程时的计划不是抵港事实。请按眼前码头情况核对；驳船或接应人未到位时，可用救援舱位必须为 0。"
                : "The departure plan is not an arrival record. Check the dock as it stands; usable rescue berths stay at 0 until both tender and receiver are here.";
        }

        public static string CrewLabel(int maximum, bool zh)
        {
            return zh
                ? "实际抵达船员（最多 " + maximum + "）"
                : "Crew actually arrived (max " + maximum + ")";
        }

        public static string BerthLabel(int maximum, bool zh)
        {
            return zh
                ? "当前可用救援舱位（最多 " + maximum + "）"
                : "Rescue berths usable now (max " + maximum + ")";
        }

        public static string Status(int crew, int berths, bool tender,
            bool receiver, bool zh)
        {
            if (crew <= 0)
                return zh ? "先核对实际到港船员；不会自动沿用出航人数。" :
                    "Record the crew you actually see; departure numbers are not copied.";
            if (!tender || !receiver)
                return zh ? "驳船与接应尚未同时到位：当前可用救援舱位锁定为 0。" :
                    "Tender and receiver are not both present: usable rescue berths are locked at 0.";
            return zh
                ? "已核对实到 " + crew + " 人；当前可用救援舱位 " + berths + "。"
                : crew + " crew observed; " + berths + " rescue berths currently usable.";
        }

        private static string RouteName(M03VoyageChoice route, bool zh)
        {
            switch (route)
            {
                case M03VoyageChoice.LuJunEscort:
                    return zh ? "陆钧护航" : "LU JUN ESCORT";
                case M03VoyageChoice.QiQueMerchant:
                    return zh ? "祁雀商路" : "QI QUE MERCHANT";
                case M03VoyageChoice.HanQiaoOfficial:
                    return zh ? "韩峤官船" : "HAN QIAO OFFICIAL";
                case M03VoyageChoice.MixedWithOfficial:
                    return zh ? "官船混合航路" : "MIXED OFFICIAL ROUTE";
                case M03VoyageChoice.MixedWithCivilian:
                    return zh ? "民船混合航路" : "MIXED CIVILIAN ROUTE";
                default:
                    throw new ArgumentOutOfRangeException(nameof(route));
            }
        }
    }
}
