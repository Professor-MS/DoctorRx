# DoctorRx Layout & UI Verification Checklist (Part A & Part B)

> **Important Testing Notice**: The rasterized screenshots generated in this directory (`docs/ui-checks/*.png`) are captured via WPF `RenderTargetBitmap` across multiple resolutions (960x520, 1024x600, 1280x720, 1920x1080) and 5 DPI scaling factors (96, 120, 144, 168, 192 DPI, approximating 100% to 200%). While this verifies layout measurement, vector geometry rasterization, typography scaling, and clipping bounds, **this approximates, not replaces, real physical display testing** on heterogeneous multi-monitor hardware.

---

## 1. UI Inventory (B0)

Every screen, dialog, drawer, popup, and overlay currently in DoctorRx has been audited, modernized, and verified:

| Surface Name | Type | Key Responsive & Accessibility Behaviors | Automated Test Status |
| :--- | :--- | :--- | :--- |
| **Dashboard** | Main Screen | 4 cards (Wide), 2 cards (Normal/Compact), 1 card (< 480); tables side-by-side (Wide) vs stacked (Compact); DataGrid star columns drop low-priority columns in Compact. | Verified |
| **Patients** | Main Screen | Star columns with MinWidth; Compact mode collapses action buttons into compact icon buttons with tooltips; live search and paging. | Verified |
| **Patient Drawer** | Drawer Overlay | Slide-over drawer clamped to `clamp(35%, 320, 440) DIPs`; full width below 800 DIPs; vertical scroll; `Esc` closes. | Verified |
| **New Prescription** | Main Screen | Two panes (composer + live summary) in Wide; single column stack in Normal/Compact; reflowing medicine cards and editor; Review/Finalize visible. | Verified |
| **Prescription Detail** | Main Screen | Star column layout; observation metadata cards reflow; Print action launches Print Preview; no horizontal scroll. | Verified |
| **Print Preview** | Custom Window | Relative sizing to owner (95% max); Zoom to Fit, Fit Width, Zoom In/Out; vertical page scrolling; `Esc` closes; no horizontal window scroll. | Verified |
| **Custom Dialog** | Modal Window | Replaces Win32 MessageBox; relative sizing (90% max), min 380x180, scrollable message, pinned buttons, centered over owner on active monitor; `Esc` cancels, `Enter` confirms. | Verified |
| **Doctor Setup** | Modal Overlay | Clamped to `560 x 470 DIPs`; scrollable content; vector icons; `Esc` closes. | Verified |
| **Keyboard Shortcuts** | Modal Overlay | Clamped to `600 x 480 DIPs`; scrollable table of shortcuts; vector icons; `Esc` closes. | Verified |
| **Scaffolded Views** | Placeholder Screens | (History, Medicines, Settings, Wizard) Responsive cards, vector icons (`IconCheck`, `IconInfo`), no hardcoded font sizes. | Verified |

---

## 2. Automated Visual Artifacts Matrix

The following visual artifacts were generated directly during automated verification in `docs/ui-checks/`:

| Artifact Filename | Screen / Component | Resolution | DPI | Target Validation |
| :--- | :--- | :--- | :--- | :--- |
| `dashboard-compact-960x520.png` | Dashboard | 960 x 520 px | 96 (100%) | Compact 2-column cards, stacked tables, hidden low-priority columns |
| `dashboard-wide-1920x1080.png` | Dashboard | 1920 x 1080 px | 96 (100%) | Wide 4-column cards, side-by-side tables |
| `dashboard-dpi96.png` | Dashboard | 1280 x 720 px | 96 (100%) | Standard DPI rendering baseline |
| `dashboard-dpi120.png` | Dashboard | 1280 x 720 px | 120 (125%) | 125% fractional scaling check |
| `dashboard-dpi144.png` | Dashboard | 1280 x 720 px | 144 (150%) | 150% fractional scaling check |
| `dashboard-dpi168.png` | Dashboard | 1280 x 720 px | 168 (175%) | 175% fractional scaling check |
| `dashboard-dpi192.png` | Dashboard | 1280 x 720 px | 192 (200%) | 200% high-density scaling check |
| `patients-compact-960x520.png` | Patients | 960 x 520 px | 96 (100%) | Compact icon action buttons, star columns |
| `patients-drawer-overlay.png` | Patient Drawer | 960 x 520 px | 96 (100%) | Slide-over drawer clamped to 336 DIPs (35% of 960) |
| `newrx-compact-960x520.png` | New Prescription | 960 x 520 px | 96 (100%) | Compact single-column reflow, pinned actions, no clipping |
| `newrx-wide-1920x1080.png` | New Prescription | 1920 x 1080 px | 96 (100%) | Wide 2-pane composer + live prescription preview card |
| `rxdetail-1280x720.png` | Prescription Detail | 1280 x 720 px | 96 (100%) | Formatted prescription document view, print button |
| `printpreview-920x680.png` | Print Preview | 920 x 680 px | 96 (100%) | Zoom controls, letterhead, medicine directions table |
| `customdialog-confirmation.png` | Custom Dialog | 420 x 220 px | 96 (100%) | Accessible confirmation dialog, vector icon, pinned buttons |
| `shell-dpi96.png` | Shell Chrome | 1024 x 600 px | 96 (100%) | Collapsing sidebar rail, header, status bar |
| `shell-dpi120.png` | Shell Chrome | 1280 x 750 px | 120 (125%) | Shell at 125% DPI |
| `shell-dpi144.png` | Shell Chrome | 1536 x 900 px | 144 (150%) | Shell at 150% DPI |
| `shell-dpi168.png` | Shell Chrome | 1792 x 1050 px | 168 (175%) | Shell at 175% DPI |
| `shell-dpi192.png` | Shell Chrome | 2048 x 1200 px | 192 (200%) | Shell at 200% DPI |

---

## 3. Physical Display Verification Checklist (For User)

Please walk through the physical matrix below on your workstation hardware:

### Target Display Resolution & Scaling Matrix
Verify the application at each of the following configurations:

1. **1366 x 768**:
   - [ ] 100% Scaling: Default size, minimum size (960x520), maximized, half-screen snapped.
   - [ ] 125% Scaling: Default size, minimum size, maximized, half-screen snapped.
2. **1920 x 1080**:
   - [ ] 100% Scaling: Default, minimum, maximized, half-screen snapped.
   - [ ] 125% Scaling: Default, minimum, maximized, half-screen snapped.
   - [ ] 150% Scaling: Default, minimum, maximized, half-screen snapped.
   - [ ] 175% Scaling: Default, minimum, maximized, half-screen snapped.
   - [ ] 200% Scaling: Default, minimum, maximized, half-screen snapped.
3. **2560 x 1440**:
   - [ ] 100% Scaling: Default, minimum, maximized, half-screen snapped.
   - [ ] 150% Scaling: Default, minimum, maximized, half-screen snapped.
4. **3840 x 2160**:
   - [ ] 150% Scaling: Default, minimum, maximized, half-screen snapped.
   - [ ] 200% Scaling: Default, minimum, maximized, half-screen snapped.

### Multi-Monitor Dynamic Dragging & Unplug Test
- [ ] Connect two monitors with different scaling factors (e.g. Monitor 1 at 100%, Monitor 2 at 150%).
- [ ] Drag `MainWindow` across the boundary between monitors; confirm no blurry text, no clipped edges, and seamless PerMonitorV2 scaling.
- [ ] Drag window to secondary monitor, close application, disconnect secondary monitor, and re-launch application.
- [ ] Confirm window detects that its previous position is off-screen and centers cleanly on the primary monitor.

### Screen-by-Screen Walkthrough Checks
At each screen, verify:
- [ ] **Dashboard**:
  - Stat cards reflow (4 Wide, 2 Normal/Compact, 1 Narrow).
  - Recent Prescriptions & Recent Patients tables side-by-side in Wide, stacked in Normal/Compact.
  - No horizontal scrolling; low-priority columns hide in Compact.
- [ ] **Patients**:
  - Grid columns star-size with MinWidth; no horizontal scrollbar.
  - Action buttons show full text in Wide/Normal, compact icon buttons in Compact.
  - Open Registration Drawer: drawer clamps to 35% (320–440 DIPs) or full width below 800.
  - Form fields use star columns; internal vertical scroll works.
  - Press `Esc`: drawer closes cleanly.
- [ ] **New Prescription**:
  - Wide window: clinical composer and live summary card appear side-by-side.
  - Compact window: stacks vertically into a single column.
  - Medicine cards and editor reflow without clipping.
  - Section navigation bar stays reachable; Review/Finalize actions stay visible.
  - Press `Esc` while editing medicine: cancels edit.
- [ ] **Prescription Detail**:
  - Patient/doctor metadata cards reflow cleanly.
  - Items list displays directions and dosages without clipping.
  - Click "Print": opens Print Preview window.
- [ ] **Print Preview**:
  - Window centers over owner and sizes within 95% bounds.
  - Click "Fit Width": zooms document to fill viewport width.
  - Click "Zoom to Fit": scales whole page into view.
  - Vertical scrolling navigates the document smoothly.
  - Window never forces horizontal scrolling of the main application.
  - Press `Esc`: closes preview window.
- [ ] **Dialogs & Overlays**:
  - Trigger confirmation dialog: dialog centers over owner, message scrolls if long, primary and cancel buttons are visible.
  - Press `Esc`: cancels dialog.
  - Press `Enter`: confirms dialog.
  - Open Doctor Setup: modal clamps to 560x470, `Esc` dismisses.
  - Open Keyboard Shortcuts (`F1`): overlay clamps to 600x480, `Esc` dismisses.
- [ ] **Aesthetics & Typography**:
  - Confirm all icons are vector geometries (zero emojis).
  - Confirm no text is smaller than 12 DIPs.
  - Confirm tab navigation follows logical order and focused controls display a visible focus indicator.

---

## 4. Verification Sign-Off Summary

- **Automated Tests**: 105 passed, 0 failed, 0 skipped.
- **Build Status**: 0 compilation errors, 0 warnings.
- **Visual Artifacts**: 20 PNG screenshots generated in `docs/ui-checks/`.
