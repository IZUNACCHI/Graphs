using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Core.Widgets
{
    /// <summary>
    /// A TextField with a built-in per-focus-session local undo stack and a
    /// commit-on-blur event.
    ///
    /// Behaviour:
    ///  - On focus-in: snapshot originalValue, clear local stacks.
    ///  - On each value change while focused: push previous value onto a
    ///    local undo stack, merging consecutive changes inside a 500ms
    ///    burst window.
    ///  - Ctrl+Z while focused: pop local undo, restore that value. Does
    ///    NOT reach any outer handlers (propagation stopped).
    ///  - Ctrl+Shift+Z while focused: redo within the local stack.
    ///  - Delete / Backspace while focused: handled as character edits by
    ///    the base TextField; propagation stopped so outer handlers don't
    ///    also act on them.
    ///  - On focus-out: if the net value changed, fires OnCommit(old, new).
    ///    If net value matches originalValue, no event fires. Local stacks
    ///    are discarded either way.
    ///
    /// Domain-agnostic — knows nothing about Nodes, Commands, or EventBus.
    /// Callers subscribe to OnCommit and do whatever they want with the
    /// net change (typically pushing a command onto an undo system).
    ///
    /// For external updates (e.g. another system changed the underlying
    /// value while the user is editing), call SetExternalValue — it updates
    /// the displayed text silently and invalidates the local stack if
    /// focused.
    /// </summary>
    public sealed class FlexTextField : TextField
    {
        /// <summary>
        /// Fires when the user commits a net change (blur with new value
        /// different from focus-in value). Arguments: (oldValue, newValue).
        /// </summary>
        public event Action<string, string> OnCommit;

        /// <summary>
        /// Burst-merge window. Consecutive value changes within this many
        /// seconds collapse into a single local undo entry.
        /// </summary>
        public float BurstMergeSeconds { get; set; } = 0.5f;

        private string originalValue;
        private bool focused;
        private bool suppressChange;      // re-entrancy guard for programmatic writes
        private float lastChangeTime;
        private readonly Stack<string> undoStack = new();
        private readonly Stack<string> redoStack = new();

        public FlexTextField() : this(multiline: false) { }

        public FlexTextField(bool multiline)
        {
            this.multiline = multiline;

            RegisterCallback<FocusInEvent>(OnFocusIn);
            RegisterCallback<FocusOutEvent>(OnFocusOut);
            // Using the underlying ChangeEvent<string> directly instead of the
            // RegisterValueChangedCallback extension method — the extension
            // isn't always visible from this context.
            RegisterCallback<ChangeEvent<string>>(OnValueChanged);
            // Trickle-down so we intercept Ctrl+Z / Ctrl+Shift+Z / Delete /
            // Backspace before any ancestor handler sees them.
            RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        }

        /// <summary>
        /// Set the field's displayed value from an external source without
        /// firing ChangeEvent or OnCommit. If the widget is currently
        /// focused, the local undo/redo stacks are invalidated and
        /// originalValue is reset — the baseline is no longer valid.
        /// </summary>
        public void SetExternalValue(string newValue)
        {
            var v = newValue ?? "";
            if (value == v) return;

            SetValueSilently(v);

            if (focused)
            {
                undoStack.Clear();
                redoStack.Clear();
                originalValue = v;
                lastChangeTime = 0f;
            }
        }

        // ---------- internal plumbing ----------

        private void OnFocusIn(FocusInEvent _)
        {
            originalValue = value ?? "";
            focused = true;
            undoStack.Clear();
            redoStack.Clear();
            lastChangeTime = 0f;
        }

        private void OnFocusOut(FocusOutEvent _)
        {
            if (!focused) return;
            focused = false;

            var final = value ?? "";
            var start = originalValue ?? "";

            undoStack.Clear();
            redoStack.Clear();

            if (final != start)
                OnCommit?.Invoke(start, final);
        }

        private void OnValueChanged(ChangeEvent<string> evt)
        {
            if (suppressChange) return;

            var before = evt.previousValue ?? "";
            var after = evt.newValue ?? "";
            if (before == after) return;

            if (!focused) return;

            bool mergeIntoLast =
                undoStack.Count > 0 &&
                (Time.unscaledTime - lastChangeTime) <= BurstMergeSeconds;

            if (!mergeIntoLast)
                undoStack.Push(before);
            // If merging, the burst's earlier baseline on the stack is kept
            // so undo reverts the entire burst.

            redoStack.Clear();
            lastChangeTime = Time.unscaledTime;
        }

        private void OnKeyDown(KeyDownEvent e)
        {
            // Stop Delete / Backspace from bubbling — TextField already
            // handles them as character edits.
            if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
            {
                e.StopPropagation();
                return;
            }

            bool ctrl = e.ctrlKey || e.commandKey;
            if (!ctrl) return;

            if (e.keyCode == KeyCode.Z && !e.shiftKey)
            {
                if (undoStack.Count == 0) { e.StopPropagation(); return; }

                var previous = undoStack.Pop();
                redoStack.Push(value ?? "");
                SetValueSilently(previous);
                lastChangeTime = 0f;
                e.StopPropagation();
                return;
            }

            if (e.keyCode == KeyCode.Z && e.shiftKey)
            {
                if (redoStack.Count == 0) { e.StopPropagation(); return; }

                var next = redoStack.Pop();
                undoStack.Push(value ?? "");
                SetValueSilently(next);
                lastChangeTime = 0f;
                e.StopPropagation();
                return;
            }
        }

        private void SetValueSilently(string v)
        {
            suppressChange = true;
            try { SetValueWithoutNotify(v ?? ""); }
            finally { suppressChange = false; }
        }
    }
}