using System;

namespace LegendsOfWarAndMagic.DebugTools.Core
{
    public sealed class DebugScope : IDisposable
    {
        private DebugSession session;
        private readonly int eventId;

        public DebugScope(DebugSession session, int eventId)
        {
            this.session = session;
            this.eventId = eventId;
        }

        public void Dispose()
        {
            if (session == null)
            {
                return;
            }

            session.EndScope(eventId);
            session = null;
        }
    }
}
