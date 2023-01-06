using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibDMMGamePlayer
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDMMGamePlayer(this IServiceCollection services)
        {
            services.AddHttpClient("DMMGamePlayer")
                .ConfigurePrimaryHttpMessageHandler(() =>
                {
                    return new HttpClientHandler()
                    {
                        AllowAutoRedirect = false,
                        UseCookies = false
                    };
                });
            return services;
        }
    }
}
