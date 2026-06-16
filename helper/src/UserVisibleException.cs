using System;

namespace YNWpsTranslatorHelper
{
    internal sealed class UserVisibleException : Exception
    {
        public UserVisibleException(string message)
            : base(message)
        {
        }
    }
}
