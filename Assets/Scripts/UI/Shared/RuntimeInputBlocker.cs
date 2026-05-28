using System.Collections.Generic;
using UnityEngine;

namespace LegendsOfWarAndMagic.UI.Shared
{
    public static class RuntimeInputBlocker
    {
        private static readonly List<Object> Owners = new();

        public static bool IsBlocked
        {
            get
            {
                Owners.RemoveAll(owner => owner == null);
                return Owners.Count > 0;
            }
        }

        public static void SetBlocked(Object owner, bool blocked)
        {
            if (owner == null)
            {
                return;
            }

            Owners.RemoveAll(existing => existing == null);
            if (blocked)
            {
                if (!Owners.Contains(owner))
                {
                    Owners.Add(owner);
                }
            }
            else
            {
                Owners.Remove(owner);
            }

            ApplyCursorState();
        }

        public static void ReleaseAll()
        {
            Owners.Clear();
            ApplyCursorState();
        }

        private static void ApplyCursorState()
        {
            if (IsBlocked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
