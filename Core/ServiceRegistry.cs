using System;
using ATS_WPF.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ATS_WPF.Core
{
    /// <summary>
    /// Lightweight service registry bridge for gradual DI migration.
    /// This now wraps the Microsoft.Extensions.DependencyInjection provider.
    /// </summary>
    public static class ServiceRegistry
    {
        private static IServiceProvider? _provider;

        public static void SetProvider(IServiceProvider provider)
        {
            _provider = provider;
        }

        public static T GetService<T>() where T : class
        {
            if (_provider == null)
            {
                throw new InvalidOperationException("ServiceRegistry provider has not been initialized. Call SetProvider first.");
            }
            
            return _provider.GetRequiredService<T>();
        }
    }
}

