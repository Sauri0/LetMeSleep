using System;

namespace LetMeSleep.Online
{
    /// <summary>
    /// Copy-on-write packet subscribers dispatched in isolation: one throwing consumer (room, gameplay, voice,
    /// appearance, lobby movement) must neither starve the others of the packet nor stop the receive drain.
    /// </summary>
    public sealed class PacketHandlerList
    {
        private Action<string, byte, ArraySegment<byte>>[] handlers = Array.Empty<Action<string, byte, ArraySegment<byte>>>();

        public int Count => handlers.Length;

        public void Add(Action<string, byte, ArraySegment<byte>> handler)
        {
            if (handler == null) return;
            var next = new Action<string, byte, ArraySegment<byte>>[handlers.Length + 1];
            Array.Copy(handlers, next, handlers.Length); next[handlers.Length] = handler;
            handlers = next;
        }

        /// <summary>Removes the last matching subscription, like multicast delegate removal.</summary>
        public void Remove(Action<string, byte, ArraySegment<byte>> handler)
        {
            if (handler == null) return;
            int index = Array.LastIndexOf(handlers, handler);
            if (index < 0) return;
            var next = new Action<string, byte, ArraySegment<byte>>[handlers.Length - 1];
            Array.Copy(handlers, 0, next, 0, index);
            Array.Copy(handlers, index + 1, next, index, handlers.Length - index - 1);
            handlers = next;
        }

        public void Clear() => handlers = Array.Empty<Action<string, byte, ArraySegment<byte>>>();

        public void Dispatch(string member, byte channel, ArraySegment<byte> packet, Action<Exception> failed)
        {
            var current = handlers;
            for (int i = 0; i < current.Length; i++)
            {
                try { current[i](member, channel, packet); }
                catch (Exception error) { failed?.Invoke(error); }
            }
        }
    }
}
