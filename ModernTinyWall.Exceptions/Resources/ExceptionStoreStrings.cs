using System.Globalization;
using System.Resources;

namespace ModernTinyWall.Exceptions.Resources;

internal static class ExceptionStoreStrings
{
    private static readonly ResourceManager ResourceManager = new("ModernTinyWall.Exceptions.Resources.ExceptionStoreStrings", typeof(ExceptionStoreStrings).Assembly);

    internal static string ExceptionAdded => GetString(nameof(ExceptionAdded));
    internal static string ExecutableExceptionsAdded => GetString(nameof(ExecutableExceptionsAdded));
    internal static string ServiceExceptionAdded => GetString(nameof(ServiceExceptionAdded));
    internal static string PackageExceptionAdded => GetString(nameof(PackageExceptionAdded));
    internal static string ExceptionUpdated => GetString(nameof(ExceptionUpdated));
    internal static string ExceptionRemoved => GetString(nameof(ExceptionRemoved));
    internal static string AllExceptionsRemoved => GetString(nameof(AllExceptionsRemoved));
    internal static string CouldNotLoadSettings => GetString(nameof(CouldNotLoadSettings));
    internal static string TinyWallLocked => GetString(nameof(TinyWallLocked));
    internal static string CouldNotContactService => GetString(nameof(CouldNotContactService));
    internal static string CouldNotUpdateExceptions => GetString(nameof(CouldNotUpdateExceptions));

    internal static string UnexpectedServiceResponse(object response)
    {
        return string.Format(CultureInfo.CurrentCulture, GetString("UnexpectedServiceResponseFormat"), response);
    }

    internal static string ExistingExceptionPrompt(string entryKind, string existingPolicy, string newPolicy)
    {
        return string.Format(CultureInfo.CurrentCulture, GetString("ExistingExceptionPromptFormat"), entryKind, existingPolicy, newPolicy);
    }

    internal static string CannotApplyUnnamedItem(string verb)
    {
        return string.Format(CultureInfo.CurrentCulture, GetString("CannotApplyUnnamedItemFormat"), verb);
    }

    internal static string CannotApplyServiceWithoutPath(string verb)
    {
        return string.Format(CultureInfo.CurrentCulture, GetString("CannotApplyServiceWithoutPathFormat"), verb);
    }

    internal static string CannotApplyPackageWithoutSid(string verb)
    {
        return string.Format(CultureInfo.CurrentCulture, GetString("CannotApplyPackageWithoutSidFormat"), verb);
    }

    internal static string CannotApplyProcessWithoutPath(string verb)
    {
        return string.Format(CultureInfo.CurrentCulture, GetString("CannotApplyProcessWithoutPathFormat"), verb);
    }

    internal static string ExceptionApplied(string policy)
    {
        return string.Format(CultureInfo.CurrentCulture, GetString("ExceptionAppliedFormat"), policy);
    }

    private static string GetString(string name)
    {
        return ResourceManager.GetString(name, CultureInfo.CurrentUICulture) ?? name;
    }
}
