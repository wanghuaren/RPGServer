namespace Ddxy.GameServer.Core
{
    public class DynamicProperty<T>
    {
        private T _value;
        public bool Changed { get; private set; }

        public DynamicProperty(T value)
        {
            _value = value;
            Changed = false;
        }

        public void Reset()
        {
            Changed = false;
        }

        public void Reset(T value)
        {
            _value = value;
            Changed = false;
        }

        public T Value
        {
            get => _value;
            set
            {
                if (value == null && _value == null ||
                    value != null && value.Equals(_value) ||
                    _value != null && _value.Equals(value))
                {
                    return;
                }

                _value = value;
                Changed = true;
            }
        }
    }
}