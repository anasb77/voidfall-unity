namespace VoidFall.Runtime
{
    /// <summary>
    /// Compact slot bookkeeping for entity families that share exact semantics:
    /// a validated duplicate guard on append and swap-with-last removal.
    /// Replaces six hand-copied order/position/count field trios.
    ///
    /// Arrays allocate lazily so reflection-built test fixtures that never run
    /// Awake still work. Capacity equals the owning state array's length, so
    /// the append overflow guard is unreachable exactly as before.
    /// </summary>
    public sealed class SlotOrder
    {
        private readonly int _slotBound;
        private int[] _order;
        private int[] _position;
        private int _count;

        public SlotOrder(int slotBound)
        {
            _slotBound = slotBound;
        }

        public int Count => _count;

        public int SlotAt(int index) => _order[index];

        public void Reset()
        {
            _count = 0;
            if (_order == null) return;
            for (var index = 0; index < _order.Length; index++)
            {
                _order[index] = -1;
                _position[index] = -1;
            }
        }

        public void Append(int slot)
        {
            if (_order == null)
            {
                _order = new int[_slotBound];
                _position = new int[_slotBound];
            }
            if (slot < 0 || slot >= _slotBound || _count >= _order.Length) return;
            var position = _position[slot];
            if (position >= 0 && position < _count && _order[position] == slot) return;
            _position[slot] = _count;
            _order[_count++] = slot;
        }

        public void Remove(int slot)
        {
            if (_order == null || slot < 0 || slot >= _slotBound) return;
            var position = _position[slot];
            if (position < 0 || position >= _count || _order[position] != slot)
            {
                _position[slot] = -1;
                return;
            }
            var lastPosition = --_count;
            if (position != lastPosition)
            {
                var replacement = _order[lastPosition];
                _order[position] = replacement;
                _position[replacement] = position;
            }
            _order[lastPosition] = -1;
            _position[slot] = -1;
        }
    }
}
