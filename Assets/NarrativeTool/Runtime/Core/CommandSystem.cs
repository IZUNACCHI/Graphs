using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarrativeTool.Core
{
    /// <summary>
    /// Central undo/redo manager. Executes ICommands, maintains the undo/redo
    /// stacks, supports merging of consecutive commands, and supports transactions
    /// for multi-step operations (which appear as a single entry on the stack).
    /// </summary>
    public sealed class CommandSystem
    {
        private readonly Stack<ICommand> undo = new();
        private readonly Stack<ICommand> redo = new();
        private Transaction activeTransaction;

        public int UndoCount => undo.Count;
        public int RedoCount => redo.Count;
        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;

        /// <summary>
        /// Execute a command, apply it, and push it on the undo stack. Clears
        /// the redo stack (standard undo/redo semantics).
        ///
        /// If a transaction is open, the command is added to the transaction
        /// instead of being pushed individually.
        ///
        /// If the top of the undo stack accepts a merge with this command,
        /// the new command swallows the old one and only the merged result
        /// remains on the stack.
        /// </summary>
        public void Execute(ICommand cmd)
        {
            if (cmd == null) throw new ArgumentNullException(nameof(cmd));

            cmd.Do();

            if (activeTransaction != null)
            {
                activeTransaction.Add(cmd);
                Debug.Log($"[Cmd] {cmd.Name} (in transaction)");
                return;
            }

            // Try to merge with top of stack
            if (undo.Count > 0)
            {
                var top = undo.Peek();
                if (cmd.TryMerge(top))
                {
                    undo.Pop(); // discard the one that got merged in
                    undo.Push(cmd);
                    redo.Clear();
                    Debug.Log($"[Cmd] {cmd.Name} merged — stack size: {undo.Count}");
                    return;
                }
            }

            undo.Push(cmd);
            redo.Clear();
            Debug.Log($"[Cmd] {cmd.Name} executed — stack size: {undo.Count}");
        }

        /// <summary>
        /// Undo the top command. No-op if stack is empty.
        /// </summary>
        public void Undo()
        {
            if (undo.Count == 0) { Debug.Log("[Cmd] Undo — nothing to undo"); return; }
            var cmd = undo.Pop();
            cmd.Undo();
            redo.Push(cmd);
            Debug.Log($"[Cmd] Undo {cmd.Name}");
        }

        /// <summary>
        /// Redo the most recently undone command. No-op if redo stack is empty.
        /// </summary>
        public void Redo()
        {
            if (redo.Count == 0) { Debug.Log("[Cmd] Redo — nothing to redo"); return; }
            var cmd = redo.Pop();
            cmd.Do();
            undo.Push(cmd);
            Debug.Log($"[Cmd] Redo {cmd.Name}");
        }

        /// <summary>
        /// Begin a transaction. Every Execute until the returned handle is
        /// disposed is grouped into one CompositeCommand, which appears as a
        /// single entry on the undo stack.
        ///
        /// Use with `using`:
        ///   using (Commands.BeginTransaction("Delete selection")) { ... }
        /// </summary>
        public IDisposable BeginTransaction(string name)
        {
            if (activeTransaction != null)
                throw new InvalidOperationException("Nested transactions are not supported.");
            activeTransaction = new Transaction(name, this);
            Debug.Log($"[Cmd] Transaction '{name}' opened");
            return activeTransaction;
        }

        internal void EndTransaction(Transaction tx)
        {
            if (activeTransaction != tx) return;
            activeTransaction = null;
            if (tx.Count == 0)
            {
                Debug.Log($"[Cmd] Transaction '{tx.Name}' closed (empty — discarded)");
                return;
            }
            var composite = new CompositeCommand(tx.Name, tx.Commands);
            undo.Push(composite);
            redo.Clear();
            Debug.Log($"[Cmd] Transaction '{tx.Name}' committed ({tx.Count} ops) — stack size: {undo.Count}");
        }

        /// <summary>Clear both stacks. Call after loading a new project.</summary>
        public void Clear()
        {
            undo.Clear();
            redo.Clear();
            Debug.Log("[Cmd] Stacks cleared");
        }

        // -- internal --

        internal sealed class Transaction : IDisposable
        {
            public string Name { get; }
            public List<ICommand> Commands { get; } = new();
            public int Count => Commands.Count;

            private readonly CommandSystem owner;
            private bool disposed;

            public Transaction(string name, CommandSystem owner)
            {
                Name = name; this.owner = owner;
            }

            public void Add(ICommand cmd) => Commands.Add(cmd);

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                owner.EndTransaction(this);
            }
        }

        private sealed class CompositeCommand : ICommand
        {
            private readonly List<ICommand> ops;
            public string Name { get; }
            public CompositeCommand(string name, List<ICommand> ops)
            {
                Name = name; this.ops = new List<ICommand>(ops);
            }
            public void Do() { for (int i = 0; i < ops.Count; i++) ops[i].Do(); }
            public void Undo() { for (int i = ops.Count - 1; i >= 0; i--) ops[i].Undo(); }
            public bool TryMerge(ICommand previous) => false; // composites never merge
        }
    }
}