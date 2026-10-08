# DoctorRx UI & Layout Architecture Guidelines

This document governs the design system, responsive layout engine, typography, spacing tokens, per-screen responsive reflow patterns, overlay rules, dialog sizing, and window placement standards for DoctorRx desktop workstation clients across Windows 10 and Windows 11.

---

## 1. Responsive Layout Modes & Breakpoints

DoctorRx implements a multi-tier responsive shell and view hierarchy that dynamically adapts between compact netbooks/high-DPI scaling displays and ultra-wide clinical monitors.

| Mode | Window Width (DIPs) | Sidebar Behavior | Clinical Form & Content Reflow | Secondary Badges / Details |
| :--- | :--- | :--- | :--- | :--- |
| **Compact** | `< 1024` | Collapses to **64 DIP icon rail** (can be toggled to expanded via hamburger) | Forms stack in a **single column**; side panels transform into **overlays/drawers** | Secondary table columns and status metadata collapsed/hidden; titles trimmed with ellipses |
| **Normal** | `1024` to `1439` | Standard **240 DIP expanded sidebar** | Flexible single or two-column layout | Full status bar and header metadata visible |
| **Wide** | `>= 1440` | Standard **240 DIP expanded sidebar** | Multi-column clinical workflows (e.g. form + live prescription summary side-by-side) | Extended telemetry and metrics visible |

### Minimum Supported Display Sizes
- **Absolute Minimum Supported Application Size**: `960 x 520 DIPs`.
- **Workstation Shell Clamping Minimum**: `MinWidth="900"`, `MinHeight="520"`.
- **Target Configurations Validated**:
  - `1366 x 768` laptop displays at `125%` DPI scaling (`1092 x 614` effective DIP work area).
  - `1920 x 1080` displays at `200%` DPI scaling (`960 x 540` effective DIP work area).
  - High-density multi-monitor configurations spanning `96 DPI` (100%) to `192 DPI` (200%).

---

## 2. Design System Tokens

All controls, view templates, dialogs, and shell windows must source styling exclusively from centralized tokens defined in `Theme/Styles.xaml`, `Theme/Colors.xaml`, and `Theme/Icons.xaml`.

### Typography Tokens
Per clinical legibility and patient safety standards, **no text in DoctorRx may be rendered below 12 DIPs**.

| Token Key | Size (DIPs) | Weight / Usage | Target Controls |
| :--- | :--- | :--- | :--- |
| `FontSizeH1` | `24` | SemiBold | Main screen titles, modal headers |
| `FontSizeH2` | `18` | SemiBold | Section headings, panel headers |
| `FontSizeH3` | `15` | SemiBold | Card titles, group dividers |
| `FontSizeBody` | `13.5` | Regular / Medium | Form inputs, primary buttons, grid rows |
| `FontSizeBodyMuted` | `12.5` | Regular | Secondary descriptions, placeholders |
| `FontSizeCaption` | `12` | Medium / SemiBold | Status badges, table headers, captions (floor limit: 12 DIPs) |

### Spacing Tokens
DoctorRx adheres to an 8-point base grid with 4-point micro increments:

| Token Key | Double (DIPs) | Thickness Token | Primary Usage |
| :--- | :--- | :--- | :--- |
| `SpacingXXS` | `2` | `SpacingThicknessXXS` | Micro borders, button gaps |
| `SpacingXS` | `4` | `SpacingThicknessXS` | Label-to-input vertical margins |
| `SpacingSM` | `8` | `SpacingThicknessSM` | Input field padding, button margins |
| `SpacingMD` | `12` | `SpacingThicknessMD` | Control row gaps, column spacing |
| `SpacingLG` | `16` | `SpacingThicknessLG` | Card internal padding, section margins |
| `SpacingXL` | `20` | `SpacingThicknessXL` | Modal card padding, page outer margins |
| `Spacing2XL` | `24` | `SpacingThickness2XL` | Container margins, header padding |
| `Spacing3XL` | `32` | `SpacingThickness3XL` | Major layout section separation |

---

## 3. Mandatory UI Architectural Rules

### Rule 1: No Fixed Content Widths & Vertical Scroll Only
- Clinical content views must never declare fixed pixel widths or heights on text containers (`Width="1200"` or `Height="400"` on text-containing controls is prohibited).
- Use `MinWidth`, `MaxWidth`, `MinHeight`, and `HorizontalAlignment="Stretch"` instead.
- The shell content host enforces `HorizontalScrollBarVisibility="Disabled"` and `VerticalScrollBarVisibility="Auto"`. **Horizontal scrolling is strictly prohibited** anywhere in the application.

### Rule 2: Proportional Star Columns in DataGrids
- DataGrid columns must use proportional star widths combined with explicit `MinWidth` constraints (e.g. `Width="*" MinWidth="140"`).
- In **Compact** mode (`< 1024 DIPs`), low-priority columns (such as non-essential timestamps, secondary tags, or internal codes) must be collapsed or dropped to preserve readability for critical clinical columns.

### Rule 3: Vector Icons Only (Zero Emojis)
- Emoji characters (e.g., `📊`, `📝`, `💊`, `⚙️`, `⚠️`) are strictly prohibited in production UI chrome due to rendering and scaling discrepancies across Windows 10 and Windows 11.
- All icons must use SVG vector path geometries declared in `Theme/Icons.xaml` (e.g., `IconDashboard`, `IconNewPrescription`, `IconPatients`, `IconSearch`, `IconRefresh`, `IconPrint`, `IconEdit`, `IconArchive`, `IconZoomFit`).
- Every interactive icon button must declare `AutomationProperties.Name` and a `ToolTip` to guarantee accessibility and screen reader support.

### Rule 4: Custom Dialogs & Modal Windows Sizing
- Custom dialogs and modal overlays must size relative to their parent window:
  - `MaxWidth` and `MaxHeight` must not exceed **90% of the owner window** (or 90% of screen work area if un-owned).
  - Dialogs must fit cleanly at `960 x 520 DIPs` with `MinWidth="380"` and `MinHeight="180"`.
  - Content must scroll vertically if long; action buttons (e.g., Yes, No, Cancel, OK) must remain pinned and visible.
  - Dialogs must center over the owner window on the monitor where the owner resides.
  - Standard keyboard conventions apply: `Esc` cancels/dismisses, `Enter` confirms primary action.

---

## 4. Per-Screen Responsive Reflow Patterns

### B1 — Dashboard
- **Stat Cards**: Rendered via a dynamic responsive grid (`Columns="{Binding StatCardColumns}"`):
  - **Wide (`>= 1440 DIPs`)**: 4 cards per row.
  - **Normal (`1024–1439 DIPs`)**: 2 cards per row.
  - **Compact (`< 1024 DIPs`)**: 2 cards per row, collapsing to 1 card per row if available card width drops below 220 DIPs.
- **Tables (Recent Prescriptions & Recent Patients)**:
  - **Wide**: Side-by-side in a 2-column grid (`Grid.Column="0"` and `Grid.Column="1"`).
  - **Normal / Compact**: Stack vertically into a single column (`Grid.Row="0"` and `Grid.Row="1"`).
  - The Drafts-waiting alert card follows the same width reflow rule.
- **DataGrid Columns**: Proportional star widths with `MinWidth`. In Compact mode, low-priority columns (`Status` and `Items` count) are hidden to eliminate horizontal scrolling.

### B2 — Patients
- **Grid Layout**: Proportional star sizing on `Record #`, `Name`, `Age / Gender`, `Phone`, and `Last Visit`.
- **Action Buttons**: In Wide/Normal mode, full text buttons ("Edit", "Archive"); in Compact mode, reflow to compact icon buttons (`CompactIconButton`, `CompactDangerIconButton`) with tooltips.
- **Patient Registration & Edit Drawer**:
  - Implemented as a slide-over overlay with a semi-transparent backdrop.
  - Clamped width: `clamp(35% of window, 320, 440) DIPs`.
  - In Compact mode (window width < 800 DIPs), drawer expands to **100% full width**.
  - Internal form fields use star columns and wrap inside a vertical `ScrollViewer`.
  - Paging, archiving, DOB picker, duplicate warning, and live search remain fully functional.
  - Pressing `Esc` closes the drawer.

### B3 — New Prescription & Prescription Detail
- **New Prescription**:
  - **Wide (`>= 1440 DIPs`)**: Two-column layout with the interactive clinical composer on the left (star-sized) and live prescription preview card on the right (`Width="360"`).
  - **Normal / Compact (`< 1440 DIPs`)**: Single column layout where the composer stretches full width and preview stacks below.
  - Medicine composer: form inputs reflow across star columns; prescribed medicine cards wrap and stack without clipping.
  - Section navigation bar: stays sticky and reachable; Review/Finalize actions remain pinned and visible without horizontal scrolling.
  - Quick-register drawer: slide-over overlay clamped to `clamp(35%, 320, 440)` DIPs (full width below 800 DIPs).
- **Prescription Detail**:
  - Clinical observations, doctor/patient metadata, and medicine items use star columns with `MinWidth`.
  - Single column stack in Compact mode; two columns in Wide mode.
  - "Print" action launches the responsive `PrintPreviewWindow`.
- **Print Preview**:
  - Sized relative to owner (`MaxWidth="95%"`, `MaxHeight="95%"`).
  - Toolbar features **Zoom to Fit**, **Fit Width**, Zoom In (+15%), Zoom Out (-15%), and Print.
  - Paper container scales dynamically via `ScaleTransform` (`0.4x` to `2.5x`) inside a vertical `ScrollViewer`.
  - Horizontal scrolling of the application window is strictly prevented.
  - Pressing `Esc` closes the preview window.

### B4 — Dialogs & Popups
- **CustomDialogWindow**: Replaces standard Win32 `MessageBox.Show`.
  - Supports `Information`, `Warning`, `Error`, `Confirmation`, and `ConfirmationWithCancel`.
  - Pinned button footer (`PrimaryButton`, `SecondaryButton`, `CancelButton`) ensures action buttons are never obscured.
  - Message body wrapped in `ScrollViewer`.
  - Center-owner positioning on the active monitor.
- **Doctor Setup Modal**:
  - Clamped to `MaxWidth="560"` and `MaxHeight="470"`.
  - Form wrapped in vertical `ScrollViewer`.
  - Centered overlay with backdrop blur/darkening.
- **Keyboard Shortcuts Overlay**:
  - Clamped to `MaxWidth="600"` and `MaxHeight="480"`.
  - Category tables wrapped in `ScrollViewer`.
  - `Esc` dismisses the overlay.

---

## 5. Keyboard & Accessibility Standards

- **Tab Order**: Logical top-to-bottom, left-to-right tab progression across all forms.
- **Visible Focus**: All buttons, text fields, and list items inherit high-contrast visible focus cues (`FocusVisualStyle` using `PrimaryTealLightBrush`).
- **Escape Key Contract**:
  - `Esc` closes the Keyboard Shortcuts overlay.
  - `Esc` cancels and closes the Doctor Setup modal.
  - `Esc` closes the Patient Registration/Edit drawer.
  - `Esc` cancels active Medicine editing in the Prescription Composer.
  - `Esc` closes the Print Preview window.
  - `Esc` cancels or dismisses all `CustomDialogWindow` instances.
- **Screen Reader Support**: All icon-only buttons declare `AutomationProperties.Name` matching their `ToolTip`.

---

## 6. Checklist for New Screens & Dialogs

Before merging any new screen, modal, or overlay into DoctorRx, verify the following 10 requirements:

1. [ ] **No Fixed Widths/Heights on Content**: Use `HorizontalAlignment="Stretch"`, `MinWidth`, `MaxWidth`, `MinHeight`.
2. [ ] **Vertical Scroll Only**: Wrap scrollable content in a `ScrollViewer` with `HorizontalScrollBarVisibility="Disabled"` and `VerticalScrollBarVisibility="Auto"`.
3. [ ] **Minimum Size Validation**: Verify the screen does not clip or overflow at `960 x 520 DIPs`.
4. [ ] **Design Tokens Only**: Use `FontSizeH1` through `FontSizeCaption` (no text below 12 DIPs); use spacing tokens `SpacingXS` through `Spacing3XL`.
5. [ ] **Vector Icons Only**: No emojis. Use geometries from `Theme/Icons.xaml`.
6. [ ] **Responsive Reflow**: Implement 2 columns in Wide (`>= 1440 DIPs`) and 1 column in Compact (`< 1024 DIPs`).
7. [ ] **Drawer & Overlay Clamping**: Overlays clamp to `clamp(35%, 320, 440) DIPs` and expand to 100% width below 800 DIPs.
8. [ ] **Dialog Relative Sizing**: Custom dialogs clamp to `<= 90%` owner bounds, fit in `960 x 520`, scroll content, and pin action buttons.
9. [ ] **Keyboard & Focus**: Verify logical Tab order, visible focus rings, and `Esc` dismisses drawers/modals.
10. [ ] **Accessibility Labels**: Every icon-only button specifies `AutomationProperties.Name` and `ToolTip`.
