using System.Collections.Generic;

namespace ValveKeyValue.Deserialization
{
    class KVPartialState<TState> : KVPartialState
    {
        public Stack<TState> States { get { return _states; } }
        private Stack<TState> _states = new Stack<TState>();
    }
}
