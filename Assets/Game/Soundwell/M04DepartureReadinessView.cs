using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Shows only resources actually verified by the voyage host. The screen
    /// cannot add a ship, a pilot, a tender, a landing contact or new berths.
    /// The host remains responsible for saving any successfully accepted plan.
    /// </summary>
    public sealed class M04DepartureReadinessView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform authored;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text details;
        private readonly Text confirmLabel;
        private readonly Button confirm;
        private readonly Button back;
        private readonly Text backLabel;
        private readonly Action<M04DepartureManifest> onConfirmed;
        private readonly Action onBack;
        private M04DepartureManifest manifest;
        private bool disposed;
        private bool delivered;

        public M04DepartureManifest Manifest => manifest;
        public Text Details => details;
        public Button ConfirmButton => confirm;
        public Button BackButton => back;

        public M04DepartureReadinessView(Transform parent, M04DepartureManifest initial,
            Action<M04DepartureManifest> onConfirmed, Action onBack)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            this.onConfirmed = onConfirmed ?? throw new ArgumentNullException(nameof(onConfirmed));
            this.onBack = onBack ?? throw new ArgumentNullException(nameof(onBack));
            manifest = initial;
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = SoundwellUiTheme.CreateOverlayCanvas(
                parent, "M04 departure readiness canvas", 129, out authored);
            heading = SoundwellUiTheme.CreateLabel(authored, 24, new Vector2(0f, 209f), new Vector2(820f, 46f));
            details = SoundwellUiTheme.CreateLabel(authored, 18, new Vector2(0f, 40f), new Vector2(830f, 277f));
            details.alignment = TextAnchor.UpperLeft;
            confirm = SoundwellUiTheme.CreateButton(authored, "M04 confirm departure",
                new Vector2(-210f, -207f), new Vector2(340f, 46f), 18, out confirmLabel);
            back = SoundwellUiTheme.CreateButton(authored, "M04 return to preparations",
                new Vector2(210f, -207f), new Vector2(340f, 46f), 18, out backLabel);
            confirm.onClick.AddListener(Confirm);
            back.onClick.AddListener(GoBack);
            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            SoundwellUiLocalization.Changed += Refresh;
            Refresh();
        }

        public void SetConfirmedManifest(M04DepartureManifest updated)
        {
            if (disposed) throw new ObjectDisposedException(nameof(M04DepartureReadinessView));
            if (delivered) throw new InvalidOperationException("The departure has been confirmed.");
            manifest = updated ?? throw new ArgumentNullException(nameof(updated));
            Refresh();
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed) return;
            SoundwellUiTheme.ApplyViewportAspectScale(authored, aspect);
        }

        public void Refresh()
        {
            if (disposed) return;
            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = details.font = confirmLabel.font = backLabel.font = font;
            heading.text = zh ? "启航前 · 船位与接应核验" : "BEFORE DEPARTURE · VOYAGE CHECK";
            details.text = M04DepartureReadinessCopy.Describe(manifest, zh);
            confirmLabel.text = zh ? "确认准备齐全" : "Confirm readiness";
            backLabel.text = zh ? "返回准备" : "Return to preparations";
            confirm.interactable = !delivered && !manifest.DepartureConfirmed &&
                manifest.Check().Length == 0;
            back.interactable = !delivered;
            if (!delivered)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform,
                    confirm.interactable ? confirm : back);
        }

        private void Confirm()
        {
            // Direct Button.onClick invocation bypasses interactable; lock the
            // whole panel before calling the host to reject reentrant submits.
            if (disposed || delivered || manifest.DepartureConfirmed ||
                manifest.Check().Length != 0)
                return;
            delivered = true;
            Refresh();
            try
            {
                if (!manifest.ConfirmDeparture(onConfirmed))
                {
                    delivered = false;
                    Refresh();
                }
            }
            catch
            {
                // Interrupted Makinom writes are not a successful departure.
                // The manifest transaction has rolled its in-memory flag back.
                delivered = false;
                Refresh();
                throw;
            }
        }

        private void GoBack()
        {
            if (disposed || delivered) return;
            onBack();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            SoundwellUiLocalization.Changed -= Refresh;
            confirm.onClick.RemoveAllListeners();
            back.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }

    public static class M04DepartureReadinessCopy
    {
        public static string Describe(M04DepartureManifest manifest, bool chinese)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            var blockers = manifest.Check();
            var copy = new StringBuilder();
            copy.Append(chinese ? "实际核对的铺位：" : "Verified berths: ");
            copy.Append(manifest.AvailableBerths);
            copy.Append(chinese ? "；计划预留的救援舱位：" : "; rescue berths to reserve: ");
            copy.Append(manifest.ReturneesToBook);
            copy.AppendLine();
            if (blockers.Length == 0)
            {
                copy.AppendLine(chinese ? "船只与登岸接应均已核实，可以申请正式确认。" :
                    "The ship and landing arrangements are verified. You may confirm this plan.");
                // Report only verified supplies from the manifest; never
                // invent a named ship, pilot or landing contact from a route.
                copy.Append(chinese ? "船只：" : "Vessel: ").Append(manifest.ActualHull);
                copy.Append(chinese ? "；领航：" : "; pilot: ").AppendLine(manifest.ConfirmedPilot);
                copy.Append(chinese ? "船员：" : "Crew: ").Append(manifest.AssignedCrew);
                copy.Append(chinese ? "；接驳：" : "; tender: ").AppendLine(manifest.ShallowTender);
                copy.Append(chinese ? "登岸接应：" : "Landing receiver: ").Append(manifest.LandingReceiver);
                return copy.ToString();
            }
            copy.AppendLine(chinese ? "尚不能启航；必须先解决：" :
                "Departure is blocked. Resolve these items first:");
            foreach (M04DepartureBlocker issue in blockers)
            {
                copy.Append("• ");
                copy.AppendLine(BlockerText(issue, chinese));
            }
            return copy.ToString();
        }

        public static string BlockerText(M04DepartureBlocker issue, bool chinese)
        {
            switch (issue)
            {
                case M04DepartureBlocker.NoConfirmedHull:
                    return chinese ? "尚无核实的船只" : "No confirmed vessel";
                case M04DepartureBlocker.NoConfirmedPilot:
                    return chinese ? "缺少确认的领航人" : "No confirmed pilot";
                case M04DepartureBlocker.NoCrewAssigned:
                    return chinese ? "尚未安排船员" : "No assigned crew";
                case M04DepartureBlocker.UncheckedDraft:
                    return chinese ? "尚未检查船只吃水" : "Draft has not been checked";
                case M04DepartureBlocker.NoShallowTender:
                    return chinese ? "缺少浅滩接驳艇" : "No shallow-water tender";
                case M04DepartureBlocker.NoLandingReceiver:
                    return chinese ? "尚无指定登岸接应" : "No named landing receiver";
                case M04DepartureBlocker.NoRescueBerthPlan:
                    return chinese ? "先确定本航次要预留的救援舱位" : "Plan how many berths to reserve for the rescue";
                case M04DepartureBlocker.InsufficientBerths:
                    return chinese ? "已核实铺位不足" : "Not enough verified berths";
                case M04DepartureBlocker.MissingOfficialStamp:
                    return chinese ? "官方航路缺少有效的登记批准" : "Official passage lacks a valid stamp";
                case M04DepartureBlocker.UnacceptedEscortFlag:
                    return chinese ? "陆钧的公开护航旗尚未确认" : "Lu Jun's public escort flag is not accepted";
                default:
                    throw new ArgumentOutOfRangeException(nameof(issue));
            }
        }
    }
}
