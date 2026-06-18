using System;

namespace ModernTinyWall.TinyWall.Parser
{
    public abstract class ParserVariable
    {
        internal int Start;

        internal abstract string Resolve(string str);
        internal abstract string GetOpeningTag();
        internal abstract int GetOpeningTagLength();
    }
}
