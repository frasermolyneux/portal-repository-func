using System;
using System.Reflection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var type = typeof(HealthCheckService);
var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

foreach (var method in methods)
{
    if (method.Name.Contains("CheckHealth"))
    {
        Console.WriteLine($"Method: {method.Name}");
        foreach (var param in method.GetParameters())
        {
            Console.WriteLine($"  Parameter: {param.ParameterType.Name} {param.Name} (Optional: {param.IsOptional})");
        }
    }
}
