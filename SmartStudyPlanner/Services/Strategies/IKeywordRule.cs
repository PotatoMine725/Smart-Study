using System;

namespace SmartStudyPlanner.Services.Strategies
{
    // Match 1 keyword set -> value. Dùng cho SmartParser keyword rules.
    public interface IKeywordRule<T>
    {
        bool TryMatch(string lowerInput, out T value);
    }

    public class ContainsAnyRule<T> : IKeywordRule<T>
    {
        private readonly Func<T> _factory;
        private readonly string[] _keywords;

        public ContainsAnyRule(T value, params string[] keywords)
        {
            _factory = () => value;
            _keywords = keywords;
        }

        public ContainsAnyRule(Func<T> factory, params string[] keywords)
        {
            _factory = factory;
            _keywords = keywords;
        }

        public bool TryMatch(string lowerInput, out T value)
        {
            foreach (var k in _keywords)
            {
                if (lowerInput.Contains(k))
                {
                    value = _factory();
                    return true;
                }
            }
            value = default!;
            return false;
        }
    }
}
