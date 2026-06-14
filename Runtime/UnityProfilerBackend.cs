using System.Collections.Generic;
using EOS.Profiling;
using Unity.Profiling;

namespace EOS.Unity
{
    /// <summary><c>IEosProfilerBackend</c> that maps EOS profiler spans onto Unity <c>ProfilerMarker</c>s, caching markers by label and stacking for balanced nesting.</summary>
    public sealed class UnityProfilerBackend : IEosProfilerBackend
    {
        readonly Dictionary<string, ProfilerMarker> _markers = new();
        readonly Stack<ProfilerMarker> _stack = new();

        /// <summary>Begins a profiler span for the label, creating and caching its marker on first use.</summary>
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

        /// <summary>Ends the most recently begun span; a safe no-op when the stack is empty.</summary>
        public void End()
        {
            if (_stack.Count == 0) return;
            _stack.Pop().End();
        }
    }
}
