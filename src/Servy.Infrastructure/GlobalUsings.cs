// GlobalUsings.cs
using Fody;

// This attribute tells ConfigureAwait.Fody to apply .ConfigureAwait(false) 
// to every await statement in this assembly automatically.
[assembly: ConfigureAwait(false)]