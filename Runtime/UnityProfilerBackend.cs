using System.Collections.Generic;
using EOS.Profiling;
using Unity.Profiling;

namespace EOS.Unity
{
    public sealed class UnityProfilerBackend : IEosProfilerBackend
    {
        readonly Dictionary<string, ProfilerMarker> _markers = new();
        readonly Stack<ProfilerMarker> _stack = new();

        public void Begin(string label)
        {
            if (!_markers.TryGetValue(label, out var marker))
            {
                marker = new ProfilerMarker(label);
                _markers[label] = marker;
            }
            marker.Begin();
            _stack.Push(marker);
        }

        public void End()
        {
            if (_stack.Count == 0) return;
            _stack.Pop().End();
        }
    }
}
