using System;
using System.Collections.Generic;

namespace EOS.Unity
{
    [Serializable]
    public sealed class IncarnationIndex
    {
        public List<IncarnationEntry> Entries = new();
        public List<IncarnationRedirect> Redirects = new();
    }

    [Serializable]
    public sealed class IncarnationEntry
    {
        public string Id;
        public string Path;
    }

    [Serializable]
    public sealed class IncarnationRedirect
    {
        public string OldId;
        public string NewId;
    }
}
