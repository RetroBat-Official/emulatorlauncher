using System;

namespace SquashFS.Reader
{
    internal sealed class SquashFsException : Exception
    {
        public SquashFsException(string msg) : base(msg) { }
    }
}
