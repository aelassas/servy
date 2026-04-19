namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Polyfill for CallerArgumentExpressionAttribute to allow C# 10+ features 
    /// in .NET Framework 4.8.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    internal sealed class CallerArgumentExpressionAttribute : Attribute
    {
        public CallerArgumentExpressionAttribute(string parameterName)
        {
            ParameterName = parameterName;
        }

        public string ParameterName { get; }
    }
}