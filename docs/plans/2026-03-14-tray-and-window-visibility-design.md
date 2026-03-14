# Design: Always-hidden fences with tray control

## Goal
Keep all fence windows out of Task View and the taskbar. Provide a tray icon with menu actions to control visibility and core actions (AI classify, autostart, exit).

## Scope
- Fence windows never appear in Task View or the taskbar.
- Tray icon is the primary control surface.
- Left click shows the tray menu only.
- Tray menu items: Show all fences, Hide all fences, AI classify, Autostart toggle, Exit.

## Architecture
- Add a tray controller that owns a WinForms NotifyIcon and its ContextMenuStrip.
- Initialize the tray controller in App startup and keep it alive for the app lifetime.
- Fence windows are created as before but forced to remain hidden from Task View:
  - ShowInTaskbar = false
  - Avoid showing them in Task View by never calling ShowInTaskbar true.
- Visibility is controlled by the tray controller by calling Show() or Hide() on each fence window.

## Data flow
1. App starts and loads fences.
2. Tray controller builds a menu and wires menu events.
3. Menu actions delegate to PalisadesManager:
   - Show all: show all fence windows.
   - Hide all: hide all fence windows.
   - AI classify: call the existing manual AI classify pipeline.
   - Autostart: toggle registry entry, update menu label and checked state.
   - Exit: call the existing app shutdown path.

## Error handling
- AI classify failure should show the current error message and fall back to local rules.
- Autostart failure should show an error and restore the previous menu state.
- Tray initialization failure should warn the user once and keep the app running.

## Testing
- Start the app and confirm no fence appears in Task View.
- Use tray menu to show/hide all fences.
- Verify AI classify runs from tray menu.
- Toggle autostart and verify state persists after reopen.
- Exit from tray menu and confirm the process ends.

## Non-goals
- No changes to the existing fence UI layout or behavior.
- No system shutdown restore change (keep current logic).
