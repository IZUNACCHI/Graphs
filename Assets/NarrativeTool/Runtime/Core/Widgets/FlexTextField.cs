using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Core.Widgets
{
    /// <summary>
    /// A TextField with a per-focus-session local undo stack and a commit-on-blur event.
    /// Ctrl+Z / Ctrl+Shift+Z work only inside this field while it's focused.
    /// On blur / Enter (single-line) → fires OnCommit(old, new) if value changed.
    /// </summary>
    public sealed class FlexTextField : TextField
    {
        public event System.Action<string, string> OnCommit;

        private string originalValue;
        private bool focused;
        private readonly Stack<string> undoStack = new();
        private readonly Stack<string> redoStack = new();
        private float lastChangeTime;
        private bool suppressChange;

        public float BurstMergeSeconds { get; set; } = 0.2f;

        public FlexTextField(bool multiline = false) : this(null, multiline) { }

        public FlexTextField(string label, bool multiline = false)
        {
            this.multiline = multiline;
            if (label != null) this.label = label;

            RegisterCallback<FocusInEvent>(OnFocusIn);
            RegisterCallback<FocusOutEvent>(OnFocusOut);
            RegisterCallback<ChangeEvent<string>>(OnValueChanged);
            RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        }

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
            if (final != originalValue)
                OnCommit?.Invoke(originalValue, final);

            undoStack.Clear();
            redoStack.Clear();
        }

        private void OnValueChanged(ChangeEvent<string> evt)
        {
            if (suppressChange) return;
            var before = evt.previousValue ?? "";
            var after = evt.newValue ?? "";
            if (before == after) return;
            if (!focused) return;

            bool merge = undoStack.Count > 0 &&
                         (Time.unscaledTime - lastChangeTime) <= BurstMergeSeconds;
            if (!merge) undoStack.Push(before);

            redoStack.Clear();
            lastChangeTime = Time.unscaledTime;
        }

        private void OnKeyDown(KeyDownEvent e)
        {
            bool ctrl = e.ctrlKey || e.commandKey;
            if (!ctrl) return;

            if (e.keyCode == KeyCode.Z && !e.shiftKey)
            {
                if (undoStack.Count == 0) { e.StopPropagation(); return; }
                var prev = undoStack.Pop();
                redoStack.Push(value ?? "");
                SetValueSilently(prev);
                lastChangeTime = 0f;
                e.StopPropagation();
            }
            else if (e.keyCode == KeyCode.Z && e.shiftKey)
            {
                if (redoStack.Count == 0) { e.StopPropagation(); return; }
                var next = redoStack.Pop();
                undoStack.Push(value ?? "");
                SetValueSilently(next);
                lastChangeTime = 0f;
                e.StopPropagation();
            }
        }

        private void SetValueSilently(string v)
        {
            suppressChange = true;
            SetValueWithoutNotify(v ?? "");
            suppressChange = false;
        }
    }
}