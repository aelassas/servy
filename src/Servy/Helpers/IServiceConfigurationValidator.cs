using Servy.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Servy.Helpers
{
    public interface IServiceConfigurationValidator
    {
        /// <summary>
        /// Validates the given service configuration.
        /// </summary>
        /// <param name="dto">The service DTO containing configuration.</param>
        /// <param name="wrapperExePath">Path to the wrapper executable.</param>
        /// <returns>True if valid, otherwise false.</returns>
        bool Validate(ServiceDto dto, string wrapperExePath = null);
    }
}
