using Servy.CLI.Models;
using Servy.CLI.Options;

namespace Servy.CLI.Validators
{
    public interface IServiceInstallValidator
    {
        CommandResult Validate(InstallServiceOptions opts);
    }
}
